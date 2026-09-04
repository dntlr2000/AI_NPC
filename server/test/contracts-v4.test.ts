import { describe, expect, it } from "vitest";
import {
  aiNpcRequestSchema,
  aiNpcResponseSchema,
  computeGroundingRevision,
  createResetSuccessResponse,
  createSuccessResponse,
  MAX_FACT_COUNT,
} from "../src/contracts/v4.js";

const groundingContent = {
  background: "The western gate protects Dawnfall.",
  goalsAndValues: "Protect citizens and honor lawful travelers.",
  behavioralRules: [
    "Never reveal guard rotations.",
    "Prefer de-escalation.",
  ],
  dialogueExamples: ["Guard: State your business."],
  facts: [
    {
      factId: "gate_status",
      kind: "observation" as const,
      statement: "The western gate is closed.",
      priority: 90,
    },
    {
      factId: "city_founder",
      kind: "lore" as const,
      statement: "Dawnfall was founded by Queen Mira.",
      priority: 40,
    },
    {
      factId: "guard_suspicion",
      kind: "belief" as const,
      statement: "The traveler may be hiding something.",
      priority: 50,
    },
  ],
};

const validRequest = {
  schemaVersion: 4,
  requestId: "req-v4-contract",
  sessionId: "session-v4-contract",
  character: {
    characterId: "sample-guard",
    displayName: "Guard",
    personality: "Disciplined and observant.",
    speechStyle: "Formal and concise.",
    exampleDialogue: "State your business.",
    defaultEmotion: "neutral",
  },
  grounding: {
    ...groundingContent,
    revision: computeGroundingRevision(groundingContent),
  },
  userText: "May I enter?",
  triggers: [],
};

describe("AI NPC contract V4", () => {
  it("accepts exact grounded content, empty triggers, and same-version additions", () => {
    const parsed = aiNpcRequestSchema.safeParse({
      ...validRequest,
      futureField: "ignored",
    });

    expect(parsed.success).toBe(true);
    expect(validRequest.grounding.revision).toBe(
      "ctx-0fbb1fef8071da13b9476369537500347025c3762df5df65449f89b5275022bc",
    );
  });

  it("rejects stale revisions, noncanonical text, duplicates, and fact overflow", () => {
    expect(aiNpcRequestSchema.safeParse({
      ...validRequest,
      grounding: {
        ...validRequest.grounding,
        background: "Changed canon.",
      },
    }).success).toBe(false);
    expect(aiNpcRequestSchema.safeParse({
      ...validRequest,
      grounding: {
        ...validRequest.grounding,
        background: " padded ",
      },
    }).success).toBe(false);
    expect(aiNpcRequestSchema.safeParse({
      ...validRequest,
      grounding: {
        ...validRequest.grounding,
        facts: [groundingContent.facts[0], groundingContent.facts[0]],
      },
    }).success).toBe(false);
    expect(aiNpcRequestSchema.safeParse({
      ...validRequest,
      grounding: {
        ...validRequest.grounding,
        facts: Array.from({ length: MAX_FACT_COUNT + 1 }, (_, index) => ({
          factId: `fact_${index}`,
          kind: "lore",
          statement: "Bounded fact.",
          priority: 1,
        })),
      },
    }).success).toBe(false);
  });

  it("computes the same revision regardless of incoming fact order", () => {
    expect(computeGroundingRevision({
      ...groundingContent,
      facts: [...groundingContent.facts].reverse(),
    })).toBe(validRequest.grounding.revision);
  });

  it("creates exclusive success and reset responses with zero matched IDs", () => {
    const success = createSuccessResponse(
      validRequest.requestId,
      {
        dialogue: "Guard: The gate remains closed.",
        emotion: "neutral",
        gesture: "nod",
        matchedTriggerIds: [],
      },
      [],
    );

    expect(aiNpcResponseSchema.safeParse(success).success).toBe(true);
    expect(success).not.toHaveProperty("error");
    expect(createResetSuccessResponse("req-reset")).toMatchObject({
      schemaVersion: 4,
      result: { reset: true },
    });
  });

  it("rejects mixed branches, unknown tokens, and matched IDs outside the request", () => {
    expect(aiNpcResponseSchema.safeParse({
      schemaVersion: 4,
      requestId: "req-v4-invalid",
      status: "success",
      result: {
        dialogue: "Guard: Halt.",
        emotion: "excited",
        gesture: "nod",
        matchedTriggerIds: [],
      },
      error: {
        code: "invalid_request",
        message: "Invalid.",
        retryable: false,
      },
    }).success).toBe(false);
    expect(() => createSuccessResponse(
      "req-v4-unknown-trigger",
      {
        dialogue: "Guard: Halt.",
        emotion: "neutral",
        gesture: "none",
        matchedTriggerIds: ["invented_trigger"],
      },
      ["known_trigger"],
    )).toThrow("unknown trigger ID");
  });
});
