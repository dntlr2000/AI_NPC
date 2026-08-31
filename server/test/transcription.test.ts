import OpenAI from "openai";
import { describe, expect, it } from "vitest";
import {
  OpenAiTranscriptionGenerator,
  validateCanonicalPcm16Wave,
} from "../src/transcription.js";
import { createCanonicalWave } from "./wav-fixture.js";

/** Creates one generator whose SDK client returns controlled text or an error. */
function createGenerator(
  result: string | Error,
  capturedBodies: unknown[] = [],
  capturedOptions: unknown[] = [],
): OpenAiTranscriptionGenerator {
  const fakeClient = {
    audio: {
      transcriptions: {
        create: async (body: unknown, options: unknown): Promise<unknown> => {
          capturedBodies.push(body);
          capturedOptions.push(options);
          if (result instanceof Error) {
            throw result;
          }

          return { text: result };
        },
      },
    },
  } as unknown as OpenAI;

  return new OpenAiTranscriptionGenerator(
    {
      apiKey: "test-key-never-sent",
      model: "gpt-transcribe",
      timeoutMs: 30_000,
    },
    fakeClient,
  );
}

describe("Transcription audio and OpenAI adapter", () => {
  it("validates canonical WAV metadata and rejects malformed audio", () => {
    const valid = validateCanonicalPcm16Wave(
      createCanonicalWave(8_000, 16_000),
    );
    expect(valid.sampleRate).toBe(16_000);
    expect(valid.sampleFrames).toBe(8_000);
    expect(valid.durationMilliseconds).toBe(500);

    const truncated = createCanonicalWave();
    truncated.writeUInt32LE(123, 40);
    expect(() => validateCanonicalPcm16Wave(truncated))
      .toThrowError(expect.objectContaining({ code: "invalid_audio" }));

    const stereo = createCanonicalWave();
    stereo.writeUInt16LE(2, 22);
    expect(() => validateCanonicalPcm16Wave(stereo))
      .toThrowError(expect.objectContaining({ code: "invalid_audio" }));
  });

  it("rejects unsupported rates and recordings longer than 15 seconds", () => {
    expect(() => validateCanonicalPcm16Wave(createCanonicalWave(100, 4_000)))
      .toThrowError(expect.objectContaining({ code: "invalid_audio" }));
    expect(() => validateCanonicalPcm16Wave(
      createCanonicalWave(16_000 * 15 + 1, 16_000),
    )).toThrowError(expect.objectContaining({ code: "audio_too_long" }));
  });

  it("uploads an in-memory WAV with the configured model and returns text", async () => {
    const capturedBodies: unknown[] = [];
    const capturedOptions: unknown[] = [];
    const generator = createGenerator(
      "안녕하세요.",
      capturedBodies,
      capturedOptions,
    );
    const signal = new AbortController().signal;

    const result = await generator.generate(
      validateCanonicalPcm16Wave(createCanonicalWave()),
      signal,
    );

    expect(result.text).toBe("안녕하세요.");
    expect(capturedBodies[0]).toMatchObject({
      model: "gpt-transcribe",
      response_format: "json",
    });
    const file = Reflect.get(capturedBodies[0] as object, "file") as File;
    expect(file.name).toBe("input.wav");
    expect(file.type).toBe("audio/wav");
    expect(capturedOptions[0]).toMatchObject({ signal });
  });

  it("rejects empty provider text and safely maps rate limits and timeouts", async () => {
    const audio = validateCanonicalPcm16Wave(createCanonicalWave());
    await expect(createGenerator(" ").generate(
      audio,
      new AbortController().signal,
    )).rejects.toMatchObject({ code: "upstream_invalid_response" });
    await expect(createGenerator(Object.assign(new Error("private"), {
      status: 429,
    })).generate(
      audio,
      new AbortController().signal,
    )).rejects.toMatchObject({ code: "rate_limited", retryable: true });
    await expect(createGenerator(Object.assign(new Error("private"), {
      name: "APIConnectionTimeoutError",
    })).generate(
      audio,
      new AbortController().signal,
    )).rejects.toMatchObject({ code: "upstream_timeout", retryable: true });
  });

  it("maps caller cancellation without leaking provider text", async () => {
    const cancellation = new AbortController();
    cancellation.abort();
    await expect(createGenerator(new Error("private cancellation")).generate(
      validateCanonicalPcm16Wave(createCanonicalWave()),
      cancellation.signal,
    )).rejects.toMatchObject({
      code: "upstream_unavailable",
      retryable: true,
    });
  });
});
