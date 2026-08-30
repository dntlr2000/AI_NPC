import OpenAI from "openai";
import { describe, expect, it } from "vitest";
import { MAX_SPEECH_AUDIO_BYTES } from "../src/contracts/speech-v1.js";
import { OpenAiSpeechGenerator } from "../src/speech.js";
import type { SpeechGenerationRequest } from "../src/speech.js";

const request: SpeechGenerationRequest = {
  text: "안녕하세요.",
  preset: {
    id: "warm-friendly",
    voice: "marin",
    instructions: "Speak warmly.",
    speed: 1,
  },
};

/** Creates one generator whose SDK client returns controlled PCM or an error. */
function createGenerator(
  result: Uint8Array | Error,
  capturedBodies: unknown[] = [],
  capturedOptions: unknown[] = [],
): OpenAiSpeechGenerator {
  const fakeClient = {
    audio: {
      speech: {
        create: async (body: unknown, options: unknown): Promise<Response> => {
          capturedBodies.push(body);
          capturedOptions.push(options);
          if (result instanceof Error) {
            throw result;
          }

          return new Response(result);
        },
      },
    },
  } as unknown as OpenAI;

  return new OpenAiSpeechGenerator(
    {
      apiKey: "test-key-never-sent",
      model: "gpt-4o-mini-tts",
      timeoutMs: 30_000,
    },
    fakeClient,
  );
}

describe("OpenAiSpeechGenerator", () => {
  it("sends resolved preset settings and returns complete PCM", async () => {
    const capturedBodies: unknown[] = [];
    const capturedOptions: unknown[] = [];
    const generator = createGenerator(
      new Uint8Array([0, 128, 255, 127]),
      capturedBodies,
      capturedOptions,
    );
    const signal = new AbortController().signal;

    const generated = await generator.generate(request, signal);

    expect([...generated.pcmAudio]).toEqual([0, 128, 255, 127]);
    expect(capturedBodies[0]).toMatchObject({
      model: "gpt-4o-mini-tts",
      voice: "marin",
      input: "안녕하세요.",
      instructions: "Speak warmly.",
      speed: 1,
      response_format: "pcm",
      stream_format: "audio",
    });
    expect(capturedOptions[0]).toMatchObject({ signal });
  });

  it("rejects empty, partial-sample, and oversized PCM", async () => {
    await expect(createGenerator(new Uint8Array()).generate(
      request,
      new AbortController().signal,
    )).rejects.toMatchObject({ code: "upstream_invalid_response" });
    await expect(createGenerator(new Uint8Array([1])).generate(
      request,
      new AbortController().signal,
    )).rejects.toMatchObject({ code: "upstream_invalid_response" });
    await expect(createGenerator(
      new Uint8Array(MAX_SPEECH_AUDIO_BYTES + 2),
    ).generate(
      request,
      new AbortController().signal,
    )).rejects.toMatchObject({ code: "upstream_invalid_response" });
  });

  it("maps rate limits and timeout errors without leaking provider text", async () => {
    const rateLimit = Object.assign(new Error("private rate detail"), {
      status: 429,
    });
    const timeout = Object.assign(new Error("private timeout detail"), {
      name: "APIConnectionTimeoutError",
    });

    await expect(createGenerator(rateLimit).generate(
      request,
      new AbortController().signal,
    )).rejects.toMatchObject({ code: "rate_limited", retryable: true });
    await expect(createGenerator(timeout).generate(
      request,
      new AbortController().signal,
    )).rejects.toMatchObject({ code: "upstream_timeout", retryable: true });
  });

  it("maps caller cancellation to a safe retryable error", async () => {
    const cancellation = new AbortController();
    cancellation.abort();

    await expect(createGenerator(new Error("private cancellation")).generate(
      request,
      cancellation.signal,
    )).rejects.toMatchObject({
      code: "upstream_unavailable",
      retryable: true,
    });
  });
});
