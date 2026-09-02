import type { FastifyInstance } from "fastify";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { createApp } from "../src/app.js";
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
  schemaVersion: 3,
  requestId: "req-v3-route",
  sessionId: "session-v3-route",
  character: {
    characterId: "sample-guide",
    displayName: "Guide",
    personality: "Helpful.",
    speechStyle: "Brief.",
    exampleDialogue: "Hello.",
    defaultEmotion: "neutral",
  },
  userText: "Open the gate.",
  triggers: [
    {
      triggerId: "open_gate",
      conditionDescription: "The player asks to open the gate.",
    },
  ],
};

class ActionGenerator implements NpcResponseGenerator {
  public lastRequest: NpcGenerationRequest | null = null;
  public matchedTriggerIds: string[] = ["open_gate"];

  /** Records one action-aware generation request and returns controlled IDs. */
  public async generate(
    request: NpcGenerationRequest,
  ): Promise<NpcGenerationResult> {
    this.lastRequest = request;
    return {
      result: {
        dialogue: "Guide: I will check.",
        emotion: "happy",
        gesture: "nod",
        matchedTriggerIds: this.matchedTriggerIds,
      },
      telemetry: {
        openAiResponseId: "resp-v3-test",
        inputTokens: 1,
        outputTokens: 1,
        totalTokens: 2,
      },
    };
  }
}

describe("Phase 11 Fastify V3 routes", () => {
  let generator: ActionGenerator;
  let app: FastifyInstance;

  beforeEach(() => {
    generator = new ActionGenerator();
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

  it("returns one correlated V3 response and forwards only semantic triggers", async () => {
    const response = await app.inject({
      method: "POST",
      url: "/v3/npc/respond",
      payload: validRequest,
    });

    expect(response.statusCode).toBe(200);
    expect(response.json()).toMatchObject({
      schemaVersion: 3,
      requestId: "req-v3-route",
      status: "success",
      result: { matchedTriggerIds: ["open_gate"] },
    });
    expect(generator.lastRequest?.triggers).toEqual(validRequest.triggers);
    expect(JSON.stringify(generator.lastRequest)).not.toContain("actionId");
  });

  it("rejects unknown generated trigger IDs without committing the turn", async () => {
    generator.matchedTriggerIds = ["invented_trigger"];
    const rejected = await app.inject({
      method: "POST",
      url: "/v3/npc/respond",
      payload: validRequest,
    });
    generator.matchedTriggerIds = [];
    const retry = await app.inject({
      method: "POST",
      url: "/v3/npc/respond",
      payload: { ...validRequest, requestId: "req-v3-retry" },
    });

    expect(rejected.statusCode).toBe(502);
    expect(rejected.json()).toMatchObject({
      status: "error",
      error: { code: "upstream_invalid_response" },
    });
    expect(retry.statusCode).toBe(200);
    expect(generator.lastRequest?.history).toEqual([]);
  });

  it("resets V3 sessions idempotently and rejects malformed requests", async () => {
    const reset = await app.inject({
      method: "POST",
      url: "/v3/npc/sessions/reset",
      payload: {
        schemaVersion: 3,
        requestId: "req-v3-reset",
        sessionId: "session-unknown",
        characterId: "sample-guide",
      },
    });
    const invalid = await app.inject({
      method: "POST",
      url: "/v3/npc/respond",
      payload: { ...validRequest, triggers: [] },
    });

    expect(reset.statusCode).toBe(200);
    expect(reset.json()).toMatchObject({ result: { reset: true } });
    expect(invalid.statusCode).toBe(400);
    expect(invalid.json()).toMatchObject({
      schemaVersion: 3,
      error: { code: "invalid_request" },
    });
  });

  it("returns route-correct V3 errors for malformed JSON and versions", async () => {
    const malformed = await app.inject({
      method: "POST",
      url: "/v3/npc/respond",
      headers: { "content-type": "application/json" },
      payload: "{",
    });
    const wrongVersion = await app.inject({
      method: "POST",
      url: "/v3/npc/respond",
      payload: { ...validRequest, schemaVersion: 2 },
    });

    expect(malformed.statusCode).toBe(400);
    expect(malformed.json()).toMatchObject({
      schemaVersion: 3,
      status: "error",
      error: { code: "invalid_request" },
    });
    expect(wrongVersion.statusCode).toBe(400);
    expect(wrongVersion.json()).toMatchObject({
      schemaVersion: 3,
      requestId: validRequest.requestId,
      error: { code: "unsupported_schema_version" },
    });
  });
});
