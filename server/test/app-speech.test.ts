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
import { JsonVoicePresetResolver } from "../src/voice-presets.js";

/** Supplies the unrelated conversation dependency without invoking OpenAI. */
class StubNpcGenerator implements NpcResponseGenerator {
  /** Returns a fixed result if an unrelated conversation route is called. */
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

/** Supplies deterministic PCM or one configured safe route error. */
class StubSpeechGenerator implements SpeechGenerator {
  public error: Error | null = null;
  public lastRequest: SpeechGenerationRequest | null = null;

  /** Returns two PCM samples or the configured failure. */
  public async generate(
    request: SpeechGenerationRequest,
  ): Promise<SpeechGenerationResult> {
    this.lastRequest = request;
    if (this.error !== null) {
      throw this.error;
    }

    return { pcmAudio: Buffer.from([0, 128, 255, 127]) };
  }
}

describe("Speech V1 Fastify route", () => {
  let app: FastifyInstance;
  let speechGenerator: StubSpeechGenerator;

  beforeEach(() => {
    const generator = new StubNpcGenerator();
    speechGenerator = new StubSpeechGenerator();
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
      speechGenerator,
      voicePresetResolver: new JsonVoicePresetResolver([{
        id: "warm-friendly",
        voice: "marin",
        instructions: "Speak warmly.",
        speed: 1,
      }]),
      logger: false,
    });
  });

  afterEach(async () => {
    await app.close();
  });

  it("returns correlated fixed-format PCM headers and bytes", async () => {
    const response = await app.inject({
      method: "POST",
      url: "/v1/speech/synthesize",
      payload: {
        schemaVersion: 1,
        requestId: "speech-route-001",
        voicePresetId: "warm-friendly",
        text: "안녕하세요.",
      },
    });

    expect(response.statusCode).toBe(200);
    expect(response.headers["content-type"]).toContain("application/octet-stream");
    expect(response.headers["x-ai-character-kit-speech-version"]).toBe("1");
    expect(response.headers["x-ai-character-kit-request-id"])
      .toBe("speech-route-001");
    expect(response.headers["x-ai-character-kit-audio-format"])
      .toBe("pcm_s16le");
    expect(response.headers["x-ai-character-kit-sample-rate"]).toBe("24000");
    expect(response.headers["x-ai-character-kit-channels"]).toBe("1");
    expect([...response.rawPayload]).toEqual([0, 128, 255, 127]);
    expect(speechGenerator.lastRequest).toMatchObject({
      text: "안녕하세요.",
      preset: { id: "warm-friendly", voice: "marin" },
    });
  });

  it("rejects unsupported versions and unknown presets before generation", async () => {
    const unsupported = await app.inject({
      method: "POST",
      url: "/v1/speech/synthesize",
      payload: {
        schemaVersion: 2,
        requestId: "speech-route-002",
        voicePresetId: "warm-friendly",
        text: "Hello.",
      },
    });
    const missingPreset = await app.inject({
      method: "POST",
      url: "/v1/speech/synthesize",
      payload: {
        schemaVersion: 1,
        requestId: "speech-route-003",
        voicePresetId: "missing-preset",
        text: "Hello.",
      },
    });

    expect(unsupported.statusCode).toBe(400);
    expect(unsupported.json()).toMatchObject({
      error: { code: "unsupported_schema_version" },
    });
    expect(missingPreset.statusCode).toBe(400);
    expect(missingPreset.json()).toMatchObject({
      error: { code: "voice_preset_not_found", retryable: false },
    });
    expect(speechGenerator.lastRequest).toBeNull();
  });

  it("returns a route-correct error for malformed JSON", async () => {
    const response = await app.inject({
      method: "POST",
      url: "/v1/speech/synthesize",
      headers: { "content-type": "application/json" },
      payload: "{not-json",
    });

    expect(response.statusCode).toBe(400);
    expect(response.json()).toMatchObject({
      schemaVersion: 1,
      status: "error",
      error: { code: "invalid_request" },
    });
  });

  it("maps safe generator errors without returning binary content", async () => {
    speechGenerator.error = new NpcServiceError(
      "rate_limited",
      "Speech is temporarily limited.",
      429,
      true,
      "test_speech_rate_limit",
    );

    const response = await app.inject({
      method: "POST",
      url: "/v1/speech/synthesize",
      payload: {
        schemaVersion: 1,
        requestId: "speech-route-004",
        voicePresetId: "warm-friendly",
        text: "Hello.",
      },
    });

    expect(response.statusCode).toBe(429);
    expect(response.headers["content-type"]).toContain("application/json");
    expect(response.json()).toMatchObject({
      requestId: "speech-route-004",
      error: { code: "rate_limited", retryable: true },
    });
  });
});
