import { describe, expect, it } from "vitest";
import {
  aiNpcRequestSchema,
  aiNpcResponseSchema,
  aiNpcSessionResetRequestSchema,
  createErrorResponse,
  createResetSuccessResponse,
  createSuccessResponse,
  MAX_TRIGGER_COUNT,
} from "../src/contracts/v3.js";

const validRequest = {
  schemaVersion: 3,
  requestId: "req-v3-contract",
  sessionId: "session-v3-contract",
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

describe("AI NPC contract V3", () => {
  it("accepts a bounded trigger request and ignores same-version additions", () => {
    const parsed = aiNpcRequestSchema.safeParse({
      ...validRequest,
      futureField: "ignored",
    });

    expect(parsed.success).toBe(true);
  });

  it("rejects missing, duplicate, malformed, and excessive trigger snapshots", () => {
    expect(aiNpcRequestSchema.safeParse({
      ...validRequest,
      triggers: undefined,
    }).success).toBe(false);
    expect(aiNpcRequestSchema.safeParse({
      ...validRequest,
      triggers: [validRequest.triggers[0], validRequest.triggers[0]],
    }).success).toBe(false);
    expect(aiNpcRequestSchema.safeParse({
      ...validRequest,
      triggers: [{ triggerId: "Open Gate", conditionDescription: "Open." }],
    }).success).toBe(false);
    expect(aiNpcRequestSchema.safeParse({
      ...validRequest,
      triggers: Array.from({ length: MAX_TRIGGER_COUNT + 1 }, (_, index) => ({
        triggerId: `trigger_${index}`,
        conditionDescription: "Match.",
      })),
    }).success).toBe(false);
  });

  it("creates exclusive success and error branches with matched IDs", () => {
    const success = createSuccessResponse(
      validRequest.requestId,
      {
        dialogue: "Guide: I will check.",
        emotion: "happy",
        gesture: "nod",
        matchedTriggerIds: ["open_gate"],
      },
      ["open_gate"],
    );
    const error = createErrorResponse(
      validRequest.requestId,
      "session_busy",
      "Busy.",
      true,
    );

    expect(aiNpcResponseSchema.safeParse(success).success).toBe(true);
    expect(success).not.toHaveProperty("error");
    expect(error).not.toHaveProperty("result");
  });

  it("rejects unknown matched IDs and invalid reset fields", () => {
    expect(() => createSuccessResponse(
      validRequest.requestId,
      {
        dialogue: "No.",
        emotion: "neutral",
        gesture: "none",
        matchedTriggerIds: ["invented_trigger"],
      },
      ["open_gate"],
    )).toThrow();
    expect(aiNpcSessionResetRequestSchema.safeParse({
      schemaVersion: 3,
      requestId: "reset",
      sessionId: "",
      characterId: "sample-guide",
    }).success).toBe(false);
    expect(createResetSuccessResponse("reset")).toMatchObject({
      schemaVersion: 3,
      status: "success",
      result: { reset: true },
    });
  });

  it("rejects unsupported versions, statuses, duplicate IDs, and mixed branches", () => {
    expect(aiNpcRequestSchema.safeParse({
      ...validRequest,
      schemaVersion: 2,
    }).success).toBe(false);
    expect(aiNpcResponseSchema.safeParse({
      schemaVersion: 3,
      requestId: "req-invalid-status",
      status: "partial",
      result: {
        dialogue: "No.",
        emotion: "neutral",
        gesture: "none",
        matchedTriggerIds: [],
      },
    }).success).toBe(false);
    expect(aiNpcResponseSchema.safeParse({
      schemaVersion: 3,
      requestId: "req-duplicate",
      status: "success",
      result: {
        dialogue: "Hello.",
        emotion: "neutral",
        gesture: "none",
        matchedTriggerIds: ["open_gate", "open_gate"],
      },
    }).success).toBe(false);
    expect(aiNpcResponseSchema.safeParse({
      schemaVersion: 3,
      requestId: "req-mixed",
      status: "success",
      result: {
        dialogue: "Hello.",
        emotion: "neutral",
        gesture: "none",
        matchedTriggerIds: [],
      },
      error: {
        code: "internal_error",
        message: "Mixed branch.",
        retryable: false,
      },
    }).success).toBe(false);
  });
});
