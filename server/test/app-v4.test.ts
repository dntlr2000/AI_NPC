import type { FastifyInstance } from "fastify";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { createApp } from "../src/app.js";
import { computeGroundingRevision } from "../src/contracts/v4.js";
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
