import { Buffer } from "node:buffer";
import { z } from "zod";

export const TRANSCRIPTION_SCHEMA_VERSION = 1 as const;
export const TRANSCRIPTION_SUCCESS_STATUS = "success" as const;
export const TRANSCRIPTION_ERROR_STATUS = "error" as const;
export const TRANSCRIPTION_CONTENT_TYPE = "audio/wav";
export const TRANSCRIPTION_VERSION_HEADER =
  "X-Ai-Character-Kit-Transcription-Version";
export const TRANSCRIPTION_REQUEST_ID_HEADER =
  "X-Ai-Character-Kit-Request-Id";
export const MAX_TRANSCRIPTION_REQUEST_ID_LENGTH = 128;
export const MAX_TRANSCRIPTION_TEXT_LENGTH = 4_096;
export const MAX_TRANSCRIPTION_TEXT_UTF8_BYTES = 8 * 1_024;
export const MAX_TRANSCRIPTION_AUDIO_BYTES = 2 * 1_024 * 1_024;
export const MIN_TRANSCRIPTION_SAMPLE_RATE = 8_000;
export const MAX_TRANSCRIPTION_SAMPLE_RATE = 48_000;
export const MAX_TRANSCRIPTION_DURATION_SECONDS = 15;

const nonBlankStringSchema = z
  .string()
  .refine((value) => value.trim().length > 0, "Value must not be blank.");

const transcriptionTextSchema = nonBlankStringSchema
  .max(MAX_TRANSCRIPTION_TEXT_LENGTH)
  .refine(
    (value) => Buffer.byteLength(value, "utf8")
      <= MAX_TRANSCRIPTION_TEXT_UTF8_BYTES,
    "Transcription text exceeds the UTF-8 byte limit.",
  );

const errorCodeSchema = z
  .string()
  .regex(/^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$/);

export const transcriptionResultSchema = z
  .object({ text: transcriptionTextSchema })
  .passthrough();

export const transcriptionErrorSchema = z
  .object({
    code: errorCodeSchema,
    message: nonBlankStringSchema.max(MAX_TRANSCRIPTION_TEXT_LENGTH),
    retryable: z.boolean().default(false),
  })
  .passthrough();

export const transcriptionSuccessResponseSchema = z
  .object({
    schemaVersion: z.literal(TRANSCRIPTION_SCHEMA_VERSION),
    requestId: nonBlankStringSchema.max(MAX_TRANSCRIPTION_REQUEST_ID_LENGTH),
    status: z.literal(TRANSCRIPTION_SUCCESS_STATUS),
    result: transcriptionResultSchema,
    error: z.null().optional(),
  })
  .passthrough();

export const transcriptionErrorResponseSchema = z
  .object({
    schemaVersion: z.literal(TRANSCRIPTION_SCHEMA_VERSION),
    requestId: nonBlankStringSchema.max(MAX_TRANSCRIPTION_REQUEST_ID_LENGTH),
    status: z.literal(TRANSCRIPTION_ERROR_STATUS),
    result: z.null().optional(),
    error: transcriptionErrorSchema,
  })
  .passthrough();

export const transcriptionResponseSchema = z.discriminatedUnion("status", [
  transcriptionSuccessResponseSchema,
  transcriptionErrorResponseSchema,
]);

export type TranscriptionResponse = z.infer<
  typeof transcriptionResponseSchema
>;

/** Creates one canonical successful transcription response. */
export function createTranscriptionSuccessResponse(
  requestId: string,
  text: string,
): TranscriptionResponse {
  return transcriptionSuccessResponseSchema.parse({
    schemaVersion: TRANSCRIPTION_SCHEMA_VERSION,
    requestId,
    status: TRANSCRIPTION_SUCCESS_STATUS,
    result: { text },
  });
}

/** Creates one canonical safe transcription error response. */
export function createTranscriptionErrorResponse(
  requestId: string,
  code: string,
  message: string,
  retryable: boolean,
): TranscriptionResponse {
  return transcriptionErrorResponseSchema.parse({
    schemaVersion: TRANSCRIPTION_SCHEMA_VERSION,
    requestId,
    status: TRANSCRIPTION_ERROR_STATUS,
    error: { code, message, retryable },
  });
}

/** Extracts a bounded correlation header without trusting other request data. */
export function readTranscriptionRequestId(
  value: string | string[] | undefined,
  fallback: string,
): string {
  const requestId = Array.isArray(value) ? value[0] : value;
  return typeof requestId === "string"
    && requestId.trim().length > 0
    && requestId.length <= MAX_TRANSCRIPTION_REQUEST_ID_LENGTH
    ? requestId
    : fallback;
}
