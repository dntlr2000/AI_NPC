import { Buffer } from "node:buffer";
import { z } from "zod";

export const SPEECH_SCHEMA_VERSION = 1 as const;
export const SPEECH_ERROR_STATUS = "error" as const;
export const MAX_SPEECH_REQUEST_ID_LENGTH = 128;
export const MAX_VOICE_PRESET_ID_LENGTH = 64;
export const MAX_SPEECH_TEXT_LENGTH = 4_096;
export const MAX_SPEECH_TEXT_UTF8_BYTES = 8 * 1_024;
export const MAX_SPEECH_AUDIO_BYTES = 8 * 1_024 * 1_024;
export const SPEECH_CONTENT_TYPE = "application/octet-stream";
export const SPEECH_VERSION_HEADER = "X-Ai-Character-Kit-Speech-Version";
export const SPEECH_REQUEST_ID_HEADER = "X-Ai-Character-Kit-Request-Id";
export const SPEECH_AUDIO_FORMAT_HEADER = "X-Ai-Character-Kit-Audio-Format";
export const SPEECH_SAMPLE_RATE_HEADER = "X-Ai-Character-Kit-Sample-Rate";
export const SPEECH_CHANNELS_HEADER = "X-Ai-Character-Kit-Channels";
export const SPEECH_AUDIO_FORMAT = "pcm_s16le";
export const SPEECH_SAMPLE_RATE = 24_000;
export const SPEECH_CHANNELS = 1;

const nonBlankStringSchema = z
  .string()
  .refine((value) => value.trim().length > 0, "Value must not be blank.");

export const voicePresetIdSchema = nonBlankStringSchema
  .max(MAX_VOICE_PRESET_ID_LENGTH)
  .regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/);

const speechTextSchema = nonBlankStringSchema
  .max(MAX_SPEECH_TEXT_LENGTH)
  .refine(
    (value) => Buffer.byteLength(value, "utf8") <= MAX_SPEECH_TEXT_UTF8_BYTES,
    "Speech text exceeds the UTF-8 byte limit.",
  );

const errorCodeSchema = z
  .string()
  .regex(/^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$/);

export const speechSynthesisRequestSchema = z
  .object({
    schemaVersion: z.literal(SPEECH_SCHEMA_VERSION),
    requestId: nonBlankStringSchema.max(MAX_SPEECH_REQUEST_ID_LENGTH),
    voicePresetId: voicePresetIdSchema,
    text: speechTextSchema,
  })
  .passthrough();

export const speechErrorSchema = z
  .object({
    code: errorCodeSchema,
    message: nonBlankStringSchema.max(MAX_SPEECH_TEXT_LENGTH),
    retryable: z.boolean().default(false),
  })
  .passthrough();

export const speechErrorResponseSchema = z
  .object({
    schemaVersion: z.literal(SPEECH_SCHEMA_VERSION),
    requestId: nonBlankStringSchema.max(MAX_SPEECH_REQUEST_ID_LENGTH),
    status: z.literal(SPEECH_ERROR_STATUS),
    error: speechErrorSchema,
  })
  .passthrough();

export type SpeechSynthesisRequest = z.infer<
  typeof speechSynthesisRequestSchema
>;
export type SpeechErrorResponse = z.infer<typeof speechErrorResponseSchema>;

/** Creates one canonical JSON error for the binary speech endpoint. */
export function createSpeechErrorResponse(
  requestId: string,
  code: string,
  message: string,
  retryable: boolean,
): SpeechErrorResponse {
  return speechErrorResponseSchema.parse({
    schemaVersion: SPEECH_SCHEMA_VERSION,
    requestId,
    status: SPEECH_ERROR_STATUS,
    error: { code, message, retryable },
  });
}

/** Extracts a bounded correlation ID without trusting the complete request body. */
export function readSpeechRequestId(value: unknown, fallback: string): string {
  if (typeof value !== "object" || value === null) {
    return fallback;
  }

  const requestId = Reflect.get(value, "requestId");
  return typeof requestId === "string"
    && requestId.trim().length > 0
    && requestId.length <= MAX_SPEECH_REQUEST_ID_LENGTH
    ? requestId
    : fallback;
}
