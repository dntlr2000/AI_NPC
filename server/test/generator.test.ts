import OpenAI from "openai";
import { describe, expect, it } from "vitest";
import {
  buildNpcInstructions,
  OpenAiNpcResponseGenerator,
} from "../src/generator.js";
import type { NpcGenerationRequest } from "../src/generator.js";
import { NpcServiceError } from "../src/errors.js";

const request: NpcGenerationRequest = {
  character: {
    characterId: "sample-luna",
    displayName: "Luna",
    personality: "Playful and curious.",
    speechStyle: "Warm and brief.",
    exampleDialogue: "Tell me about an adventure.",
    defaultEmotion: "happy",
  },
  history: [],
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
    expect(buildNpcInstructions(request)).toContain("supplied conversation messages");
  });

  it("replays bounded user and assistant history before the current message", async () => {
    const capturedRequests: unknown[] = [];
    const generator = createGeneratorWithParseResult(
      {
        id: "resp-history-test",
        status: "completed",
        output: [
          {
            type: "message",
            content: [
              {
                type: "output_text",
                parsed: {
                  dialogue: "파란색이라고 했지!",
                  emotion: "happy",
                  gesture: "nod",
                },
              },
            ],
          },
        ],
        usage: null,
      },
      capturedRequests,
    );

    await generator.generate(
      {
        ...request,
        history: [
          { role: "user", content: "내가 좋아하는 색은 파랑이야." },
          { role: "assistant", content: "파란색을 기억할게." },
        ],
        userText: "내가 좋아하는 색은?",
      },
      new AbortController().signal,
    );

    expect(capturedRequests[0]).toMatchObject({
      store: false,
      input: [
        { role: "user", content: "내가 좋아하는 색은 파랑이야." },
        { role: "assistant", content: "파란색을 기억할게." },
        { role: "user", content: "내가 좋아하는 색은?" },
      ],
    });
  });

  it("uses one V3 structured response for dialogue and only configured trigger IDs", async () => {
    const capturedRequests: unknown[] = [];
    const generator = createGeneratorWithParseResult(
      {
        id: "resp-action-test",
        status: "completed",
        output: [
          {
            type: "message",
            content: [
              {
                type: "output_text",
                parsed: {
                  dialogue: "문을 확인할게.",
                  emotion: "neutral",
                  gesture: "nod",
                  matchedTriggerIds: ["open_gate"],
                },
              },
            ],
          },
        ],
        usage: null,
      },
      capturedRequests,
    );

    const generated = await generator.generate(
      {
        ...request,
        triggers: [
          {
            triggerId: "open_gate",
            conditionDescription: "The player asks to open the gate.",
          },
        ],
      },
      new AbortController().signal,
    );

    expect(generated.result.matchedTriggerIds).toEqual(["open_gate"]);
    expect(capturedRequests[0]).toMatchObject({ store: false });
    expect(buildNpcInstructions({
      ...request,
      triggers: [{
        triggerId: "open_gate",
        conditionDescription: "The player asks to open the gate.",
      }],
    })).toContain("Never invent an ID");
  });

  it("uses V4 grounding instructions and supports an empty trigger snapshot", async () => {
    const capturedRequests: unknown[] = [];
    const generator = createGeneratorWithParseResult(
      {
        id: "resp-grounding-test",
        status: "completed",
        output: [{
          type: "message",
          content: [{
            type: "output_text",
            parsed: {
              dialogue: "경비병: 서쪽 문은 닫혀 있습니다.",
              emotion: "neutral",
              gesture: "nod",
              matchedTriggerIds: [],
            },
          }],
        }],
        usage: null,
      },
      capturedRequests,
    );
    const groundedRequest: NpcGenerationRequest = {
      ...request,
      triggers: [],
      grounding: {
        revision: "ctx-test",
        background: "The western gate protects Dawnfall.",
        goalsAndValues: "Protect citizens.",
        behavioralRules: ["Do not invent permissions."],
        dialogueExamples: ["Guard: State your business."],
        facts: [{
          factId: "gate_status",
          kind: "observation",
          statement: "The western gate is closed.",
          priority: 90,
        }],
      },
    };

    const generated = await generator.generate(
      groundedRequest,
      new AbortController().signal,
    );

    expect(generated.result.matchedTriggerIds).toEqual([]);
    expect(capturedRequests[0]).toMatchObject({ store: false });
    const instructions = buildNpcInstructions(groundedRequest);
    expect(instructions).toContain("request-time game state");
    expect(instructions).toContain('"factId":"gate_status"');
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
