import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import {
  aiNpcRequestSchema,
  aiNpcResponseSchema,
  createErrorResponse,
  createSuccessResponse,
} from "../src/contracts/v1.js";

const fixturesRoot = new URL(
  "../../Assets/AiCharacterKit/Tests/EditMode/Fixtures/Transport/V1/",
  import.meta.url,
);

/** Reads one shared Phase 3 golden fixture as UTF-8 JSON. */
function readFixture(fileName: string): unknown {
  const json = readFileSync(new URL(fileName, fixturesRoot), "utf8");
  return JSON.parse(json) as unknown;
}

describe("AI NPC contract V1", () => {
  it("accepts the shared valid request fixture", () => {
    const parsed = aiNpcRequestSchema.parse(readFixture("valid-request.json"));
    expect(parsed.requestId).toBe("req-001");
    expect(parsed.character.characterId).toBe("sample-luna");
  });

  it("accepts the shared success and error response fixtures", () => {
    const success = aiNpcResponseSchema.parse(
      readFixture("valid-success-response.json"),
    );
    const error = aiNpcResponseSchema.parse(
      readFixture("valid-error-response.json"),
    );

    expect(success.status).toBe("success");
    expect(error.status).toBe("error");
  });

  it("allows additive V1 request fields", () => {
    const parsed = aiNpcRequestSchema.parse(
      readFixture("request-with-extra-field.json"),
    );
    expect(parsed.schemaVersion).toBe(1);
  });

  it("rejects missing fields, unsupported versions, and unknown commands", () => {
    expect(
      aiNpcRequestSchema.safeParse(
        readFixture("missing-character-request.json"),
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
  });

  it("creates exclusive canonical success and error branches", () => {
    const success = createSuccessResponse("req-success", {
      dialogue: "Hello.",
      emotion: "happy",
      gesture: "wave",
    });
    const error = createErrorResponse(
      "req-error",
      "rate_limited",
      "Try again later.",
      true,
    );

    expect(success).not.toHaveProperty("error");
    expect(error).not.toHaveProperty("result");
  });
});
