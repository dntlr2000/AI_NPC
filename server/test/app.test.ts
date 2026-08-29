import type { FastifyInstance } from "fastify";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { createApp } from "../src/app.js";
import { NpcServiceError } from "../src/errors.js";
import type {
  NpcGenerationResult,
  NpcResponseGenerator,
} from "../src/generator.js";
import type { AiNpcRequest } from "../src/contracts/v1.js";

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

/** Supplies deterministic route results without performing an OpenAI request. */
class StubGenerator implements NpcResponseGenerator {
  public error: Error | null = null;
  public lastRequest: AiNpcRequest | null = null;

  /** Returns one fixed structured reply or the configured safe failure. */
  public async generate(request: AiNpcRequest): Promise<NpcGenerationResult> {
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

describe("Phase 4 Fastify app", () => {
  let generator: StubGenerator;
  let app: FastifyInstance;

  beforeEach(() => {
    generator = new StubGenerator();
    app = createApp({ generator, logger: false });
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
