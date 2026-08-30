import { describe, expect, it } from "vitest";
import {
  DEFAULT_OPENAI_TTS_TIMEOUT_MS,
  DEFAULT_SESSION_IDLE_TTL_SECONDS,
  DEFAULT_SESSION_MAX_CONTEXT_BYTES,
  DEFAULT_SESSION_MAX_COUNT,
  DEFAULT_SESSION_MAX_TURNS,
  DEFAULT_TTS_MODEL,
  DEFAULT_TTS_VOICE_PRESETS_PATH,
  loadServerConfig,
} from "../src/config.js";

describe("server configuration", () => {
  it("uses documented bounded session defaults", () => {
    const config = loadServerConfig({ OPENAI_API_KEY: "test-key" });

    expect(config.sessionOptions).toEqual({
      maxTurns: DEFAULT_SESSION_MAX_TURNS,
      maxContextBytes: DEFAULT_SESSION_MAX_CONTEXT_BYTES,
      idleTtlMs: DEFAULT_SESSION_IDLE_TTL_SECONDS * 1_000,
      maxSessions: DEFAULT_SESSION_MAX_COUNT,
    });
    expect(config.ttsModel).toBe(DEFAULT_TTS_MODEL);
    expect(config.openAiTtsTimeoutMs).toBe(DEFAULT_OPENAI_TTS_TIMEOUT_MS);
    expect(config.ttsVoicePresetsPath).toBe(DEFAULT_TTS_VOICE_PRESETS_PATH);
  });

  it("loads validated TTS model, timeout, and preset path overrides", () => {
    const config = loadServerConfig({
      OPENAI_API_KEY: "test-key",
      OPENAI_TTS_MODEL: "gpt-4o-mini-tts-2025-12-15",
      OPENAI_TTS_TIMEOUT_MS: "45000",
      NPC_TTS_VOICE_PRESETS_PATH: "config/project-voices.json",
    });

    expect(config.ttsModel).toBe("gpt-4o-mini-tts-2025-12-15");
    expect(config.openAiTtsTimeoutMs).toBe(45_000);
    expect(config.ttsVoicePresetsPath).toBe("config/project-voices.json");
  });

  it("rejects an out-of-range TTS timeout", () => {
    expect(() => loadServerConfig({
      OPENAI_API_KEY: "test-key",
      OPENAI_TTS_TIMEOUT_MS: "500",
    })).toThrow("OPENAI_TTS_TIMEOUT_MS");
  });

  it("loads every supported session environment override", () => {
    const config = loadServerConfig({
      OPENAI_API_KEY: "test-key",
      NPC_SESSION_MAX_TURNS: "12",
      NPC_SESSION_MAX_CONTEXT_BYTES: "32768",
      NPC_SESSION_IDLE_TTL_SECONDS: "3600",
      NPC_SESSION_MAX_COUNT: "256",
    });

    expect(config.sessionOptions).toEqual({
      maxTurns: 12,
      maxContextBytes: 32_768,
      idleTtlMs: 3_600_000,
      maxSessions: 256,
    });
  });

  it("rejects session overrides outside their documented ranges", () => {
    expect(() => loadServerConfig({
      OPENAI_API_KEY: "test-key",
      NPC_SESSION_MAX_TURNS: "0",
    })).toThrow("NPC_SESSION_MAX_TURNS");
    expect(() => loadServerConfig({
      OPENAI_API_KEY: "test-key",
      NPC_SESSION_MAX_CONTEXT_BYTES: "2048",
    })).toThrow("NPC_SESSION_MAX_CONTEXT_BYTES");
    expect(() => loadServerConfig({
      OPENAI_API_KEY: "test-key",
      NPC_SESSION_IDLE_TTL_SECONDS: "30",
    })).toThrow("NPC_SESSION_IDLE_TTL_SECONDS");
    expect(() => loadServerConfig({
      OPENAI_API_KEY: "test-key",
      NPC_SESSION_MAX_COUNT: "0",
    })).toThrow("NPC_SESSION_MAX_COUNT");
  });
});
