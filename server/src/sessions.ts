import { Buffer } from "node:buffer";
import type { AiNpcRequest, AiNpcSessionResetRequest } from "./contracts/v2.js";
import { NpcServiceError } from "./errors.js";
import type {
  ConversationMessage,
  NpcGenerationResult,
  NpcResponseGenerator,
} from "./generator.js";

export interface ConversationTurn {
  readonly userText: string;
  readonly assistantText: string;
}

export interface SessionStoreOptions {
  readonly maxTurns: number;
  readonly maxContextBytes: number;
  readonly idleTtlMs: number;
  readonly maxSessions: number;
}

export interface SessionLease {
  readonly sessionId: string;
  readonly characterId: string;
  readonly history: readonly ConversationTurn[];
  readonly createdNew: boolean;
}

interface SessionRecord {
  readonly characterId: string;
  readonly turns: ConversationTurn[];
  lastAccessMs: number;
  busy: boolean;
}

/** Stores bounded, process-local conversation turns and enforces per-session exclusion. */
export class InMemoryConversationSessionStore {
  private readonly sessions = new Map<string, SessionRecord>();

  /** Creates a bounded store with an injectable clock for deterministic tests. */
  public constructor(
    private readonly options: SessionStoreOptions,
    private readonly now: () => number = Date.now,
  ) {
    validateOptions(options);
  }

  /** Reserves one session and returns a stable copy of its committed history. */
  public begin(sessionId: string, characterId: string): SessionLease {
    const currentTime = this.now();
    this.removeExpiredSessions(currentTime);

    let record = this.sessions.get(sessionId);
    let createdNew = false;
    if (record !== undefined) {
      ensureCharacterMatch(record, characterId);
      ensureSessionIdle(record);
    } else {
      this.makeRoomForSession();
      record = {
        characterId,
        turns: [],
        lastAccessMs: currentTime,
        busy: false,
      };
      this.sessions.set(sessionId, record);
      createdNew = true;
    }

    record.busy = true;
    record.lastAccessMs = currentTime;
    return {
      sessionId,
      characterId,
      history: record.turns.map((turn) => ({ ...turn })),
      createdNew,
    };
  }

  /** Atomically appends one successful turn and releases its session reservation. */
  public commit(lease: SessionLease, turn: ConversationTurn): void {
    const record = this.requireActiveLease(lease);
    record.turns.push({ ...turn });
    trimTurns(record.turns, this.options);
    record.busy = false;
    record.lastAccessMs = this.now();
  }

  /** Releases a failed request without persisting its pending user message. */
  public abort(lease: SessionLease): void {
    const record = this.sessions.get(lease.sessionId);
    if (record === undefined || record.characterId !== lease.characterId) {
      return;
    }

    record.busy = false;
    record.lastAccessMs = this.now();
    if (lease.createdNew && record.turns.length === 0) {
      this.sessions.delete(lease.sessionId);
    }
  }

  /** Clears one idle session while treating an unknown or expired ID as success. */
  public reset(sessionId: string, characterId: string): void {
    const currentTime = this.now();
    this.removeExpiredSessions(currentTime);
    const record = this.sessions.get(sessionId);
    if (record === undefined) {
      return;
    }

    ensureCharacterMatch(record, characterId);
    ensureSessionIdle(record);
    record.turns.length = 0;
    record.lastAccessMs = currentTime;
  }

  /** Resolves and validates the record owned by one active lease. */
  private requireActiveLease(lease: SessionLease): SessionRecord {
    const record = this.sessions.get(lease.sessionId);
    if (record === undefined
      || record.characterId !== lease.characterId
      || !record.busy) {
      throw new NpcServiceError(
        "internal_error",
        "The backend could not update the conversation session.",
        500,
        false,
        "session_invalid_lease",
      );
    }

    return record;
  }

  /** Removes idle records whose last activity exceeded the configured TTL. */
  private removeExpiredSessions(currentTime: number): void {
    for (const [sessionId, record] of this.sessions) {
      if (!record.busy
        && currentTime - record.lastAccessMs >= this.options.idleTtlMs) {
        this.sessions.delete(sessionId);
      }
    }
  }

