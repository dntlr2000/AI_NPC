import { Buffer } from "node:buffer";
import { z } from "zod";
import {
  aiNpcErrorSchema,
  aiNpcResponsePayloadSchema,
  characterSnapshotSchema,
} from "./v1.js";

export const SCHEMA_VERSION = 2 as const;
export const SUCCESS_STATUS = "success" as const;
export const ERROR_STATUS = "error" as const;
export const MAX_SESSION_ID_LENGTH = 128;
export const MAX_USER_TEXT_UTF8_BYTES = 8 * 1024;

const nonBlankStringSchema = z
  .string()
  .refine((value) => value.trim().length > 0, "Value must not be blank.");

const sessionIdSchema = nonBlankStringSchema.max(MAX_SESSION_ID_LENGTH);

const userTextSchema = nonBlankStringSchema.refine(
  (value) => Buffer.byteLength(value, "utf8") <= MAX_USER_TEXT_UTF8_BYTES,
  `Value must not exceed ${MAX_USER_TEXT_UTF8_BYTES} UTF-8 bytes.`,
);

export const aiNpcRequestSchema = z
  .object({
    schemaVersion: z.literal(SCHEMA_VERSION),
    requestId: nonBlankStringSchema,
    sessionId: sessionIdSchema,
    character: characterSnapshotSchema,
    userText: userTextSchema,
  })
  .passthrough();

const successResponseSchema = z
  .object({
    schemaVersion: z.literal(SCHEMA_VERSION),
    requestId: nonBlankStringSchema,
    status: z.literal(SUCCESS_STATUS),
    result: aiNpcResponsePayloadSchema,
    error: z.null().optional(),
  })
  .passthrough();

const errorResponseSchema = z
  .object({
    schemaVersion: z.literal(SCHEMA_VERSION),
    requestId: nonBlankStringSchema,
    status: z.literal(ERROR_STATUS),
    result: z.null().optional(),
    error: aiNpcErrorSchema,
  })
  .passthrough();

export const aiNpcResponseSchema = z.union([
  successResponseSchema,
  errorResponseSchema,
]);

export const aiNpcSessionResetRequestSchema = z
  .object({
    schemaVersion: z.literal(SCHEMA_VERSION),
    requestId: nonBlankStringSchema,
    sessionId: sessionIdSchema,
    characterId: nonBlankStringSchema,
  })
  .passthrough();

const resetSuccessResponseSchema = z
  .object({
    schemaVersion: z.literal(SCHEMA_VERSION),
    requestId: nonBlankStringSchema,
    status: z.literal(SUCCESS_STATUS),
    result: z.object({ reset: z.literal(true) }).passthrough(),
    error: z.null().optional(),
  })
  .passthrough();

export const aiNpcSessionResetResponseSchema = z.union([
  resetSuccessResponseSchema,
  errorResponseSchema,
]);

export type AiNpcRequest = z.infer<typeof aiNpcRequestSchema>;
export type AiNpcResponse = z.infer<typeof aiNpcResponseSchema>;
export type AiNpcSessionResetRequest = z.infer<
  typeof aiNpcSessionResetRequestSchema
>;
export type AiNpcSessionResetResponse = z.infer<
  typeof aiNpcSessionResetResponseSchema
>;

/** Creates a canonical V2 conversation success envelope. */
export function createSuccessResponse(
  requestId: string,
  result: z.infer<typeof aiNpcResponsePayloadSchema>,
): AiNpcResponse {
  return aiNpcResponseSchema.parse({
    schemaVersion: SCHEMA_VERSION,
    requestId,
    status: SUCCESS_STATUS,
    result,
  });
}

/** Creates a canonical V2 conversation error envelope. */
export function createErrorResponse(
  requestId: string,
  code: string,
  message: string,
  retryable: boolean,
): AiNpcResponse {
  return aiNpcResponseSchema.parse({
    schemaVersion: SCHEMA_VERSION,
    requestId,
    status: ERROR_STATUS,
    error: { code, message, retryable },
  });
}

/** Creates a canonical acknowledgement for an idempotent reset. */
export function createResetSuccessResponse(
  requestId: string,
): AiNpcSessionResetResponse {
  return aiNpcSessionResetResponseSchema.parse({
    schemaVersion: SCHEMA_VERSION,
    requestId,
    status: SUCCESS_STATUS,
    result: { reset: true },
  });
}

/** Creates a canonical V2 reset error envelope. */
export function createResetErrorResponse(
  requestId: string,
  code: string,
  message: string,
  retryable: boolean,
): AiNpcSessionResetResponse {
  return aiNpcSessionResetResponseSchema.parse({
    schemaVersion: SCHEMA_VERSION,
    requestId,
    status: ERROR_STATUS,
    error: { code, message, retryable },
  });
}

/** Extracts a usable correlation ID without trusting the full V2 body. */
export function readRequestId(value: unknown, fallback: string): string {
  if (typeof value !== "object" || value === null) {
    return fallback;
  }

  const requestId = Reflect.get(value, "requestId");
  return typeof requestId === "string" && requestId.trim().length > 0
    ? requestId
    : fallback;
}
