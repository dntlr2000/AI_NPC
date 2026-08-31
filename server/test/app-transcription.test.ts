import type { FastifyInstance } from "fastify";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { createApp } from "../src/app.js";
import { NpcServiceError } from "../src/errors.js";
import type {
  NpcGenerationRequest,
  NpcGenerationResult,
  NpcResponseGenerator,
} from "../src/generator.js";
import {
  InMemoryConversationSessionStore,
  SessionConversationService,
} from "../src/sessions.js";
import type {
  SpeechGenerationRequest,
  SpeechGenerationResult,
  SpeechGenerator,
} from "../src/speech.js";
import type {
  TranscriptionGenerationResult,
  TranscriptionGenerator,
  ValidatedTranscriptionAudio,
} from "../src/transcription.js";
import { JsonVoicePresetResolver } from "../src/voice-presets.js";
import { createCanonicalWave } from "./wav-fixture.js";

/** Supplies the unrelated conversation dependency without invoking OpenAI. */
class StubNpcGenerator implements NpcResponseGenerator {
  /** Returns one fixed response if an unrelated conversation route is called. */
  public async generate(
    _request: NpcGenerationRequest,
  ): Promise<NpcGenerationResult> {
    return {
      result: { dialogue: "ok", emotion: "neutral", gesture: "none" },
      telemetry: {
        openAiResponseId: "unused",
        inputTokens: 0,
        outputTokens: 0,
        totalTokens: 0,
      },
    };
  }
}

/** Supplies the unrelated speech dependency without invoking OpenAI. */
class StubSpeechGenerator implements SpeechGenerator {
  /** Returns one complete PCM sample if an unrelated speech route is called. */
  public async generate(
    _request: SpeechGenerationRequest,
  ): Promise<SpeechGenerationResult> {
    return { pcmAudio: Buffer.from([0, 0]) };
  }
}

/** Supplies deterministic transcript text or one configured safe failure. */
class StubTranscriptionGenerator implements TranscriptionGenerator {
  public error: Error | null = null;
  public lastAudio: ValidatedTranscriptionAudio | null = null;

  /** Returns a fixed transcript after recording the validated audio metadata. */
  public async generate(
    audio: ValidatedTranscriptionAudio,
  ): Promise<TranscriptionGenerationResult> {
    this.lastAudio = audio;
    if (this.error !== null) {
      throw this.error;
    }

    return { text: "전사된 안녕하세요." };
  }
}

