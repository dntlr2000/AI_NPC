import { createApp } from "./app.js";
import { loadServerConfig, SERVER_HOST } from "./config.js";
import { OpenAiNpcResponseGenerator } from "./generator.js";
import { OpenAiSpeechGenerator } from "./speech.js";
import {
  InMemoryConversationSessionStore,
  SessionConversationService,
} from "./sessions.js";
import { loadVoicePresetResolver } from "./voice-presets.js";

/** Starts the local backend after validating credentials and bounded session settings. */
async function startServer(): Promise<void> {
  const config = loadServerConfig();
  const generator = new OpenAiNpcResponseGenerator({
    apiKey: config.apiKey,
    model: config.model,
    timeoutMs: config.openAiTimeoutMs,
  });
  const sessionService = new SessionConversationService(
    new InMemoryConversationSessionStore(config.sessionOptions),
    generator,
  );
  const speechGenerator = new OpenAiSpeechGenerator({
    apiKey: config.apiKey,
    model: config.ttsModel,
    timeoutMs: config.openAiTtsTimeoutMs,
  });
  const voicePresetResolver = loadVoicePresetResolver(
    config.ttsVoicePresetsPath,
  );
  const app = createApp({
    generator,
    sessionService,
    speechGenerator,
    voicePresetResolver,
  });

  registerShutdownSignal("SIGINT", app);
  registerShutdownSignal("SIGTERM", app);
  await app.listen({
    host: SERVER_HOST,
    port: config.port,
  });
}

/** Registers one process signal that gracefully closes the local HTTP listener. */
function registerShutdownSignal(
  signal: NodeJS.Signals,
  app: ReturnType<typeof createApp>,
): void {
  process.once(signal, () => {
    void app.close();
  });
}

void startServer().catch((error: unknown) => {
  const message = error instanceof Error ? error.message : "Unknown startup error.";
  process.stderr.write(`AI Character Kit backend failed to start: ${message}\n`);
  process.exitCode = 1;
});
