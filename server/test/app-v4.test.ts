import type { FastifyInstance } from "fastify";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { createApp } from "../src/app.js";
import {
  aiNpcRequestSchema,
  computeGroundingRevision,
  MAX_REQUEST_BODY_UTF8_BYTES,
} from "../src/contracts/v4.js";
import { REQUEST_BODY_LIMIT_BYTES } from "../src/config.js";
import type {
  NpcGenerationRequest,
  NpcGenerationResult,
  NpcResponseGenerator,
} from "../src/generator.js";
import {
  InMemoryConversationSessionStore,
  SessionConversationService,
} from "../src/sessions.js";

const groundingContent = {
  background: "The western gate protects Dawnfall.",
  goalsAndValues: "Protect citizens.",
  behavioralRules: ["Do not invent access permissions."],
  dialogueExamples: ["Guard: State your business."],
  facts: [{
    factId: "gate_status",
    kind: "observation" as const,
    statement: "The western gate is closed.",
    priority: 90,
  }],
};

const validRequest = {
  schemaVersion: 4,
  requestId: "req-v4-route",
  sessionId: "session-v4-route",
  character: {
    characterId: "sample-guard",
    displayName: "Guard",
    personality: "Disciplined.",
    speechStyle: "Formal.",
    exampleDialogue: "State your business.",
    defaultEmotion: "neutral",
  },
  grounding: {
    ...groundingContent,
    revision: computeGroundingRevision(groundingContent),
  },
  userText: "Is the gate open?",
  triggers: [],
};

class GroundedGenerator implements NpcResponseGenerator {
  public readonly requests: NpcGenerationRequest[] = [];

  /** Records grounded requests and returns one controlled context-aware result. */
  public async generate(
    request: NpcGenerationRequest,
  ): Promise<NpcGenerationResult> {
    this.requests.push(request);
    return {
      result: {
        dialogue: "Guard: The western gate is closed.",
        emotion: "neutral",
        gesture: "nod",
        matchedTriggerIds: [],
      },
      telemetry: {
        openAiResponseId: "resp-v4-test",
        inputTokens: 1,
        outputTokens: 1,
        totalTokens: 2,
      },
    };
  }
}

/** Recreates the schema-valid 34,994-byte request rejected by the original HTTP limit. */
function createOriginalBoundaryRequest() {
  const content = {
    background: "b".repeat(2048),
    goalsAndValues: "g".repeat(2048),
    behavioralRules: Array.from({ length: 16 }, () => "r".repeat(512)),
    dialogueExamples: Array.from({ length: 8 }, () => "e".repeat(1024)),
    facts: Array.from({ length: 24 }, (_, i) => ({
      factId: `fact_${i}`, kind: "observation" as const, statement: "f".repeat(512), priority: 50,
    })),
  };
  return {
    schemaVersion: 4, requestId: "review-max", sessionId: "review-synthetic-session",
    character: {
      characterId: "review", displayName: "Review", personality: "Brief.",
      speechStyle: "Brief.", exampleDialogue: "Hello.", defaultEmotion: "neutral",
    },
    grounding: { ...content, revision: computeGroundingRevision(content) },
    userText: "Hello.", triggers: [],
  };
}

/** Fills an exact UTF-8 field budget with canonical text that expands during JSON escaping. */
function createEscapedText(bytes: number): string {
  const prefix = '한"\\\n';
  return prefix + "\u0001".repeat(bytes - Buffer.byteLength(prefix) - 1) + "x";
}

/** Adds an ignored same-version field to exercise the exact serialized HTTP byte boundary. */
function createSizedBody(bytes: number): string {
  const payload = { ...validRequest, futurePadding: "" };
  payload.futurePadding = "x".repeat(bytes - Buffer.byteLength(JSON.stringify(payload)));
  return JSON.stringify(payload);
}

