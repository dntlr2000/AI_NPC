import { readFileSync } from "node:fs";
import { isAbsolute, resolve } from "node:path";
import { z } from "zod";
import { voicePresetIdSchema } from "./contracts/speech-v1.js";

const builtInVoiceSchema = z.enum([
  "alloy",
  "ash",
  "ballad",
  "coral",
  "echo",
  "fable",
  "onyx",
  "nova",
  "sage",
  "shimmer",
  "verse",
  "marin",
  "cedar",
]);

const voicePresetSchema = z
  .object({
    id: voicePresetIdSchema,
    voice: builtInVoiceSchema,
    instructions: z.string().trim().min(1).max(4_096),
    speed: z.number().min(0.25).max(4),
  })
  .strict();

const voicePresetFileSchema = z
  .object({
    presets: z.array(voicePresetSchema).min(1),
  })
  .strict();

export type VoicePreset = z.infer<typeof voicePresetSchema>;

export interface VoicePresetResolver {
  /** Resolves one stable preset ID without exposing its provider settings to callers. */
  resolve(voicePresetId: string): VoicePreset | undefined;
}

/** Stores validated provider settings behind stable project-owned preset IDs. */
export class JsonVoicePresetResolver implements VoicePresetResolver {
  private readonly presets: ReadonlyMap<string, VoicePreset>;

  /** Creates an immutable resolver and rejects duplicate preset IDs. */
  public constructor(presets: readonly VoicePreset[]) {
    const mapped = new Map<string, VoicePreset>();
    for (const preset of presets) {
      if (mapped.has(preset.id)) {
        throw new Error(`Duplicate voice preset ID '${preset.id}'.`);
      }

      mapped.set(preset.id, preset);
    }

    if (mapped.size === 0) {
      throw new Error("At least one voice preset is required.");
    }

    this.presets = mapped;
  }

  /** Returns one validated preset or undefined when the ID is not configured. */
  public resolve(voicePresetId: string): VoicePreset | undefined {
    return this.presets.get(voicePresetId);
  }
}

/** Loads and validates one non-secret JSON preset file during server startup. */
export function loadVoicePresetResolver(
  configuredPath: string,
  workingDirectory: string = process.cwd(),
): JsonVoicePresetResolver {
  const absolutePath = isAbsolute(configuredPath)
    ? configuredPath
    : resolve(workingDirectory, configuredPath);
  let parsedJson: unknown;
  try {
    parsedJson = JSON.parse(readFileSync(absolutePath, "utf8"));
  } catch (error: unknown) {
    throw new Error(
      `NPC TTS voice preset file could not be read: ${absolutePath}`,
      { cause: error },
    );
  }

  const parsed = voicePresetFileSchema.safeParse(parsedJson);
  if (!parsed.success) {
    throw new Error(`NPC TTS voice preset file is invalid: ${absolutePath}`);
  }

  return new JsonVoicePresetResolver(parsed.data.presets);
}
