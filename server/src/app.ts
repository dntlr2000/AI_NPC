import Fastify, {
  type FastifyInstance,
  type FastifyServerOptions,
} from "fastify";
import {
  aiNpcRequestSchema as aiNpcRequestSchemaV1,
  createErrorResponse as createErrorResponseV1,
  createSuccessResponse as createSuccessResponseV1,
  readRequestId as readRequestIdV1,
  SCHEMA_VERSION as SCHEMA_VERSION_V1,
} from "./contracts/v1.js";
import {
  aiNpcRequestSchema as aiNpcRequestSchemaV2,
  aiNpcSessionResetRequestSchema,
  createErrorResponse as createErrorResponseV2,
  createResetErrorResponse,
  createResetSuccessResponse,
  createSuccessResponse as createSuccessResponseV2,
  readRequestId as readRequestIdV2,
  SCHEMA_VERSION as SCHEMA_VERSION_V2,
} from "./contracts/v2.js";
import {
  createSpeechErrorResponse,
  readSpeechRequestId,
  SPEECH_AUDIO_FORMAT,
  SPEECH_AUDIO_FORMAT_HEADER,
  SPEECH_CHANNELS,
  SPEECH_CHANNELS_HEADER,
  SPEECH_CONTENT_TYPE,
  SPEECH_REQUEST_ID_HEADER,
  SPEECH_SAMPLE_RATE,
  SPEECH_SAMPLE_RATE_HEADER,
  SPEECH_SCHEMA_VERSION,
  SPEECH_VERSION_HEADER,
  speechSynthesisRequestSchema,
} from "./contracts/speech-v1.js";
import { REQUEST_BODY_LIMIT_BYTES } from "./config.js";
import { NpcServiceError } from "./errors.js";
import type { NpcResponseGenerator } from "./generator.js";
import type { SessionConversationService } from "./sessions.js";
import type { SpeechGenerator } from "./speech.js";
import type { VoicePresetResolver } from "./voice-presets.js";

export interface AppDependencies {
  readonly generator: NpcResponseGenerator;
  readonly sessionService: SessionConversationService;
  readonly speechGenerator: SpeechGenerator;
  readonly voicePresetResolver: VoicePresetResolver;
  readonly logger?: FastifyServerOptions["logger"];
}

