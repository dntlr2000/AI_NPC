import { Buffer } from "node:buffer";
import { createHash } from "node:crypto";
import { z } from "zod";
import {
  aiNpcErrorSchema,
  characterSnapshotSchema,
  emotionSchema,
  gestureSchema,
} from "./v1.js";

export const SCHEMA_VERSION = 4 as const;
export const SUCCESS_STATUS = "success" as const;
export const ERROR_STATUS = "error" as const;
export const MAX_SESSION_ID_LENGTH = 128;
export const MAX_USER_TEXT_UTF8_BYTES = 8 * 1024;
export const MAX_TRIGGER_COUNT = 16;
export const MAX_TRIGGER_ID_LENGTH = 64;
export const MAX_TRIGGER_CONDITION_UTF8_BYTES = 512;
export const MAX_BACKGROUND_UTF8_BYTES = 2048;
export const MAX_GOALS_AND_VALUES_UTF8_BYTES = 2048;
export const MAX_BEHAVIORAL_RULE_COUNT = 16;
export const MAX_BEHAVIORAL_RULE_UTF8_BYTES = 512;
export const MAX_DIALOGUE_EXAMPLE_COUNT = 8;
export const MAX_DIALOGUE_EXAMPLE_UTF8_BYTES = 1024;
export const MAX_FACT_COUNT = 32;
export const MAX_FACT_STATEMENT_UTF8_BYTES = 512;
export const MAX_TOTAL_FACT_UTF8_BYTES = 12 * 1024;

const identifierSchema = z
  .string()
  .max(MAX_TRIGGER_ID_LENGTH)
  .regex(/^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$/);

const nonBlankStringSchema = z
  .string()
  .refine((value) => value.trim().length > 0, "Value must not be blank.");

/** Normalizes line endings and surrounding whitespace for cross-runtime revisions. */
export function normalizeGroundingText(value: string): string {
  return value.replace(/\r\n?/g, "\n").trim();
}

/** Creates one canonical bounded optional grounding text schema. */
function canonicalOptionalTextSchema(maxUtf8Bytes: number) {
  return z.string()
    .refine(
      (value) => Buffer.byteLength(value, "utf8") <= maxUtf8Bytes,
      `Value must not exceed ${maxUtf8Bytes} UTF-8 bytes.`,
    )
    .refine(
      (value) => value === normalizeGroundingText(value),
      "Value must use canonical whitespace and line endings.",
    );
}

/** Creates one canonical non-empty bounded grounding text schema. */
function canonicalRequiredTextSchema(maxUtf8Bytes: number) {
  return nonBlankStringSchema
    .refine(
      (value) => Buffer.byteLength(value, "utf8") <= maxUtf8Bytes,
      `Value must not exceed ${maxUtf8Bytes} UTF-8 bytes.`,
    )
    .refine(
      (value) => value === normalizeGroundingText(value),
      "Value must use canonical whitespace and line endings.",
    );
}

export const contextFactKindSchema = z.enum([
  "lore",
  "belief",
  "observation",
]);

export const contextFactSchema = z
  .object({
    factId: identifierSchema,
    kind: contextFactKindSchema,
    statement: canonicalRequiredTextSchema(MAX_FACT_STATEMENT_UTF8_BYTES),
    priority: z.number().int().min(0).max(100),
  })
  .passthrough();

const groundingContentSchema = z
  .object({
    revision: z.string().regex(/^ctx-[0-9a-f]{64}$/),
    background: canonicalOptionalTextSchema(MAX_BACKGROUND_UTF8_BYTES),
    goalsAndValues: canonicalOptionalTextSchema(
      MAX_GOALS_AND_VALUES_UTF8_BYTES,
    ),
    behavioralRules: z
      .array(canonicalRequiredTextSchema(MAX_BEHAVIORAL_RULE_UTF8_BYTES))
      .max(MAX_BEHAVIORAL_RULE_COUNT),
    dialogueExamples: z
      .array(canonicalRequiredTextSchema(MAX_DIALOGUE_EXAMPLE_UTF8_BYTES))
      .max(MAX_DIALOGUE_EXAMPLE_COUNT),
    facts: z.array(contextFactSchema).max(MAX_FACT_COUNT),
  })
  .passthrough();

