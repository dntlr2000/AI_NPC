import type { FastifyInstance } from "fastify";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { createApp } from "../src/app.js";
import { NpcServiceError } from "../src/errors.js";
import type {
  NpcGenerationRequest,
  NpcGenerationResult,
  NpcResponseGenerator,
} from "../src/generator.js";
import {
  InMemoryConversationSessionStore,
  SessionConversationService,
} from "../src/sessions.js";

const validRequest = {
  schemaVersion: 1,
  requestId: "req-route-001",
  character: {
    characterId: "sample-luna",
    displayName: "Luna",
    personality: "Playful and curious.",
    speechStyle: "Warm and brief.",
    exampleDialogue: "Tell me about an adventure.",
    defaultEmotion: "happy",
  },
  userText: "Hello!",
};

const validV2Request = {
  ...validRequest,
  schemaVersion: 2,
  requestId: "req-route-v2-001",
  sessionId: "session-route-v2-001",
};

/** Supplies deterministic route results without performing an OpenAI request. */
class StubGenerator implements NpcResponseGenerator {
  public error: Error | null = null;
  public lastRequest: NpcGenerationRequest | null = null;

  /** Returns one fixed structured reply or the configured safe failure. */
  public async generate(
    request: NpcGenerationRequest,
  ): Promise<NpcGenerationResult> {
    this.lastRequest = request;
    if (this.error !== null) {
      throw this.error;
    }

    return {
      result: {
        dialogue: "Luna: Hello!",
        emotion: "happy",
        gesture: "wave",
      },
      telemetry: {
        openAiResponseId: "resp-test",
        inputTokens: 10,
        outputTokens: 5,
        totalTokens: 15,
      },
    };
  }
}