/** Creates a local-only HTTP application around injected stateless and session services. */
export function createApp(dependencies: AppDependencies): FastifyInstance {
  const app = Fastify({
    bodyLimit: REQUEST_BODY_LIMIT_BYTES,
    logger: dependencies.logger ?? createLoggerOptions(),
  });

  app.get("/healthz", async () => ({ status: "ok" }));

  app.post("/v1/npc/respond", async (request, reply) => {
    const requestId = readRequestIdV1(request.body, request.id);
    const version = readSchemaVersion(request.body);
    if (version !== undefined && version !== SCHEMA_VERSION_V1) {
      return reply.status(400).send(
        createErrorResponseV1(
          requestId,
          "unsupported_schema_version",
          "Only AI NPC contract version 1 is supported on this endpoint.",
          false,
        ),
      );
    }

    const parsedRequest = aiNpcRequestSchemaV1.safeParse(request.body);
    if (!parsedRequest.success) {
      return reply.status(400).send(
        createErrorResponseV1(
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
        {
          character: parsedRequest.data.character,
          history: [],
          userText: parsedRequest.data.userText,
        },
        cancellation.signal,
      );
      logGenerationSuccess(request.log, parsedRequest.data.requestId, generated);
      return reply.status(200).send(
        createSuccessResponseV1(parsedRequest.data.requestId, generated.result),
      );
    } catch (error: unknown) {
      const serviceError = normalizeServiceError(error);
      logServiceFailure(request.log, parsedRequest.data.requestId, serviceError);
      return reply.status(serviceError.statusCode).send(
        createErrorResponseV1(
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

  app.post("/v2/npc/respond", async (request, reply) => {
    const requestId = readRequestIdV2(request.body, request.id);
    const version = readSchemaVersion(request.body);
    if (version !== undefined && version !== SCHEMA_VERSION_V2) {
      return reply.status(400).send(
        createErrorResponseV2(
          requestId,
          "unsupported_schema_version",
          "Only AI NPC contract version 2 is supported on this endpoint.",
          false,
        ),
      );
    }

    const parsedRequest = aiNpcRequestSchemaV2.safeParse(request.body);
    if (!parsedRequest.success) {
      return reply.status(400).send(
        createErrorResponseV2(
          requestId,
          "invalid_request",
          "The session-aware AI NPC request is invalid.",
          false,
        ),
      );
    }

    const cancellation = createRequestCancellation(request.raw, reply.raw);
    try {
      const generated = await dependencies.sessionService.respond(
        parsedRequest.data,
        cancellation.signal,
      );
      logGenerationSuccess(request.log, parsedRequest.data.requestId, generated);
      return reply.status(200).send(
        createSuccessResponseV2(parsedRequest.data.requestId, generated.result),
      );
    } catch (error: unknown) {
      const serviceError = normalizeServiceError(error);
      logServiceFailure(request.log, parsedRequest.data.requestId, serviceError);
      return reply.status(serviceError.statusCode).send(
        createErrorResponseV2(
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

  app.post("/v2/npc/sessions/reset", async (request, reply) => {
    const requestId = readRequestIdV2(request.body, request.id);
    const version = readSchemaVersion(request.body);
    if (version !== undefined && version !== SCHEMA_VERSION_V2) {
      return reply.status(400).send(
        createResetErrorResponse(
          requestId,
          "unsupported_schema_version",
          "Only AI NPC contract version 2 is supported on this endpoint.",
          false,
        ),
      );
    }

    const parsedRequest = aiNpcSessionResetRequestSchema.safeParse(request.body);
    if (!parsedRequest.success) {
      return reply.status(400).send(
        createResetErrorResponse(
          requestId,
          "invalid_request",
          "The session reset request is invalid.",
          false,
        ),
      );
    }

    try {
      dependencies.sessionService.reset(parsedRequest.data);
      request.log.info(
        { contractRequestId: parsedRequest.data.requestId },
        "AI NPC session reset",
      );
      return reply.status(200).send(
        createResetSuccessResponse(parsedRequest.data.requestId),
      );
    } catch (error: unknown) {
      const serviceError = normalizeServiceError(error);
      logServiceFailure(request.log, parsedRequest.data.requestId, serviceError);
      return reply.status(serviceError.statusCode).send(
        createResetErrorResponse(
          parsedRequest.data.requestId,
          serviceError.code,
          serviceError.message,
          serviceError.retryable,
        ),
      );
    }
  });

  app.post("/v1/speech/synthesize", async (request, reply) => {
    const requestId = readSpeechRequestId(request.body, request.id);
    const version = readSchemaVersion(request.body);
    if (version !== undefined && version !== SPEECH_SCHEMA_VERSION) {
      return reply.status(400).send(
        createSpeechErrorResponse(
          requestId,
          "unsupported_schema_version",
          "Only speech contract version 1 is supported on this endpoint.",
          false,
        ),
      );
    }

    const parsedRequest = speechSynthesisRequestSchema.safeParse(request.body);
    if (!parsedRequest.success) {
      return reply.status(400).send(
        createSpeechErrorResponse(
          requestId,
          "invalid_request",
          "The speech synthesis request is invalid.",
          false,
        ),
      );
    }

    const preset = dependencies.voicePresetResolver.resolve(
      parsedRequest.data.voicePresetId,
    );
    if (preset === undefined) {
      return reply.status(400).send(
        createSpeechErrorResponse(
          parsedRequest.data.requestId,
          "voice_preset_not_found",
          "The requested voice preset is not configured.",
          false,
        ),
      );
    }

    const cancellation = createRequestCancellation(request.raw, reply.raw);
    const startedAt = Date.now();
    try {
      const generated = await dependencies.speechGenerator.generate(
        { text: parsedRequest.data.text, preset },
        cancellation.signal,
      );
      logSpeechSuccess(
        request.log,
        parsedRequest.data.requestId,
        generated.pcmAudio.byteLength,
        Date.now() - startedAt,
      );
      return reply
        .header(SPEECH_VERSION_HEADER, String(SPEECH_SCHEMA_VERSION))
        .header(SPEECH_REQUEST_ID_HEADER, parsedRequest.data.requestId)
        .header(SPEECH_AUDIO_FORMAT_HEADER, SPEECH_AUDIO_FORMAT)
        .header(SPEECH_SAMPLE_RATE_HEADER, String(SPEECH_SAMPLE_RATE))
        .header(SPEECH_CHANNELS_HEADER, String(SPEECH_CHANNELS))
        .type(SPEECH_CONTENT_TYPE)
        .status(200)
        .send(generated.pcmAudio);
    } catch (error: unknown) {
      const serviceError = normalizeSpeechServiceError(error);
      logServiceFailure(request.log, parsedRequest.data.requestId, serviceError);
      return reply.status(serviceError.statusCode).send(
        createSpeechErrorResponse(
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

    if (request.url.startsWith("/v1/speech/")) {
      return reply.status(statusCode).send(
        createSpeechErrorResponse(request.id, code, message, false),
      );
    }

    if (request.url.startsWith("/v2/npc/sessions/reset")) {
      return reply.status(statusCode).send(
        createResetErrorResponse(request.id, code, message, false),
      );
    }

    if (request.url.startsWith("/v2/")) {
      return reply.status(statusCode).send(
        createErrorResponseV2(request.id, code, message, false),
      );
    }

    return reply.status(statusCode).send(
      createErrorResponseV1(request.id, code, message, false),
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

/** Logs model identifiers and usage without storing conversation or session content. */
function logGenerationSuccess(
  logger: { info: (data: object, message: string) => void },
  requestId: string,
  generated: Awaited<ReturnType<NpcResponseGenerator["generate"]>>,
): void {
  logger.info(
    {
      contractRequestId: requestId,
      openAiResponseId: generated.telemetry.openAiResponseId,
      inputTokens: generated.telemetry.inputTokens,
      outputTokens: generated.telemetry.outputTokens,
      totalTokens: generated.telemetry.totalTokens,
    },
    "AI NPC response generated",
  );
}

/** Logs only correlation, size, and latency for successful speech generation. */
function logSpeechSuccess(
  logger: { info: (data: object, message: string) => void },
  requestId: string,
  audioBytes: number,
  elapsedMilliseconds: number,
): void {
  logger.info(
    { contractRequestId: requestId, audioBytes, elapsedMilliseconds },
    "AI NPC speech generated",
  );
}

/** Logs one safe failure category without request, profile, or session content. */
function logServiceFailure(
  logger: { warn: (data: object, message: string) => void },
  requestId: string,
  serviceError: NpcServiceError,
): void {
  logger.warn(
    {
      contractRequestId: requestId,
      category: serviceError.logCategory,
      statusCode: serviceError.statusCode,
      retryable: serviceError.retryable,
    },
    "AI NPC request failed",
  );
}

/** Reads a numeric schema version early so unsupported versions get a stable code. */
function readSchemaVersion(value: unknown): number | undefined {
  if (typeof value !== "object" || value === null) {
    return undefined;
  }

  const schemaVersion = Reflect.get(value, "schemaVersion");
  return typeof schemaVersion === "number" ? schemaVersion : undefined;
}

/** Links a disconnected HTTP request to the upstream cancellation signal. */
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

/** Converts an unexpected speech route failure into one safe non-retryable error. */
function normalizeSpeechServiceError(error: unknown): NpcServiceError {
  if (error instanceof NpcServiceError) {
    return error;
  }

  return new NpcServiceError(
    "internal_error",
    "The backend could not complete the speech request.",
    500,
    false,
    "backend_unexpected_speech_error",
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