describe("Transcription V1 Fastify route", () => {
  let app: FastifyInstance;
  let transcriptionGenerator: StubTranscriptionGenerator;

  beforeEach(() => {
    const generator = new StubNpcGenerator();
    transcriptionGenerator = new StubTranscriptionGenerator();
    app = createApp({
      generator,
      sessionService: new SessionConversationService(
        new InMemoryConversationSessionStore({
          maxTurns: 8,
          maxContextBytes: 16 * 1024,
          idleTtlMs: 30 * 60 * 1_000,
          maxSessions: 128,
        }),
        generator,
      ),
      speechGenerator: new StubSpeechGenerator(),
      voicePresetResolver: new JsonVoicePresetResolver([{
        id: "unused-voice",
        voice: "marin",
        instructions: "Unused by transcription tests.",
        speed: 1,
      }]),
      transcriptionGenerator,
      logger: false,
    });
  });

  afterEach(async () => {
    await app.close();
  });

  it("returns one correlated transcript for valid canonical WAV audio", async () => {
    const wave = createCanonicalWave(8_000, 16_000);
    const response = await app.inject({
      method: "POST",
      url: "/v1/speech/transcribe",
      headers: {
        "content-type": "audio/wav",
        "x-ai-character-kit-transcription-version": "1",
        "x-ai-character-kit-request-id": "transcription-route-001",
      },
      payload: wave,
    });

    expect(response.statusCode).toBe(200);
    expect(response.json()).toEqual({
      schemaVersion: 1,
      requestId: "transcription-route-001",
      status: "success",
      result: { text: "전사된 안녕하세요." },
    });
    expect(transcriptionGenerator.lastAudio).toMatchObject({
      sampleRate: 16_000,
      sampleFrames: 8_000,
      durationMilliseconds: 500,
    });
  });

  it("rejects missing or unsupported contract version before generation", async () => {
    const missing = await app.inject({
      method: "POST",
      url: "/v1/speech/transcribe",
      headers: {
        "content-type": "audio/wav",
        "x-ai-character-kit-request-id": "transcription-route-002",
      },
      payload: createCanonicalWave(),
    });
    const unsupported = await app.inject({
      method: "POST",
      url: "/v1/speech/transcribe",
      headers: {
        "content-type": "audio/wav",
        "x-ai-character-kit-transcription-version": "2",
        "x-ai-character-kit-request-id": "transcription-route-003",
      },
      payload: createCanonicalWave(),
    });

    expect(missing.statusCode).toBe(400);
    expect(missing.json()).toMatchObject({
      requestId: "transcription-route-002",
      error: { code: "invalid_request" },
    });
    expect(unsupported.statusCode).toBe(400);
    expect(unsupported.json()).toMatchObject({
      requestId: "transcription-route-003",
      error: { code: "unsupported_schema_version" },
    });
    expect(transcriptionGenerator.lastAudio).toBeNull();
  });

  it("rejects malformed, stereo, and over-duration WAV before generation", async () => {
    const malformed = await app.inject({
      method: "POST",
      url: "/v1/speech/transcribe",
      headers: {
        "content-type": "audio/wav",
        "x-ai-character-kit-transcription-version": "1",
        "x-ai-character-kit-request-id": "transcription-route-004",
      },
      payload: Buffer.from("not-wave"),
    });
    const stereoWave = createCanonicalWave();
    stereoWave.writeUInt16LE(2, 22);
    const stereo = await app.inject({
      method: "POST",
      url: "/v1/speech/transcribe",
      headers: {
        "content-type": "audio/wav",
        "x-ai-character-kit-transcription-version": "1",
        "x-ai-character-kit-request-id": "transcription-route-005",
      },
      payload: stereoWave,
    });
    const tooLong = await app.inject({
      method: "POST",
      url: "/v1/speech/transcribe",
      headers: {
        "content-type": "audio/wav",
        "x-ai-character-kit-transcription-version": "1",
        "x-ai-character-kit-request-id": "transcription-route-006",
      },
      payload: createCanonicalWave(16_000 * 15 + 1, 16_000),
    });

    expect(malformed.statusCode).toBe(400);
    expect(malformed.json()).toMatchObject({ error: { code: "invalid_audio" } });
    expect(stereo.statusCode).toBe(400);
    expect(stereo.json()).toMatchObject({ error: { code: "invalid_audio" } });
    expect(tooLong.statusCode).toBe(400);
    expect(tooLong.json()).toMatchObject({ error: { code: "audio_too_long" } });
    expect(transcriptionGenerator.lastAudio).toBeNull();
  });

  it("returns route-correct JSON for unsupported content type and oversized WAV", async () => {
    const wrongContentType = await app.inject({
      method: "POST",
      url: "/v1/speech/transcribe",
      headers: {
        "content-type": "application/json",
        "x-ai-character-kit-transcription-version": "1",
        "x-ai-character-kit-request-id": "transcription-route-007",
      },
      payload: { not: "audio" },
    });
    const oversized = await app.inject({
      method: "POST",
      url: "/v1/speech/transcribe",
      headers: {
        "content-type": "audio/wav",
        "x-ai-character-kit-transcription-version": "1",
        "x-ai-character-kit-request-id": "transcription-route-008",
      },
      payload: Buffer.alloc(2 * 1024 * 1024 + 1),
    });

    expect(wrongContentType.statusCode).toBe(400);
    expect(wrongContentType.json()).toMatchObject({
      requestId: "transcription-route-007",
      status: "error",
    });
    expect(oversized.statusCode).toBe(413);
    expect(oversized.json()).toMatchObject({
      requestId: "transcription-route-008",
      status: "error",
    });
  });

  it("maps one safe generator failure without exposing audio or provider detail", async () => {
    transcriptionGenerator.error = new NpcServiceError(
      "rate_limited",
      "Transcription is temporarily limited.",
      429,
      true,
      "test_transcription_rate_limit",
    );
    const response = await app.inject({
      method: "POST",
      url: "/v1/speech/transcribe",
      headers: {
        "content-type": "audio/wav",
        "x-ai-character-kit-transcription-version": "1",
        "x-ai-character-kit-request-id": "transcription-route-009",
      },
      payload: createCanonicalWave(),
    });

    expect(response.statusCode).toBe(429);
    expect(response.json()).toMatchObject({
      requestId: "transcription-route-009",
      error: { code: "rate_limited", retryable: true },
    });
  });
});