export const groundingSnapshotSchema = groundingContentSchema.superRefine(
  (grounding, context) => {
    const ids = new Set(grounding.facts.map((fact) => fact.factId));
    if (ids.size !== grounding.facts.length) {
      context.addIssue({
        code: "custom",
        path: ["facts"],
        message: "Fact IDs must be unique.",
      });
    }

    const totalBytes = grounding.facts.reduce(
      (sum, fact) => sum + Buffer.byteLength(fact.statement, "utf8"),
      0,
    );
    if (totalBytes > MAX_TOTAL_FACT_UTF8_BYTES) {
      context.addIssue({
        code: "custom",
        path: ["facts"],
        message: `Fact statements must not exceed ${MAX_TOTAL_FACT_UTF8_BYTES} UTF-8 bytes in total.`,
      });
    }

    if (computeGroundingRevision(grounding) !== grounding.revision) {
      context.addIssue({
        code: "custom",
        path: ["revision"],
        message: "Revision must match the normalized grounding content.",
      });
    }
  },
);

export const semanticTriggerSchema = z
  .object({
    triggerId: identifierSchema,
    conditionDescription: nonBlankStringSchema.refine(
      (value) => Buffer.byteLength(value, "utf8")
        <= MAX_TRIGGER_CONDITION_UTF8_BYTES,
      `Value must not exceed ${MAX_TRIGGER_CONDITION_UTF8_BYTES} UTF-8 bytes.`,
    ),
  })
  .passthrough();

const triggerSnapshotSchema = z
  .array(semanticTriggerSchema)
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
    grounding: groundingSnapshotSchema,
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
      .array(identifierSchema)
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

export type GroundingSnapshot = z.infer<typeof groundingSnapshotSchema>;
export type SemanticTrigger = z.infer<typeof semanticTriggerSchema>;
export type AiNpcRequest = z.infer<typeof aiNpcRequestSchema>;
export type AiNpcResponse = z.infer<typeof aiNpcResponseSchema>;
export type AiNpcSessionResetRequest = z.infer<
  typeof aiNpcSessionResetRequestSchema
>;
export type AiNpcSessionResetResponse = z.infer<
  typeof aiNpcSessionResetResponseSchema
>;

/** Computes the exact SHA-256 revision shared with the Unity V4 mapper. */
export function computeGroundingRevision(
  grounding: Pick<
    GroundingSnapshot,
    | "background"
    | "goalsAndValues"
    | "behavioralRules"
    | "dialogueExamples"
    | "facts"
  >,
): string {
  let canonical = "";
  const append = (value: string): void => {
    canonical += `${value.length}:${value}|`;
  };
  const appendValues = (values: readonly string[]): void => {
    append(values.length.toString());
    for (const value of values) {
      append(value);
    }
  };

  append(grounding.background);
  append(grounding.goalsAndValues);
  appendValues(grounding.behavioralRules);
  appendValues(grounding.dialogueExamples);
  const facts = [...grounding.facts].sort(
    (left, right) => right.priority - left.priority
      || (left.factId < right.factId ? -1 : left.factId > right.factId ? 1 : 0),
  );
  const kindValues = { lore: "0", belief: "1", observation: "2" } as const;
  for (const fact of facts) {
    append(fact.factId);
    append(kindValues[fact.kind]);
    append(fact.statement);
    append(fact.priority.toString());
  }

  return `ctx-${createHash("sha256").update(canonical, "utf8").digest("hex")}`;
}

/** Creates a canonical V4 conversation success after checking the request subset. */
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

/** Creates a canonical V4 conversation error envelope. */
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

/** Creates a canonical V4 idempotent reset acknowledgement. */
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

/** Creates a canonical V4 reset error envelope. */
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

/** Extracts a usable correlation ID without trusting the full V4 body. */
export function readRequestId(value: unknown, fallback: string): string {
  if (typeof value !== "object" || value === null) {
    return fallback;
  }

  const requestId = Reflect.get(value, "requestId");
  return typeof requestId === "string" && requestId.trim().length > 0
    ? requestId
    : fallback;
}
