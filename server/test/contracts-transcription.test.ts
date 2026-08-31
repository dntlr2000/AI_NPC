import { describe, expect, it } from "vitest";
import {
  createTranscriptionErrorResponse,
  createTranscriptionSuccessResponse,
  readTranscriptionRequestId,
  transcriptionResponseSchema,
} from "../src/contracts/transcription-v1.js";

describe("Transcription V1 contract", () => {
  it("creates valid correlated success and error branches", () => {
    expect(createTranscriptionSuccessResponse(
      "transcription-001",
      "안녕하세요.",
    )).toEqual({
      schemaVersion: 1,
      requestId: "transcription-001",
      status: "success",
      result: { text: "안녕하세요." },
    });
    expect(createTranscriptionErrorResponse(
      "transcription-002",
      "invalid_audio",
      "Invalid audio.",
      false,
    )).toMatchObject({
      schemaVersion: 1,
      requestId: "transcription-002",
      status: "error",
      error: { code: "invalid_audio", retryable: false },
    });
  });

  it("rejects unknown versions, statuses, branch overlap, and empty text", () => {
    expect(transcriptionResponseSchema.safeParse({
      schemaVersion: 2,
      requestId: "x",
      status: "success",
      result: { text: "ok" },
    }).success).toBe(false);
    expect(transcriptionResponseSchema.safeParse({
      schemaVersion: 1,
      requestId: "x",
      status: "pending",
      result: { text: "ok" },
    }).success).toBe(false);
    expect(transcriptionResponseSchema.safeParse({
      schemaVersion: 1,
      requestId: "x",
      status: "success",
      result: { text: "ok" },
      error: { code: "bad", message: "bad", retryable: false },
    }).success).toBe(false);
    expect(transcriptionResponseSchema.safeParse({
      schemaVersion: 1,
      requestId: "x",
      status: "success",
      result: { text: " " },
    }).success).toBe(false);
  });

  it("allows unknown V1 fields and safely falls back for invalid IDs", () => {
    expect(transcriptionResponseSchema.safeParse({
      schemaVersion: 1,
      requestId: "x",
      status: "success",
      result: { text: "ok", future: true },
      futureEnvelope: 1,
    }).success).toBe(true);
    expect(readTranscriptionRequestId("valid-id", "fallback"))
      .toBe("valid-id");
    expect(readTranscriptionRequestId(" ", "fallback"))
      .toBe("fallback");
  });
});
