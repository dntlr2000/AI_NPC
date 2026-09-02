import type { SessionStoreOptions } from "./sessions.js";

export const DEFAULT_MODEL = "gpt-5.6-luna";
export const DEFAULT_PORT = 8787;
export const DEFAULT_OPENAI_TIMEOUT_MS = 30_000;
export const DEFAULT_TTS_MODEL = "gpt-4o-mini-tts";
export const DEFAULT_OPENAI_TTS_TIMEOUT_MS = 30_000;
export const DEFAULT_TRANSCRIPTION_MODEL = "gpt-transcribe";
export const DEFAULT_OPENAI_TRANSCRIPTION_TIMEOUT_MS = 30_000;
export const DEFAULT_TTS_VOICE_PRESETS_PATH = "config/voice-presets.json";
export const DEFAULT_SESSION_MAX_TURNS = 8;
export const DEFAULT_SESSION_MAX_CONTEXT_BYTES = 16 * 1024;
export const DEFAULT_SESSION_IDLE_TTL_SECONDS = 1_800;
export const DEFAULT_SESSION_MAX_COUNT = 128;
export const SERVER_HOST = "127.0.0.1";
export const REQUEST_BODY_LIMIT_BYTES = 32 * 1024;

export interface ServerConfig {
  readonly apiKey: string;
  readonly model: string;
  readonly port: number;
  readonly openAiTimeoutMs: number;
  readonly ttsModel: string;
  readonly openAiTtsTimeoutMs: number;
  readonly transcriptionModel: string;
  readonly openAiTranscriptionTimeoutMs: number;
  readonly ttsVoicePresetsPath: string;
  readonly sessionOptions: SessionStoreOptions;
}

/** Loads validated local server settings without reading or writing secret files. */
export function loadServerConfig(
  environment: NodeJS.ProcessEnv = process.env,
): ServerConfig {
  const apiKey = environment.OPENAI_API_KEY?.trim() ?? "";
  if (apiKey.length === 0) {
    throw new Error("OPENAI_API_KEY must be set in the server process environment.");
  }

  const model = environment.OPENAI_MODEL?.trim() || DEFAULT_MODEL;
  const port = readInteger(
    environment.PORT,
    DEFAULT_PORT,
    1,
    65_535,
    "PORT",
  );
  const openAiTimeoutMs = readInteger(
    environment.OPENAI_TIMEOUT_MS,
    DEFAULT_OPENAI_TIMEOUT_MS,
    1_000,
    120_000,
    "OPENAI_TIMEOUT_MS",
  );
  const ttsModel = environment.OPENAI_TTS_MODEL?.trim() || DEFAULT_TTS_MODEL;
  const openAiTtsTimeoutMs = readInteger(
    environment.OPENAI_TTS_TIMEOUT_MS,
    DEFAULT_OPENAI_TTS_TIMEOUT_MS,
    1_000,
    120_000,
    "OPENAI_TTS_TIMEOUT_MS",
  );
  const transcriptionModel =
    environment.OPENAI_TRANSCRIPTION_MODEL?.trim()
    || DEFAULT_TRANSCRIPTION_MODEL;
  const openAiTranscriptionTimeoutMs = readInteger(
    environment.OPENAI_TRANSCRIPTION_TIMEOUT_MS,
    DEFAULT_OPENAI_TRANSCRIPTION_TIMEOUT_MS,
    1_000,
    120_000,
    "OPENAI_TRANSCRIPTION_TIMEOUT_MS",
  );
  const ttsVoicePresetsPath =
    environment.NPC_TTS_VOICE_PRESETS_PATH?.trim()
    || DEFAULT_TTS_VOICE_PRESETS_PATH;
  const sessionMaxTurns = readInteger(
    environment.NPC_SESSION_MAX_TURNS,
    DEFAULT_SESSION_MAX_TURNS,
    1,
    32,
    "NPC_SESSION_MAX_TURNS",
  );
  const sessionMaxContextBytes = readInteger(
    environment.NPC_SESSION_MAX_CONTEXT_BYTES,
    DEFAULT_SESSION_MAX_CONTEXT_BYTES,
    4 * 1024,
    128 * 1024,
    "NPC_SESSION_MAX_CONTEXT_BYTES",
  );
  const sessionIdleTtlSeconds = readInteger(
    environment.NPC_SESSION_IDLE_TTL_SECONDS,
    DEFAULT_SESSION_IDLE_TTL_SECONDS,
    60,
    86_400,
    "NPC_SESSION_IDLE_TTL_SECONDS",
  );
  const sessionMaxCount = readInteger(
    environment.NPC_SESSION_MAX_COUNT,
    DEFAULT_SESSION_MAX_COUNT,
    1,
    4_096,
    "NPC_SESSION_MAX_COUNT",
  );

  return {
    apiKey,
    model,
    port,
    openAiTimeoutMs,
    ttsModel,
    openAiTtsTimeoutMs,
    transcriptionModel,
    openAiTranscriptionTimeoutMs,
    ttsVoicePresetsPath,
    sessionOptions: {
      maxTurns: sessionMaxTurns,
      maxContextBytes: sessionMaxContextBytes,
      idleTtlMs: sessionIdleTtlSeconds * 1_000,
      maxSessions: sessionMaxCount,
    },
  };
}

/** Parses one bounded integer environment value or returns its documented default. */
function readInteger(
  rawValue: string | undefined,
  defaultValue: number,
  minimum: number,
  maximum: number,
  variableName: string,
): number {
  if (rawValue === undefined || rawValue.trim().length === 0) {
    return defaultValue;
  }

  const value = Number(rawValue);
  if (!Number.isInteger(value) || value < minimum || value > maximum) {
    throw new Error(
      `${variableName} must be an integer from ${minimum} through ${maximum}.`,
    );
  }

  return value;
}
