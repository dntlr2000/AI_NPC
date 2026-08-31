import { Buffer } from "node:buffer";
import OpenAI, { toFile } from "openai";
import {
  MAX_TRANSCRIPTION_AUDIO_BYTES,
  MAX_TRANSCRIPTION_DURATION_SECONDS,
  MAX_TRANSCRIPTION_SAMPLE_RATE,
  MAX_TRANSCRIPTION_TEXT_LENGTH,
  MAX_TRANSCRIPTION_TEXT_UTF8_BYTES,
  MIN_TRANSCRIPTION_SAMPLE_RATE,
} from "./contracts/transcription-v1.js";
import { NpcServiceError } from "./errors.js";

const CANONICAL_WAVE_HEADER_BYTES = 44;

export interface ValidatedTranscriptionAudio {
  readonly waveAudio: Buffer;
  readonly sampleRate: number;
  readonly sampleFrames: number;
  readonly durationMilliseconds: number;
}

export interface TranscriptionGenerationResult {
  readonly text: string;
}

export interface TranscriptionGenerator {
  /** Generates one transcript from a complete validated WAV payload. */
  generate(
    audio: ValidatedTranscriptionAudio,
    cancellationSignal: AbortSignal,
  ): Promise<TranscriptionGenerationResult>;
}

export interface OpenAiTranscriptionGeneratorOptions {
  readonly apiKey: string;
  readonly model: string;
  readonly timeoutMs: number;
}

/** Generates bounded text through the server-owned OpenAI Transcription API client. */
export class OpenAiTranscriptionGenerator implements TranscriptionGenerator {
  private readonly client: OpenAI;
  private readonly model: string;

  /** Creates one reusable client with SDK retries explicitly disabled. */
  public constructor(
    options: OpenAiTranscriptionGeneratorOptions,
    client?: OpenAI,
  ) {
    this.model = options.model;
    this.client = client ?? new OpenAI({
      apiKey: options.apiKey,
      maxRetries: 0,
      timeout: options.timeoutMs,
    });
  }

  /** Uploads one in-memory WAV and validates the complete returned transcript. */
  public async generate(
    audio: ValidatedTranscriptionAudio,
    cancellationSignal: AbortSignal,
  ): Promise<TranscriptionGenerationResult> {
    try {
      const file = await toFile(audio.waveAudio, "input.wav", {
        type: "audio/wav",
      });
      const response = await this.client.audio.transcriptions.create(
        {
          file,
          model: this.model,
          response_format: "json",
        },
        { signal: cancellationSignal },
      );
      const text = typeof response.text === "string" ? response.text : "";
      validateTranscript(text);
      return { text };
    } catch (error: unknown) {
      throw mapOpenAiTranscriptionError(error, cancellationSignal);
    }
  }
}

/** Validates one canonical PCM16 mono WAV and returns safe metadata for processing. */
export function validateCanonicalPcm16Wave(
  value: unknown,
): ValidatedTranscriptionAudio {
  if (!Buffer.isBuffer(value)
      || value.byteLength < CANONICAL_WAVE_HEADER_BYTES
      || value.byteLength > MAX_TRANSCRIPTION_AUDIO_BYTES
      || value.toString("ascii", 0, 4) !== "RIFF"
      || value.toString("ascii", 8, 12) !== "WAVE"
      || value.toString("ascii", 12, 16) !== "fmt "
      || value.readUInt32LE(16) !== 16
      || value.readUInt16LE(20) !== 1
      || value.readUInt16LE(22) !== 1
      || value.readUInt16LE(32) !== 2
      || value.readUInt16LE(34) !== 16
      || value.toString("ascii", 36, 40) !== "data") {
    throw createInvalidAudioError();
  }

  const riffByteCount = value.readUInt32LE(4);
  const sampleRate = value.readUInt32LE(24);
  const byteRate = value.readUInt32LE(28);
  const dataByteCount = value.readUInt32LE(40);
  if (riffByteCount !== value.byteLength - 8
      || dataByteCount !== value.byteLength - CANONICAL_WAVE_HEADER_BYTES
      || dataByteCount === 0
      || dataByteCount % 2 !== 0
      || sampleRate < MIN_TRANSCRIPTION_SAMPLE_RATE
      || sampleRate > MAX_TRANSCRIPTION_SAMPLE_RATE
      || byteRate !== sampleRate * 2) {
    throw createInvalidAudioError();
  }

  const sampleFrames = dataByteCount / 2;
  if (sampleFrames > sampleRate * MAX_TRANSCRIPTION_DURATION_SECONDS) {
    throw new NpcServiceError(
      "audio_too_long",
      "The recorded audio exceeds the 15 second limit.",
      400,
      false,
      "transcription_audio_too_long",
    );
  }

  return {
    waveAudio: value,
    sampleRate,
    sampleFrames,
    durationMilliseconds: Math.ceil(sampleFrames * 1_000 / sampleRate),
  };
}

/** Rejects empty or oversized provider text before it reaches the wire contract. */
function validateTranscript(text: string): void {
  if (text.trim().length === 0
      || text.length > MAX_TRANSCRIPTION_TEXT_LENGTH
      || Buffer.byteLength(text, "utf8") > MAX_TRANSCRIPTION_TEXT_UTF8_BYTES) {
    throw new NpcServiceError(
      "upstream_invalid_response",
      "The transcription service returned invalid text.",
      502,
      true,
      "openai_invalid_transcription_text",
    );
  }
}

/** Creates one stable client error for malformed or unsupported WAV data. */
function createInvalidAudioError(): NpcServiceError {
  return new NpcServiceError(
    "invalid_audio",
    "The request must contain canonical PCM16 mono WAV audio.",
    400,
    false,
    "transcription_invalid_audio",
  );
}

/** Converts provider and transport failures into safe transcription errors. */
function mapOpenAiTranscriptionError(
  error: unknown,
  cancellationSignal: AbortSignal,
): NpcServiceError {
  if (error instanceof NpcServiceError) {
    return error;
  }

  if (cancellationSignal.aborted) {
    return new NpcServiceError(
      "upstream_unavailable",
      "The transcription request was cancelled before completion.",
      502,
      true,
      "openai_transcription_cancelled",
      { cause: error },
    );
  }

  const status = readNumericProperty(error, "status");
  const errorName = error instanceof Error ? error.name.toLowerCase() : "";
  if (status === 429) {
    return new NpcServiceError(
      "rate_limited",
      "The transcription service is temporarily rate limited.",
      429,
      true,
      "openai_transcription_rate_limit",
      { cause: error },
    );
  }

  if (errorName.includes("timeout")) {
    return new NpcServiceError(
      "upstream_timeout",
      "The transcription service did not respond before the timeout.",
      504,
      true,
      "openai_transcription_timeout",
      { cause: error },
    );
  }

  if (status !== undefined && status >= 500) {
    return new NpcServiceError(
      "upstream_unavailable",
      "The transcription service is temporarily unavailable.",
      502,
      true,
      "openai_transcription_server_error",
      { cause: error },
    );
  }

  if (errorName.includes("connection")) {
    return new NpcServiceError(
      "upstream_unavailable",
      "The transcription service could not be reached.",
      502,
      true,
      "openai_transcription_connection_error",
      { cause: error },
    );
  }

  return new NpcServiceError(
    "internal_error",
    "The backend could not complete the transcription request.",
    500,
    false,
    "openai_transcription_configuration_or_unknown_error",
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
