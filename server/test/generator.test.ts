import OpenAI from "openai";
import { describe, expect, it } from "vitest";
import type { AiNpcRequest } from "../src/contracts/v1.js";
import {
  buildNpcInstructions,
  OpenAiNpcResponseGenerator,
} from "../src/generator.js";
import { NpcServiceError } from "../src/errors.js";

const request: AiNpcRequest = {
  schemaVersion: 1,
  requestId: "req-generator-001",
  character: {
    characterId: "sample-luna",
    displayName: "Luna",
    personality: "Playful and curious.",
    speechStyle: "Warm and brief.",
    exampleDialogue: "Tell me about an adventure.",
    defaultEmotion: "happy",
  },
  userText: "안녕!",
};

/** Creates a generator whose SDK client returns one controlled response or error. */
function createGeneratorWithParseResult(
  parseResult: unknown,
  capturedRequests: unknown[] = [],
): OpenAiNpcResponseGenerator {
  const fakeClient = {
    responses: {
      parse: async (body: unknown): Promise<unknown> => {
        capturedRequests.push(body);
        if (parseResult instanceof Error) {
          throw parseResult;
        }

        return parseResult;
      },
    },
  } as unknown as OpenAI;

  return new OpenAiNpcResponseGenerator(
    {
      apiKey: "test-key-never-sent",
      model: "gpt-5.6-luna",
      timeoutMs: 30_000,
    },
    fakeClient,
  );
}

describe("OpenAiNpcResponseGenerator", () => {
  it("builds profile instructions and returns parsed output with usage", async () => {
    const capturedRequests: unknown[] = [];
    const generator = createGeneratorWithParseResult(
      {
        id: "resp-generator-test",
        status: "completed",
        output: [
          {
            type: "message",
            content: [
              {
                type: "output_text",
                parsed: {
                  dialogue: "루나: 안녕!",
                  emotion: "happy",
                  gesture: "wave",
                },
              },
            ],
          },
        ],
        usage: {
          input_tokens: 20,
          output_tokens: 8,
          total_tokens: 28,
        },
      },
      capturedRequests,
    );

    const generated = await generator.generate(request, new AbortController().signal);

    expect(generated.result.dialogue).toBe("루나: 안녕!");
    expect(generated.telemetry.totalTokens).toBe(28);
    expect(capturedRequests).toHaveLength(1);
    expect(capturedRequests[0]).toMatchObject({
      model: "gpt-5.6-luna",
      store: false,
      reasoning: { effort: "none" },
      max_output_tokens: 256,
      input: [{ role: "user", content: "안녕!" }],
    });
    expect(buildNpcInstructions(request)).toContain('"displayName":"Luna"');
  });

  it("maps a structured refusal without returning its text", async () => {
    const generator = createGeneratorWithParseResult({
      id: "resp-refusal",
      status: "completed",
      output: [
        {
          type: "message",
          content: [{ type: "refusal", refusal: "Private refusal text." }],
        },
      ],
      usage: null,
    });

    await expect(
      generator.generate(request, new AbortController().signal),
    ).rejects.toMatchObject({
      code: "content_refused",
      statusCode: 422,
      retryable: false,
    });
  });

  it("maps incomplete output and rate limits to stable errors", async () => {
    const incompleteGenerator = createGeneratorWithParseResult({
      id: "resp-incomplete",
      status: "incomplete",
      output: [],
      usage: null,
    });
    const rateLimitError = Object.assign(new Error("private upstream message"), {
      status: 429,
    });
    const rateLimitedGenerator = createGeneratorWithParseResult(rateLimitError);

    await expect(
      incompleteGenerator.generate(request, new AbortController().signal),
    ).rejects.toMatchObject({
      code: "upstream_invalid_response",
      retryable: true,
    });
    await expect(
      rateLimitedGenerator.generate(request, new AbortController().signal),
    ).rejects.toMatchObject({
      code: "rate_limited",
      retryable: true,
    });
  });

  it("maps schema-invalid parsed output to an upstream contract error", async () => {
    const generator = createGeneratorWithParseResult({
      id: "resp-invalid-structured-output",
      status: "completed",
      output: [
        {
          type: "message",
          content: [
            {
              type: "output_text",
              parsed: {
                dialogue: "Hello.",
                emotion: "unknown",
                gesture: "wave",
              },
            },
          ],
        },
      ],
      usage: null,
    });

    await expect(
      generator.generate(request, new AbortController().signal),
    ).rejects.toMatchObject({
      code: "upstream_invalid_response",
      statusCode: 502,
      retryable: true,
    });
  });

  it("never includes the API key in a mapped error message", async () => {
    const generator = createGeneratorWithParseResult(
      new Error("test-key-never-sent"),
    );

    try {
      await generator.generate(request, new AbortController().signal);
      throw new Error("Expected the generator to fail.");
    } catch (error: unknown) {
      expect(error).toBeInstanceOf(NpcServiceError);
      expect((error as Error).message).not.toContain("test-key-never-sent");
    }
  });
});