  /** Evicts the least recently used idle record or reports bounded capacity. */
  private makeRoomForSession(): void {
    if (this.sessions.size < this.options.maxSessions) {
      return;
    }

    let oldestSessionId: string | undefined;
    let oldestAccess = Number.POSITIVE_INFINITY;
    for (const [sessionId, record] of this.sessions) {
      if (!record.busy && record.lastAccessMs < oldestAccess) {
        oldestSessionId = sessionId;
        oldestAccess = record.lastAccessMs;
      }
    }

    if (oldestSessionId === undefined) {
      throw new NpcServiceError(
        "session_capacity_reached",
        "The conversation session capacity is temporarily full.",
        503,
        true,
        "session_capacity_reached",
      );
    }

    this.sessions.delete(oldestSessionId);
  }
}

/** Coordinates generation with an atomic session lease and successful-turn commit. */
export class SessionConversationService {
  /** Captures the bounded store and replaceable response generator. */
  public constructor(
    private readonly store: InMemoryConversationSessionStore,
    private readonly generator: NpcResponseGenerator,
  ) {}

  /** Generates one response from committed history and stores only its success. */
  public async respond(
    request: AiNpcRequest,
    cancellationSignal: AbortSignal,
  ): Promise<NpcGenerationResult> {
    cancellationSignal.throwIfAborted();
    const lease = this.store.begin(
      request.sessionId,
      request.character.characterId,
    );

    try {
      const generated = await this.generator.generate(
        {
          character: request.character,
          history: toConversationMessages(lease.history),
          userText: request.userText,
        },
        cancellationSignal,
      );
      cancellationSignal.throwIfAborted();
      this.store.commit(lease, {
        userText: request.userText,
        assistantText: generated.result.dialogue,
      });
      return generated;
    } catch (error: unknown) {
      this.store.abort(lease);
      throw error;
    }
  }

  /** Clears the addressed session without creating a record for an unknown ID. */
  public reset(request: AiNpcSessionResetRequest): void {
    this.store.reset(request.sessionId, request.characterId);
  }
}

/** Validates positive store bounds before any session state is accepted. */
function validateOptions(options: SessionStoreOptions): void {
  if (options.maxTurns <= 0
    || options.maxContextBytes <= 0
    || options.idleTtlMs <= 0
    || options.maxSessions <= 0) {
    throw new RangeError("All session store limits must be greater than zero.");
  }
}

/** Rejects accidental reuse of one opaque ID for another character. */
function ensureCharacterMatch(record: SessionRecord, characterId: string): void {
  if (record.characterId !== characterId) {
    throw new NpcServiceError(
      "session_character_mismatch",
      "The conversation session belongs to another character.",
      409,
      false,
      "session_character_mismatch",
    );
  }
}

/** Rejects overlapping generation or reset work for the same session. */
function ensureSessionIdle(record: SessionRecord): void {
  if (record.busy) {
    throw new NpcServiceError(
      "session_busy",
      "The conversation session is already processing a request.",
      409,
      true,
      "session_busy",
    );
  }
}

/** Removes oldest complete turns until both count and byte limits are met. */
function trimTurns(
  turns: ConversationTurn[],
  options: SessionStoreOptions,
): void {
  let contextBytes = turns.reduce(
    (total, turn) => total + getTurnBytes(turn),
    0,
  );

  while (turns.length > options.maxTurns
    || contextBytes > options.maxContextBytes) {
    const removed = turns.shift();
    if (removed === undefined) {
      break;
    }

    contextBytes -= getTurnBytes(removed);
  }
}

/** Counts exact UTF-8 payload bytes for one complete stored turn. */
function getTurnBytes(turn: ConversationTurn): number {
  return Buffer.byteLength(turn.userText, "utf8")
    + Buffer.byteLength(turn.assistantText, "utf8");
}

/** Flattens complete turns into ordered user and assistant model inputs. */
function toConversationMessages(
  turns: readonly ConversationTurn[],
): ConversationMessage[] {
  const messages: ConversationMessage[] = [];
  for (const turn of turns) {
    messages.push({ role: "user", content: turn.userText });
    messages.push({ role: "assistant", content: turn.assistantText });
  }

  return messages;
}
