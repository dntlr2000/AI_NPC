import Fastify, {
  type FastifyInstance,
  type FastifyServerOptions,
} from "fastify";
import {
  aiNpcRequestSchema,
  createErrorResponse,
  createSuccessResponse,
  readRequestId,
  SCHEMA_VERSION,
} from "./contracts/v1.js";
import { REQUEST_BODY_LIMIT_BYTES } from "./config.js";
import { NpcServiceError } from "./errors.js";
import type { NpcResponseGenerator } from "./generator.js";

export interface AppDependencies {
  readonly generator: NpcResponseGenerator;
  readonly logger?: FastifyServerOptions["logger"];
}

/** Creates a local-only HTTP application around an injected NPC response generator. */
export function createApp(dependencies: AppDependencies): FastifyInstance {
  const app = Fastify({
    bodyLimit: REQUEST_BODY_LIMIT_BYTES,
    logger: dependencies.logger ?? createLoggerOptions(),
  });

  app.get("/healthz", async () => ({ status: "ok" }));

  app.post("/v1/npc/respond", async (request, reply) => {
    const requestId = readRequestId(request.body, request.id);
    const version = readSchemaVersion(request.body);
    if (version !== undefined && version !== SCHEMA_VERSION) {
      return reply.status(400).send(
        createErrorResponse(
          requestId,
          "unsupported_schema_version",
          "Only AI NPC contract version 1 is supported.",
          false,
        ),
      );
    }

    const parsedRequest = aiNpcRequestSchema.safeParse(request.body);
    if (!parsedRequest.success) {
      return reply.status(400).send(
        createErrorResponse(
          requestId,
          "invalid_request",
          "The AI NPC request is invalid.",
          false,
        ),
      );
    }

    const cancellation = createRequestCancellation(request.raw, reply.raw);
    try {
      const generated = await dependencies.generator.generate(
        parsedRequest.data,
        cancellation.signal,
      );
      request.log.info(
        {
          contractRequestId: parsedRequest.data.requestId,
          openAiResponseId: generated.telemetry.openAiResponseId,
          inputTokens: generated.telemetry.inputTokens,
          outputTokens: generated.telemetry.outputTokens,
          totalTokens: generated.telemetry.totalTokens,
        },
        "AI NPC response generated",
      );
      return reply.status(200).send(
        createSuccessResponse(parsedRequest.data.requestId, generated.result),
      );
    } catch (error: unknown) {
      const serviceError = normalizeServiceError(error);
      request.log.warn(
        {
          contractRequestId: parsedRequest.data.requestId,
          category: serviceError.logCategory,
          statusCode: serviceError.statusCode,
          retryable: serviceError.retryable,
        },
        "AI NPC request failed",
      );
      return reply.status(serviceError.statusCode).send(
        createErrorResponse(
          parsedRequest.data.requestId,
          serviceError.code,
          serviceError.message,
          serviceError.retryable,
        ),
      );
    } finally {
      cancellation.dispose();
    }
  });

  app.setErrorHandler((error, request, reply) => {
    const isBodyTooLarge =
      readUnknownString(error, "code") === "FST_ERR_CTP_BODY_TOO_LARGE";
    const statusCode = isBodyTooLarge
      ? 413
      : readUnknownNumber(error, "statusCode") ?? 500;
    const isClientError = statusCode >= 400 && statusCode < 500;
    const code = isClientError ? "invalid_request" : "internal_error";
    const message = isClientError
      ? "The AI NPC request body is invalid."
      : "The backend could not process the request.";

    request.log.warn(
      {
        category: isClientError ? "http_invalid_body" : "http_internal_error",
        statusCode,
      },
      "AI NPC HTTP request rejected",
    );
    return reply.status(statusCode).send(
      createErrorResponse(request.id, code, message, false),
    );
  });

  return app;
}

/** Produces body-safe structured logging with common secret headers redacted. */
function createLoggerOptions(): FastifyServerOptions["logger"] {
  return {
    level: "info",
    redact: [
      "req.headers.authorization",
      "req.headers.x-api-key",
      "headers.authorization",
      "headers.x-api-key",
    ],
  };
}

/** Reads a numeric schema version early so unsupported versions get a stable code. */
function readSchemaVersion(value: unknown): number | undefined {
  if (typeof value !== "object" || value === null) {
    return undefined;
  }

  const schemaVersion = Reflect.get(value, "schemaVersion");
  return typeof schemaVersion === "number" ? schemaVersion : undefined;
}

/** Links a disconnected HTTP request to the upstream OpenAI cancellation signal. */
function createRequestCancellation(
  request: NodeJS.EventEmitter,
  response: NodeJS.EventEmitter & { writableEnded?: boolean },
): { signal: AbortSignal; dispose: () => void } {
  const controller = new AbortController();
  const abortRequest = (): void => controller.abort();
  const abortUnfinishedResponse = (): void => {
    if (!response.writableEnded) {
      controller.abort();
    }
  };

  request.once("aborted", abortRequest);
  response.once("close", abortUnfinishedResponse);
  return {
    signal: controller.signal,
    dispose: () => {
      request.removeListener("aborted", abortRequest);
      response.removeListener("close", abortUnfinishedResponse);
    },
  };
}

/** Converts unexpected route failures into one non-retryable safe backend error. */
function normalizeServiceError(error: unknown): NpcServiceError {
  if (error instanceof NpcServiceError) {
    return error;
  }

  return new NpcServiceError(
    "internal_error",
    "The backend could not complete the conversation request.",
    500,
    false,
    "backend_unexpected_error",
    { cause: error },
  );
}

/** Reads one string property from an unknown Fastify or parser error. */
function readUnknownString(
  value: unknown,
  propertyName: string,
): string | undefined {
  if (typeof value !== "object" || value === null) {
    return undefined;
  }

  const property = Reflect.get(value, propertyName);
  return typeof property === "string" ? property : undefined;
}

/** Reads one finite numeric property from an unknown Fastify or parser error. */
function readUnknownNumber(
  value: unknown,
  propertyName: string,
): number | undefined {
  if (typeof value !== "object" || value === null) {
    return undefined;
  }

  const property = Reflect.get(value, propertyName);
  return typeof property === "number" && Number.isFinite(property)
    ? property
    : undefined;
}
