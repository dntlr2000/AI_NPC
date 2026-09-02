import { Buffer } from "node:buffer";
import { z } from "zod";
import {
  aiNpcErrorSchema,
  characterSnapshotSchema,
  emotionSchema,
  gestureSchema,
} from "./v1.js";

export const SCHEMA_VERSION = 3 as const;
export const SUCCESS_STATUS = "success" as const;
export const ERROR_STATUS = "error" as const;
export const MAX_SESSION_ID_LENGTH = 128;
export const MAX_USER_TEXT_UTF8_BYTES = 8 * 1024;
export const MAX_TRIGGER_COUNT = 16;
export const MAX_TRIGGER_ID_LENGTH = 64;
export const MAX_TRIGGER_CONDITION_UTF8_BYTES = 512;

const nonBlankStringSchema = z
  .string()
  .refine((value) => value.trim().length > 0, "Value must not be blank.");

export const triggerIdSchema = z
  .string()
  .max(MAX_TRIGGER_ID_LENGTH)
  .regex(/^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$/);

export const semanticTriggerSchema = z
  .object({
    triggerId: triggerIdSchema,
    conditionDescription: nonBlankStringSchema.refine(
      (value) => Buffer.byteLength(value, "utf8")
        <= MAX_TRIGGER_CONDITION_UTF8_BYTES,
      `Value must not exceed ${MAX_TRIGGER_CONDITION_UTF8_BYTES} UTF-8 bytes.`,
    ),
  })
  .passthrough();

const triggerSnapshotSchema = z
  .array(semanticTriggerSchema)
  .min(1)
  .max(MAX_TRIGGER_COUNT)
  .refine(
    (triggers) => new Set(triggers.map((trigger) => trigger.triggerId)).size
      === triggers.length,
    "Trigger IDs must be unique.",
  );

export const aiNpcRequestSchema = z
  .object({
    schemaVersion: z.literal(SCHEMA_VERSION),
    requestId: nonBlankStringSchema,
    sessionId: nonBlankStringSchema.max(MAX_SESSION_ID_LENGTH),
    character: characterSnapshotSchema,
    userText: nonBlankStringSchema.refine(
      (value) => Buffer.byteLength(value, "utf8") <= MAX_USER_TEXT_UTF8_BYTES,
      `Value must not exceed ${MAX_USER_TEXT_UTF8_BYTES} UTF-8 bytes.`,
    ),
    triggers: triggerSnapshotSchema,
  })
  .passthrough();

export const aiNpcResponsePayloadSchema = z
  .object({
    dialogue: nonBlankStringSchema,
    emotion: emotionSchema,
    gesture: gestureSchema,
    matchedTriggerIds: z
      .array(triggerIdSchema)
      .max(MAX_TRIGGER_COUNT)
      .refine(
        (ids) => new Set(ids).size === ids.length,
        "Matched trigger IDs must be unique.",
      ),
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
    sessionId: nonBlankStringSchema.max(MAX_SESSION_ID_LENGTH),
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
export type SemanticTrigger = z.infer<typeof semanticTriggerSchema>;

/** Creates a canonical V3 conversation success after checking the request subset. */
export function createSuccessResponse(
  requestId: string,
  result: z.infer<typeof aiNpcResponsePayloadSchema>,
  configuredTriggerIds: readonly string[],
): AiNpcResponse {
  const knownIds = new Set(configuredTriggerIds);
  if (result.matchedTriggerIds.some((id) => !knownIds.has(id))) {
    throw new Error("A response matched an unknown trigger ID.");
  }

  return aiNpcResponseSchema.parse({
    schemaVersion: SCHEMA_VERSION,
    requestId,
    status: SUCCESS_STATUS,
    result,
  });
}

/** Creates a canonical V3 conversation error envelope. */
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

/** Creates a canonical V3 idempotent reset acknowledgement. */
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

/** Creates a canonical V3 reset error envelope. */
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

/** Extracts a usable correlation ID without trusting the full V3 body. */
export function readRequestId(value: unknown, fallback: string): string {
  if (typeof value !== "object" || value === null) {
    return fallback;
  }

  const requestId = Reflect.get(value, "requestId");
  return typeof requestId === "string" && requestId.trim().length > 0
    ? requestId
    : fallback;
}
