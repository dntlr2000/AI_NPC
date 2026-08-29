import { describe, expect, it } from "vitest";
import type { AiNpcRequest } from "../src/contracts/v2.js";
import type {
  NpcGenerationRequest,
  NpcGenerationResult,
  NpcResponseGenerator,
} from "../src/generator.js";
import {
  InMemoryConversationSessionStore,
  SessionConversationService,
} from "../src/sessions.js";

const defaultOptions = {
  maxTurns: 8,
  maxContextBytes: 16 * 1024,
  idleTtlMs: 30 * 60 * 1_000,
  maxSessions: 128,
};

/** Creates one valid V2 request with selected session and character identity. */
function createRequest(
  sessionId: string,
  characterId = "sample-luna",
  userText = "안녕!",
): AiNpcRequest {
  return {
    schemaVersion: 2,
    requestId: `req-${sessionId}`,
    sessionId,
    character: {
      characterId,
      displayName: characterId,
      personality: "Friendly.",
      speechStyle: "Brief.",
      exampleDialogue: "Hello.",
      defaultEmotion: "neutral",
    },
    userText,
  };
}

/** Creates a fixed structured generation result for session tests. */
function createResult(dialogue: string): NpcGenerationResult {
  return {
    result: { dialogue, emotion: "neutral", gesture: "none" },
    telemetry: {
      openAiResponseId: "resp-session-test",
      inputTokens: 1,
      outputTokens: 1,
      totalTokens: 2,
    },
  };
}

/** Records explicit generator inputs and delegates output to one test function. */
class RecordingGenerator implements NpcResponseGenerator {
  public readonly requests: NpcGenerationRequest[] = [];

  /** Captures one immutable request view before returning controlled output. */
  public constructor(
    private readonly generateResult: (
      request: NpcGenerationRequest,
    ) => Promise<NpcGenerationResult>,
  ) {}

  /** Records and generates one deterministic test response. */
  public generate(
    request: NpcGenerationRequest,
    _cancellationSignal: AbortSignal,
  ): Promise<NpcGenerationResult> {
    this.requests.push(request);
    return this.generateResult(request);
  }
}

