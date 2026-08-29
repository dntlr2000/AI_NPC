import { z } from "zod";

export const SCHEMA_VERSION = 1 as const;
export const SUCCESS_STATUS = "success" as const;
export const ERROR_STATUS = "error" as const;

export const emotionSchema = z.enum([
  "neutral",
  "happy",
  "sad",
  "angry",
  "concerned",
]);

export const gestureSchema = z.enum(["none", "nod", "wave"]);

const nonBlankStringSchema = z
  .string()
  .refine((value) => value.trim().length > 0, "Value must not be blank.");

const errorCodeSchema = z
  .string()
  .regex(/^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$/);

export const characterSnapshotSchema = z
  .object({
    characterId: nonBlankStringSchema,
    displayName: nonBlankStringSchema,
    personality: nonBlankStringSchema,
    speechStyle: nonBlankStringSchema,
    exampleDialogue: nonBlankStringSchema,
    defaultEmotion: emotionSchema,
  })
  .passthrough();

export const aiNpcRequestSchema = z
  .object({
    schemaVersion: z.literal(SCHEMA_VERSION),
    requestId: nonBlankStringSchema,
    character: characterSnapshotSchema,
    userText: nonBlankStringSchema,
  })
  .passthrough();

export const aiNpcResponsePayloadSchema = z
  .object({
    dialogue: nonBlankStringSchema,
    emotion: emotionSchema,
    gesture: gestureSchema,
  })
  .passthrough();

export const aiNpcErrorSchema = z
  .object({
    code: errorCodeSchema,
    message: nonBlankStringSchema,
    retryable: z.boolean().default(false),
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

export const modelNpcResponseSchema = z
  .object({
    dialogue: nonBlankStringSchema.max(600),
    emotion: emotionSchema,
    gesture: gestureSchema,
  })
  .strict();

export type AiNpcRequest = z.infer<typeof aiNpcRequestSchema>;
export type AiNpcResponse = z.infer<typeof aiNpcResponseSchema>;
export type AiNpcResponsePayload = z.infer<typeof aiNpcResponsePayloadSchema>;
export type ModelNpcResponse = z.infer<typeof modelNpcResponseSchema>;

/** Creates a canonical V1 success envelope owned by the backend. */
export function createSuccessResponse(
  requestId: string,
  result: AiNpcResponsePayload,
): AiNpcResponse {
  return aiNpcResponseSchema.parse({
    schemaVersion: SCHEMA_VERSION,
    requestId,
    status: SUCCESS_STATUS,
    result,
  });
}

/** Creates a canonical V1 error envelope with no inactive result branch. */
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
    error: {
      code,
      message,
      retryable,
    },
  });
}

/** Extracts a usable correlation ID without trusting the full request body. */
export function readRequestId(value: unknown, fallback: string): string {
  if (typeof value !== "object" || value === null) {
    return fallback;
  }

  const requestId = Reflect.get(value, "requestId");
  return typeof requestId === "string" && requestId.trim().length > 0
    ? requestId
    : fallback;
}
