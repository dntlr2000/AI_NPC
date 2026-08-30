import { Buffer } from "node:buffer";
import { describe, expect, it } from "vitest";
import {
  createSpeechErrorResponse,
  MAX_SPEECH_TEXT_UTF8_BYTES,
  readSpeechRequestId,
  SPEECH_SCHEMA_VERSION,
  speechErrorResponseSchema,
  speechSynthesisRequestSchema,
} from "../src/contracts/speech-v1.js";

const validRequest = {
  schemaVersion: SPEECH_SCHEMA_VERSION,
  requestId: "speech-contract-001",
  voicePresetId: "warm-friendly",
  text: "안녕하세요.",
};

describe("Speech contract V1", () => {
  it("accepts one complete request and ignores same-version additions", () => {
    const parsed = speechSynthesisRequestSchema.safeParse({
      ...validRequest,
      futureField: "ignored",
    });

    expect(parsed.success).toBe(true);
  });

  it("rejects missing, malformed, and unsupported request fields", () => {
    expect(speechSynthesisRequestSchema.safeParse({
      ...validRequest,
      schemaVersion: 2,
    }).success).toBe(false);
    expect(speechSynthesisRequestSchema.safeParse({
      ...validRequest,
      voicePresetId: "Warm_Friendly",
    }).success).toBe(false);
    expect(speechSynthesisRequestSchema.safeParse({
      ...validRequest,
      text: " ",
    }).success).toBe(false);
  });

  it("enforces the UTF-8 budget independently of string length", () => {
    const oversizedKorean = "가".repeat(
      Math.floor(MAX_SPEECH_TEXT_UTF8_BYTES / 3) + 1,
    );

    expect(Buffer.byteLength(oversizedKorean, "utf8"))
      .toBeGreaterThan(MAX_SPEECH_TEXT_UTF8_BYTES);
    expect(speechSynthesisRequestSchema.safeParse({
      ...validRequest,
      text: oversizedKorean,
    }).success).toBe(false);
  });

  it("creates only a correlated validated error branch", () => {
    const response = createSpeechErrorResponse(
      "speech-contract-002",
      "voice_preset_not_found",
      "Preset not found.",
      false,
    );

    expect(speechErrorResponseSchema.safeParse(response).success).toBe(true);
    expect(response).toEqual({
      schemaVersion: 1,
      requestId: "speech-contract-002",
      status: "error",
      error: {
        code: "voice_preset_not_found",
        message: "Preset not found.",
        retryable: false,
      },
    });
  });

  it("uses the fallback for untrusted or oversized request IDs", () => {
    expect(readSpeechRequestId(validRequest, "fallback"))
      .toBe("speech-contract-001");
    expect(readSpeechRequestId({ requestId: "x".repeat(129) }, "fallback"))
      .toBe("fallback");
    expect(readSpeechRequestId(null, "fallback")).toBe("fallback");
  });
});