describe("SessionConversationService", () => {
  it("commits only successful complete turns and replays them in order", async () => {
    const generator = new RecordingGenerator(async (request) =>
      createResult(request.userText === "첫 질문" ? "첫 답변" : "두 번째 답변"),
    );
    const service = new SessionConversationService(
      new InMemoryConversationSessionStore(defaultOptions),
      generator,
    );

    await service.respond(
      createRequest("session-a", "sample-luna", "첫 질문"),
      new AbortController().signal,
    );
    await service.respond(
      createRequest("session-a", "sample-luna", "두 번째 질문"),
      new AbortController().signal,
    );

    expect(generator.requests[0]?.history).toEqual([]);
    expect(generator.requests[1]?.history).toEqual([
      { role: "user", content: "첫 질문" },
      { role: "assistant", content: "첫 답변" },
    ]);
  });

  it("does not store a failed generation or its user message", async () => {
    let shouldFail = true;
    const generator = new RecordingGenerator(async () => {
      if (shouldFail) {
        shouldFail = false;
        throw new Error("private failure");
      }

      return createResult("성공");
    });
    const service = new SessionConversationService(
      new InMemoryConversationSessionStore(defaultOptions),
      generator,
    );

    await expect(
      service.respond(
        createRequest("session-failure", "sample-luna", "저장 금지"),
        new AbortController().signal,
      ),
    ).rejects.toThrow();
    await service.respond(
      createRequest("session-failure", "sample-luna", "다시 시도"),
      new AbortController().signal,
    );

    expect(generator.requests[1]?.history).toEqual([]);
  });

  it("does not store a cancelled generation or its user message", async () => {
    let shouldCancel = true;
    const generator = new RecordingGenerator(async () => {
      if (shouldCancel) {
        shouldCancel = false;
        const cancellation = new Error("cancelled");
        cancellation.name = "AbortError";
        throw cancellation;
      }

      return createResult("성공");
    });
    const service = new SessionConversationService(
      new InMemoryConversationSessionStore(defaultOptions),
      generator,
    );

    await expect(
      service.respond(
        createRequest("session-cancel", "sample-luna", "취소된 입력"),
        new AbortController().signal,
      ),
    ).rejects.toMatchObject({ name: "AbortError" });
    await service.respond(
      createRequest("session-cancel", "sample-luna", "다시 시도"),
      new AbortController().signal,
    );

    expect(generator.requests[1]?.history).toEqual([]);
  });

  it("does not commit when cancellation wins after generation resolves", async () => {
    const cancellation = new AbortController();
    let abortAfterGeneration = true;
    const generator = new RecordingGenerator(async () => {
      if (abortAfterGeneration) {
        abortAfterGeneration = false;
        cancellation.abort();
      }

      return createResult("완료");
    });
    const service = new SessionConversationService(
      new InMemoryConversationSessionStore(defaultOptions),
      generator,
    );

    await expect(
      service.respond(
        createRequest("session-cancel-race", "sample-luna", "취소 경합"),
        cancellation.signal,
      ),
    ).rejects.toMatchObject({ name: "AbortError" });
    await service.respond(
      createRequest("session-cancel-race", "sample-luna", "다시 시도"),
      new AbortController().signal,
    );

    expect(generator.requests[1]?.history).toEqual([]);
  });

  it("isolates sessions, rejects character reuse, and resets only one session", async () => {
    const generator = new RecordingGenerator(async (request) =>
      createResult(`답변:${request.userText}`),
    );
    const service = new SessionConversationService(
      new InMemoryConversationSessionStore(defaultOptions),
      generator,
    );

    await service.respond(
      createRequest("session-luna", "sample-luna", "루나 사실"),
      new AbortController().signal,
    );
    await service.respond(
      createRequest("session-guard", "sample-guard", "가드 사실"),
      new AbortController().signal,
    );
    service.reset({
      schemaVersion: 2,
      requestId: "req-reset-luna",
      sessionId: "session-luna",
      characterId: "sample-luna",
    });
    service.reset({
      schemaVersion: 2,
      requestId: "req-reset-unknown",
      sessionId: "session-unknown",
      characterId: "sample-luna",
    });

    await service.respond(
      createRequest("session-luna", "sample-luna", "루나 확인"),
      new AbortController().signal,
    );
    await service.respond(
      createRequest("session-guard", "sample-guard", "가드 확인"),
      new AbortController().signal,
    );

    expect(generator.requests[2]?.history).toEqual([]);
    expect(generator.requests[3]?.history).toHaveLength(2);
    await expect(
      service.respond(
        createRequest("session-guard", "sample-luna", "잘못된 캐릭터"),
        new AbortController().signal,
      ),
    ).rejects.toMatchObject({ code: "session_character_mismatch" });
  });

  it("rejects overlapping work for one session but allows different sessions", async () => {
    let completeFirst: ((value: NpcGenerationResult) => void) | undefined;
    const generator = new RecordingGenerator(
      (request) => request.userText === "대기"
        ? new Promise<NpcGenerationResult>((resolve) => {
            completeFirst = resolve;
          })
        : Promise.resolve(createResult("다른 세션 완료")),
    );
    const service = new SessionConversationService(
      new InMemoryConversationSessionStore(defaultOptions),
      generator,
    );

    const pending = service.respond(
      createRequest("session-busy", "sample-luna", "대기"),
      new AbortController().signal,
    );
    await expect(
      service.respond(
        createRequest("session-busy", "sample-luna", "중복"),
        new AbortController().signal,
      ),
    ).rejects.toMatchObject({ code: "session_busy", retryable: true });
    expect(() => service.reset({
      schemaVersion: 2,
      requestId: "req-reset-busy",
      sessionId: "session-busy",
      characterId: "sample-luna",
    })).toThrowError(
      expect.objectContaining({ code: "session_busy", retryable: true }),
    );
    await expect(
      service.respond(
        createRequest("session-other", "sample-luna", "독립"),
        new AbortController().signal,
      ),
    ).resolves.toMatchObject({ result: { dialogue: "다른 세션 완료" } });

    completeFirst?.(createResult("대기 완료"));
    await pending;
  });
});