describe("Phase 15 Fastify V4 routes", () => {
  let generator: GroundedGenerator;
  let app: FastifyInstance;

  beforeEach(() => {
    generator = new GroundedGenerator();
    app = createApp({
      generator,
      sessionService: new SessionConversationService(
        new InMemoryConversationSessionStore({
          maxTurns: 8,
          maxContextBytes: 16 * 1024,
          idleTtlMs: 30 * 60 * 1_000,
          maxSessions: 128,
        }),
        generator,
      ),
      logger: false,
    });
  });

  afterEach(async () => {
    await app.close();
  });

  it("accepts the original 34,994-byte maximum-grounding regression request", async () => {
    const payload = createOriginalBoundaryRequest();
    expect(aiNpcRequestSchema.safeParse(payload).success).toBe(true);
    expect(Buffer.byteLength(JSON.stringify(payload))).toBe(34994);

    const response = await app.inject({ method: "POST", url: "/v4/npc/respond", payload });

    expect(response.statusCode).toBe(200);
    expect(generator.requests).toHaveLength(1);
    expect(generator.requests[0]?.grounding).toEqual(payload.grounding);
  });

  it("accepts saturated field budgets including Korean and JSON escape expansion", async () => {
    const content = {
      background: createEscapedText(2048),
      goalsAndValues: createEscapedText(2048),
      behavioralRules: Array.from({ length: 16 }, () => createEscapedText(512)),
      dialogueExamples: Array.from({ length: 8 }, () => createEscapedText(1024)),
      facts: Array.from({ length: 32 }, (_, index) => ({
        factId: `fact_${index}`, kind: "observation" as const,
        statement: createEscapedText(index < 16 ? 512 : 256), priority: 50,
      })),
    };
    const payload = {
      ...validRequest,
      grounding: { ...content, revision: computeGroundingRevision(content) },
      userText: createEscapedText(8192),
      triggers: Array.from({ length: 16 }, (_, index) => ({
        triggerId: `trigger_${index}`, conditionDescription: createEscapedText(512),
      })),
    };
    const bodyBytes = Buffer.byteLength(JSON.stringify(payload));
    expect(aiNpcRequestSchema.safeParse(payload).success).toBe(true);
    expect(bodyBytes).toBeGreaterThan(256 * 1024);
    expect(bodyBytes).toBeLessThan(MAX_REQUEST_BODY_UTF8_BYTES);

    const response = await app.inject({ method: "POST", url: "/v4/npc/respond", payload });

    expect(response.statusCode).toBe(200);
    expect(generator.requests[0]?.grounding).toEqual(payload.grounding);
    expect(generator.requests[0]?.triggers).toEqual(payload.triggers);
    expect(generator.requests[0]?.userText).toBe(payload.userText);
  });

  it.each([0, 1])("enforces the V4 serialized body limit with %i excess bytes", async (excess) => {
    expect(MAX_REQUEST_BODY_UTF8_BYTES).toBe(524288);
    const payload = createSizedBody(MAX_REQUEST_BODY_UTF8_BYTES + excess);
    expect(Buffer.byteLength(payload)).toBe(MAX_REQUEST_BODY_UTF8_BYTES + excess);
    const response = await app.inject({
      method: "POST", url: "/v4/npc/respond",
      headers: { "content-type": "application/json" }, payload,
    });

    expect(response.statusCode).toBe(excess === 0 ? 200 : 413);
    expect(generator.requests).toHaveLength(excess === 0 ? 1 : 0);
    if (excess > 0) {
      expect(response.json()).toMatchObject({
        schemaVersion: 4, status: "error",
        error: { code: "invalid_request", retryable: false },
      });
      expect(response.body).not.toContain("futurePadding");
    }
  });

  it.each([
    ["/v1/npc/respond", 1],
    ["/v2/npc/respond", 2],
    ["/v3/npc/respond", 3],
    ["/v2/npc/sessions/reset", 2],
    ["/v3/npc/sessions/reset", 3],
    ["/v4/npc/sessions/reset", 4],
    ["/v1/speech/synthesize", 1],
  ])("preserves the 32 KiB JSON limit for %s", async (url, schemaVersion) => {
    expect(REQUEST_BODY_LIMIT_BYTES).toBe(32768);
    const response = await app.inject({
      method: "POST", url: String(url),
      headers: { "content-type": "application/json" },
      payload: createSizedBody(REQUEST_BODY_LIMIT_BYTES + 1),
    });

    expect(response.statusCode).toBe(413);
    expect(response.json()).toMatchObject({
      schemaVersion, status: "error", error: { code: "invalid_request", retryable: false },
    });
    expect(generator.requests).toHaveLength(0);
  });

  it("forwards grounding for the current turn while preserving text-only history", async () => {
    const first = await app.inject({
      method: "POST",
      url: "/v4/npc/respond",
      payload: validRequest,
    });
    const second = await app.inject({
      method: "POST",
      url: "/v4/npc/respond",
      payload: {
        ...validRequest,
        requestId: "req-v4-second",
        userText: "What did I ask?",
      },
    });

    expect(first.statusCode).toBe(200);
    expect(second.statusCode).toBe(200);
    expect(first.json()).toMatchObject({
      schemaVersion: 4,
      requestId: validRequest.requestId,
      result: { matchedTriggerIds: [] },
    });
    expect(generator.requests[0]?.grounding).toEqual(validRequest.grounding);
    expect(generator.requests[1]?.history).toEqual([
      { role: "user", content: "Is the gate open?" },
      { role: "assistant", content: "Guard: The western gate is closed." },
    ]);
    expect(generator.requests[1]?.history).not.toContainEqual(
      expect.objectContaining({ content: expect.stringContaining("Dawnfall") }),
    );
  });

  it("returns route-correct V4 errors for stale revisions and versions", async () => {
    const stale = await app.inject({
      method: "POST",
      url: "/v4/npc/respond",
      payload: {
        ...validRequest,
        grounding: { ...validRequest.grounding, background: "Changed." },
      },
    });
    const version = await app.inject({
      method: "POST",
      url: "/v4/npc/respond",
      payload: { ...validRequest, schemaVersion: 3 },
    });

    expect(stale.statusCode).toBe(400);
    expect(stale.json()).toMatchObject({
      schemaVersion: 4,
      error: { code: "invalid_request" },
    });
    expect(version.statusCode).toBe(400);
    expect(version.json()).toMatchObject({
      requestId: validRequest.requestId,
      error: { code: "unsupported_schema_version" },
    });
  });

  it("maps malformed JSON to a safe route-correct V4 envelope", async () => {
    const response = await app.inject({
      method: "POST",
      url: "/v4/npc/respond",
      headers: { "content-type": "application/json" },
      payload: "{",
    });

    expect(response.statusCode).toBe(400);
    expect(response.json()).toMatchObject({
      schemaVersion: 4,
      status: "error",
      error: { code: "invalid_request", retryable: false },
    });
    expect(response.body).not.toContain("SyntaxError");
  });

  it("resets V4 sessions idempotently", async () => {
    const response = await app.inject({
      method: "POST",
      url: "/v4/npc/sessions/reset",
      payload: {
        schemaVersion: 4,
        requestId: "req-v4-reset",
        sessionId: "unknown-session",
        characterId: "sample-guard",
      },
    });

    expect(response.statusCode).toBe(200);
    expect(response.json()).toMatchObject({ result: { reset: true } });
  });
});
