import { Buffer } from "node:buffer";

/** Creates one canonical PCM16 mono WAV with silent samples for route tests. */
export function createCanonicalWave(
  sampleFrames = 1_600,
  sampleRate = 16_000,
): Buffer {
  const dataBytes = sampleFrames * 2;
  const wave = Buffer.alloc(44 + dataBytes);
  wave.write("RIFF", 0, "ascii");
  wave.writeUInt32LE(wave.byteLength - 8, 4);
  wave.write("WAVE", 8, "ascii");
  wave.write("fmt ", 12, "ascii");
  wave.writeUInt32LE(16, 16);
  wave.writeUInt16LE(1, 20);
  wave.writeUInt16LE(1, 22);
  wave.writeUInt32LE(sampleRate, 24);
  wave.writeUInt32LE(sampleRate * 2, 28);
  wave.writeUInt16LE(2, 32);
  wave.writeUInt16LE(16, 34);
  wave.write("data", 36, "ascii");
  wave.writeUInt32LE(dataBytes, 40);
  return wave;
}
