import OpenAI from "openai";
import { MAX_SPEECH_AUDIO_BYTES } from "./contracts/speech-v1.js";
import { NpcServiceError } from "./errors.js";
import type { VoicePreset } from "./voice-presets.js";

export interface SpeechGenerationRequest {
  readonly text: string;
  readonly preset: VoicePreset;
}

export interface SpeechGenerationResult {
  readonly pcmAudio: Buffer;
}

export interface SpeechGenerator {
  /** Generates one complete normalized PCM result from a resolved voice preset. */
  generate(
    request: SpeechGenerationRequest,
    cancellationSignal: AbortSignal,
  ): Promise<SpeechGenerationResult>;
}

export interface OpenAiSpeechGeneratorOptions {
  readonly apiKey: string;
  readonly model: string;
  readonly timeoutMs: number;
}

/** Generates fixed-format PCM through the server-owned OpenAI Speech API client. */
export class OpenAiSpeechGenerator implements SpeechGenerator {
  private readonly client: OpenAI;
  private readonly model: string;

  /** Creates one reusable client with SDK retries explicitly disabled. */
  public constructor(options: OpenAiSpeechGeneratorOptions, client?: OpenAI) {
    this.model = options.model;
    this.client = client ?? new OpenAI({
      apiKey: options.apiKey,
      maxRetries: 0,
      timeout: options.timeoutMs,
    });
  }

  /** Requests provider audio and validates the complete PCM buffer before returning it. */
  public async generate(
    request: SpeechGenerationRequest,
    cancellationSignal: AbortSignal,
  ): Promise<SpeechGenerationResult> {
    try {
      const response = await this.client.audio.speech.create(
        {
          model: this.model,
          voice: request.preset.voice,
          input: request.text,
          instructions: request.preset.instructions,
          speed: request.preset.speed,
          response_format: "pcm",
          stream_format: "audio",
        },
        { signal: cancellationSignal },
      );
      const pcmAudio = Buffer.from(await response.arrayBuffer());
      validatePcmAudio(pcmAudio);
      return { pcmAudio };
    } catch (error: unknown) {
      throw mapOpenAiSpeechError(error, cancellationSignal);
    }
  }
}

/** Rejects empty, partial-sample, and oversized provider audio. */
function validatePcmAudio(pcmAudio: Buffer): void {
  if (pcmAudio.byteLength === 0
      || pcmAudio.byteLength % 2 !== 0
      || pcmAudio.byteLength > MAX_SPEECH_AUDIO_BYTES) {
    throw new NpcServiceError(
      "upstream_invalid_response",
      "The speech service returned invalid audio.",
      502,
      true,
      "openai_invalid_speech_audio",
    );
  }
}

/** Converts provider and transport failures into safe speech contract errors. */
function mapOpenAiSpeechError(
  error: unknown,
  cancellationSignal: AbortSignal,
): NpcServiceError {
  if (error instanceof NpcServiceError) {
    return error;
  }

  if (cancellationSignal.aborted) {
    return new NpcServiceError(
      "upstream_unavailable",
      "The speech request was cancelled before completion.",
      502,
      true,
      "openai_speech_cancelled",
      { cause: error },
    );
  }

  const status = readNumericProperty(error, "status");
  const errorName = error instanceof Error ? error.name.toLowerCase() : "";
  if (status === 429) {
    return new NpcServiceError(
      "rate_limited",
      "The speech service is temporarily rate limited.",
      429,
      true,
      "openai_speech_rate_limit",
      { cause: error },
    );
  }

  if (errorName.includes("timeout")) {
    return new NpcServiceError(
      "upstream_timeout",
      "The speech service did not respond before the timeout.",
      504,
      true,
      "openai_speech_timeout",
      { cause: error },
    );
  }

  if (status !== undefined && status >= 500) {
    return new NpcServiceError(
      "upstream_unavailable",
      "The speech service is temporarily unavailable.",
      502,
      true,
      "openai_speech_server_error",
      { cause: error },
    );
  }

  if (errorName.includes("connection")) {
    return new NpcServiceError(
      "upstream_unavailable",
      "The speech service could not be reached.",
      502,
      true,
      "openai_speech_connection_error",
      { cause: error },
    );
  }

  return new NpcServiceError(
    "internal_error",
    "The backend could not complete the speech request.",
    500,
    false,
    "openai_speech_configuration_or_unknown_error",
    { cause: error },
  );
}

/** Reads one finite numeric property without trusting an arbitrary thrown value. */
function readNumericProperty(
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