describe("Phase 5 Fastify app", () => {
  let generator: StubGenerator;
  let app: FastifyInstance;

  beforeEach(() => {
    generator = new StubGenerator();
    const sessionService = new SessionConversationService(
      new InMemoryConversationSessionStore({
        maxTurns: 8,
        maxContextBytes: 16 * 1024,
        idleTtlMs: 30 * 60 * 1_000,
        maxSessions: 128,
      }),
      generator,
    );
    app = createApp({ generator, sessionService, logger: false });
  });

  afterEach(async () => {
    await app.close();
  });

  it("reports health without invoking the generator", async () => {
    const response = await app.inject({ method: "GET", url: "/healthz" });

    expect(response.statusCode).toBe(200);
    expect(response.json()).toEqual({ status: "ok" });
    expect(generator.lastRequest).toBeNull();
  });

  it("returns one correlated V1 success envelope", async () => {
    const response = await app.inject({
      method: "POST",
      url: "/v1/npc/respond",
      payload: validRequest,
    });

    expect(response.statusCode).toBe(200);
    expect(response.json()).toMatchObject({
      schemaVersion: 1,
      requestId: "req-route-001",
      status: "success",
      result: {
        dialogue: "Luna: Hello!",
        emotion: "happy",
        gesture: "wave",
      },
    });
    expect(generator.lastRequest?.userText).toBe("Hello!");
    expect(generator.lastRequest?.history).toEqual([]);
  });

  it("stores successful V2 turns and returns correlated responses", async () => {
    const first = await app.inject({
      method: "POST",
      url: "/v2/npc/respond",
      payload: validV2Request,
    });
    const second = await app.inject({
      method: "POST",
      url: "/v2/npc/respond",
      payload: {
        ...validV2Request,
        requestId: "req-route-v2-002",
        userText: "What did I say?",
      },
    });

    expect(first.statusCode).toBe(200);
    expect(first.json()).toMatchObject({
      schemaVersion: 2,
      requestId: "req-route-v2-001",
      status: "success",
    });
    expect(second.statusCode).toBe(200);
    expect(generator.lastRequest?.history).toEqual([
      { role: "user", content: "Hello!" },
      { role: "assistant", content: "Luna: Hello!" },
    ]);
  });

  it("resets one V2 session idempotently", async () => {
    await app.inject({
      method: "POST",
      url: "/v2/npc/respond",
      payload: validV2Request,
    });
    const reset = await app.inject({
      method: "POST",
      url: "/v2/npc/sessions/reset",
      payload: {
        schemaVersion: 2,
        requestId: "req-route-reset-001",
        sessionId: validV2Request.sessionId,
        characterId: validV2Request.character.characterId,
      },
    });
    const repeatedReset = await app.inject({
      method: "POST",
      url: "/v2/npc/sessions/reset",
      payload: {
        schemaVersion: 2,
        requestId: "req-route-reset-002",
        sessionId: "session-unknown",
        characterId: validV2Request.character.characterId,
      },
    });

    expect(reset.statusCode).toBe(200);
    expect(reset.json()).toMatchObject({
      schemaVersion: 2,
      requestId: "req-route-reset-001",
      status: "success",
      result: { reset: true },
    });
    expect(repeatedReset.statusCode).toBe(200);
  });

  it("returns route-correct V2 errors for malformed and invalid bodies", async () => {
    const invalid = await app.inject({
      method: "POST",
      url: "/v2/npc/respond",
      payload: { ...validV2Request, sessionId: "" },
    });
    const malformedReset = await app.inject({
      method: "POST",
      url: "/v2/npc/sessions/reset",
      headers: { "content-type": "application/json" },
      payload: "{not-json",
    });

    expect(invalid.statusCode).toBe(400);
    expect(invalid.json()).toMatchObject({
      schemaVersion: 2,
      status: "error",
      error: { code: "invalid_request" },
    });
    expect(malformedReset.statusCode).toBe(400);
    expect(malformedReset.json()).toMatchObject({
      schemaVersion: 2,
      status: "error",
      error: { code: "invalid_request" },
    });
  });

  it("rejects invalid and unsupported requests before generation", async () => {
    const invalidResponse = await app.inject({
      method: "POST",
      url: "/v1/npc/respond",
      payload: { ...validRequest, userText: " " },
    });
    const versionResponse = await app.inject({
      method: "POST",
      url: "/v1/npc/respond",
      payload: { ...validRequest, schemaVersion: 2 },
    });

    expect(invalidResponse.statusCode).toBe(400);
    expect(invalidResponse.json()).toMatchObject({
      requestId: "req-route-001",
      status: "error",
      error: { code: "invalid_request", retryable: false },
    });
    expect(versionResponse.statusCode).toBe(400);
    expect(versionResponse.json()).toMatchObject({
      error: { code: "unsupported_schema_version", retryable: false },
    });
    expect(generator.lastRequest).toBeNull();
  });

  it("returns a V1 error for malformed JSON", async () => {
    const response = await app.inject({
      method: "POST",
      url: "/v1/npc/respond",
      headers: { "content-type": "application/json" },
      payload: "{not-json",
    });

    expect(response.statusCode).toBe(400);
    expect(response.json()).toMatchObject({
      schemaVersion: 1,
      status: "error",
      error: { code: "invalid_request", retryable: false },
    });
  });

  it.each([
    ["content_refused", 422, false],
    ["rate_limited", 429, true],
    ["upstream_timeout", 504, true],
    ["upstream_unavailable", 502, true],
    ["upstream_invalid_response", 502, true],
    ["internal_error", 500, false],
  ])(
    "maps %s into its documented HTTP and V1 error",
    async (code, statusCode, retryable) => {
      generator.error = new NpcServiceError(
        code,
        "Safe public message.",
        statusCode,
        retryable,
        "test_error",
      );

      const response = await app.inject({
        method: "POST",
        url: "/v1/npc/respond",
        payload: validRequest,
      });

      expect(response.statusCode).toBe(statusCode);
      expect(response.json()).toMatchObject({
        requestId: "req-route-001",
        status: "error",
        error: { code, retryable },
      });
    },
  );
});