describe("InMemoryConversationSessionStore limits", () => {
  it("trims oldest whole turns by count", () => {
    const store = new InMemoryConversationSessionStore({
      ...defaultOptions,
      maxTurns: 2,
      maxContextBytes: 12,
    });

    for (const value of ["aa", "bb", "cc"]) {
      const lease = store.begin("session-trim", "sample-luna");
      store.commit(lease, { userText: value, assistantText: value });
    }

    const lease = store.begin("session-trim", "sample-luna");
    expect(lease.history).toEqual([
      { userText: "bb", assistantText: "bb" },
      { userText: "cc", assistantText: "cc" },
    ]);
    store.abort(lease);
  });

  it("trims oldest whole turns by exact UTF-8 byte budget", () => {
    const store = new InMemoryConversationSessionStore({
      ...defaultOptions,
      maxContextBytes: 8,
    });
    const first = store.begin("session-byte-trim", "sample-luna");
    store.commit(first, { userText: "가", assistantText: "나" });
    const second = store.begin("session-byte-trim", "sample-luna");
    store.commit(second, { userText: "aa", assistantText: "bb" });

    const lease = store.begin("session-byte-trim", "sample-luna");
    expect(lease.history).toEqual([
      { userText: "aa", assistantText: "bb" },
    ]);
    store.abort(lease);
  });

  it("expires idle sessions and rejects capacity when every record is busy", () => {
    let currentTime = 0;
    const store = new InMemoryConversationSessionStore(
      { ...defaultOptions, idleTtlMs: 100, maxSessions: 1 },
      () => currentTime,
    );
    const committed = store.begin("session-expire", "sample-luna");
    store.commit(committed, { userText: "fact", assistantText: "stored" });
    currentTime = 100;

    const recreated = store.begin("session-expire", "sample-luna");
    expect(recreated.history).toEqual([]);
    expect(() => store.begin("session-overflow", "sample-luna")).toThrowError(
      expect.objectContaining({ code: "session_capacity_reached" }),
    );
    store.abort(recreated);
  });

  it("evicts the least recently used idle session at capacity", () => {
    let currentTime = 1;
    const store = new InMemoryConversationSessionStore(
      { ...defaultOptions, maxSessions: 2 },
      () => currentTime,
    );
    const first = store.begin("session-old", "sample-luna");
    store.commit(first, { userText: "old", assistantText: "old" });
    currentTime = 2;
    const second = store.begin("session-new", "sample-luna");
    store.commit(second, { userText: "new", assistantText: "new" });
    currentTime = 3;
    const third = store.begin("session-third", "sample-luna");
    store.abort(third);

    const recreatedOld = store.begin("session-old", "sample-luna");
    expect(recreatedOld.history).toEqual([]);
    store.abort(recreatedOld);
  });

  it("preserves a reset session character binding after aborted work", () => {
    const store = new InMemoryConversationSessionStore(defaultOptions);
    const original = store.begin("session-reset", "sample-luna");
    store.commit(original, { userText: "fact", assistantText: "stored" });
    store.reset("session-reset", "sample-luna");

    const retry = store.begin("session-reset", "sample-luna");
    store.abort(retry);

    expect(() => store.begin("session-reset", "sample-guard")).toThrowError(
      expect.objectContaining({ code: "session_character_mismatch" }),
    );
  });
});
