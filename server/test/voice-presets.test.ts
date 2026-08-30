import { describe, expect, it } from "vitest";
import {
  JsonVoicePresetResolver,
  loadVoicePresetResolver,
} from "../src/voice-presets.js";

describe("voice preset registry", () => {
  it("loads the default non-secret provider mappings", () => {
    const resolver = loadVoicePresetResolver("config/voice-presets.json");

    expect(resolver.resolve("warm-friendly")).toMatchObject({
      voice: "marin",
      speed: 1,
    });
    expect(resolver.resolve("calm-formal")).toMatchObject({
      voice: "cedar",
      speed: 0.95,
    });
    expect(resolver.resolve("missing")).toBeUndefined();
  });

  it("rejects invalid and duplicate preset files", () => {
    expect(() => loadVoicePresetResolver(
      "test/fixtures/invalid-voice-presets.json",
    )).toThrow("invalid");
    expect(() => loadVoicePresetResolver(
      "test/fixtures/duplicate-voice-presets.json",
    )).toThrow("Duplicate voice preset ID");
  });

  it("rejects an empty programmatic registry", () => {
    expect(() => new JsonVoicePresetResolver([]))
      .toThrow("At least one voice preset");
  });
});
