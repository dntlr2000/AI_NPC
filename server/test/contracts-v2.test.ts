import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import {
  aiNpcRequestSchema,
  aiNpcResponseSchema,
  aiNpcSessionResetRequestSchema,
  aiNpcSessionResetResponseSchema,
  createErrorResponse,
  createResetErrorResponse,
  createResetSuccessResponse,
  createSuccessResponse,
} from "../src/contracts/v2.js";

const fixturesRoot = new URL(
  "../../Assets/AiCharacterKit/Tests/EditMode/Fixtures/Transport/V2/",
  import.meta.url,
);

/** Reads one shared Phase 5 golden fixture as UTF-8 JSON. */
function readFixture(fileName: string): unknown {
  return JSON.parse(
    readFileSync(new URL(fileName, fixturesRoot), "utf8"),
  ) as unknown;
}

describe("AI NPC contract V2", () => {
  it("accepts shared conversation and reset golden fixtures", () => {
    const request = aiNpcRequestSchema.parse(readFixture("valid-request.json"));
    const success = aiNpcResponseSchema.parse(
      readFixture("valid-success-response.json"),
    );
    const error = aiNpcResponseSchema.parse(
      readFixture("valid-error-response.json"),
    );
    const resetRequest = aiNpcSessionResetRequestSchema.parse(
      readFixture("valid-reset-request.json"),
    );
    const resetSuccess = aiNpcSessionResetResponseSchema.parse(
      readFixture("valid-reset-success-response.json"),
    );
    const resetError = aiNpcSessionResetResponseSchema.parse(
      readFixture("valid-reset-error-response.json"),
    );

    expect(request.sessionId).toBe("session-001");
    expect(success.status).toBe("success");
    expect(error.status).toBe("error");
    expect(resetRequest.characterId).toBe("sample-luna");
    expect(resetSuccess.status).toBe("success");
    expect(resetError.status).toBe("error");
  });

  it("allows additive V2 fields", () => {
    const parsed = aiNpcRequestSchema.parse(
      readFixture("request-with-extra-field.json"),
    );
    expect(parsed.requestId).toBe("req-v2-extra");
  });

  it("rejects missing sessions, unsupported versions, and unknown commands", () => {
    expect(
      aiNpcRequestSchema.safeParse(
        readFixture("missing-session-request.json"),
      ).success,
    ).toBe(false);
    expect(
      aiNpcRequestSchema.safeParse(
        readFixture("unsupported-version-request.json"),
      ).success,
    ).toBe(false);
    expect(
      aiNpcResponseSchema.safeParse(
        readFixture("unknown-emotion-response.json"),
      ).success,
    ).toBe(false);
    expect(
      aiNpcSessionResetResponseSchema.safeParse(
        readFixture("invalid-reset-result.json"),
      ).success,
    ).toBe(false);
  });

  it("rejects session and user text values beyond V2 limits", () => {
    const valid = readFixture("valid-request.json") as Record<string, unknown>;
    expect(
      aiNpcRequestSchema.safeParse({ ...valid, sessionId: "s".repeat(129) })
        .success,
    ).toBe(false);
    expect(
      aiNpcRequestSchema.safeParse({ ...valid, userText: "한".repeat(2_731) })
        .success,
    ).toBe(false);
  });

  it("creates canonical exclusive conversation and reset branches", () => {
    const success = createSuccessResponse("req-success", {
      dialogue: "Hello.",
      emotion: "happy",
      gesture: "wave",
    });
    const error = createErrorResponse(
      "req-error",
      "session_busy",
      "Busy.",
      true,
    );
    const resetSuccess = createResetSuccessResponse("req-reset-success");
    const resetError = createResetErrorResponse(
      "req-reset-error",
      "session_character_mismatch",
      "Mismatch.",
      false,
    );

    expect(success).not.toHaveProperty("error");
    expect(error).not.toHaveProperty("result");
    expect(resetSuccess).not.toHaveProperty("error");
    expect(resetError).not.toHaveProperty("result");
  });
});
