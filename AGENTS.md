# AI Character Kit — Repository Instructions

## Project goal

Build a reusable Unity 3D framework for creating AI-driven NPC characters.

The framework will eventually support:

- Character personality and speech style profiles
- Structured dialogue responses
- Emotion and animation commands
- Conversation memory
- TTS and STT
- Realtime voice interaction
- Backend-based OpenAI integration
- Reuse across multiple Unity projects

## Repository layout

- `Assets/`: Unity source code, scenes, prefabs, ScriptableObjects, and other game assets
- `Packages/`: Unity package manifest and dependency lock file
- `ProjectSettings/`: project-wide Unity editor and runtime settings
- `docs/`: requirements, architecture, plans, and decisions; see `docs/ROADMAP.md`, `docs/PHASE7_PLAN.md`, `docs/CONTRACT_V2.md`, `docs/SPEECH_CONTRACT_V1.md`, and `docs/TRANSCRIPTION_CONTRACT_V1.md`
- `server/`: Node.js 24, TypeScript, Fastify, and OpenAI SDK local backend for conversation and optional speech paths

The Unity project root is the repository root. Do not assume a separate `unity/` directory.

## Source of truth

- Read `ProjectSettings/ProjectVersion.txt` before assuming the Unity version.
- Read `Packages/manifest.json` and `Packages/packages-lock.json` before assuming installed packages.
- Do not upgrade Unity or package versions unless explicitly requested.

## Current milestone

Phase 6 is complete on `main` at `0501e6d`. Phase 7 push-to-talk STT implementation, automated verification, and manual live microphone/OpenAI Play Mode verification are complete. The commit containing the Phase 7 implementation and completion documentation is the Phase 7 checkpoint.

The Phase 7 implementation scope is:

- A provider-neutral pure C# voice input controller and capture/transcription interfaces
- A separate Transcription V1 raw WAV/JSON contract and loopback backend endpoint
- Bounded PCM16 mono microphone capture with cancellation and duplicate prevention
- A reviewed-input flow that fills the existing text field and never auto-sends
- An Editor-generated single-character `VoiceInputNpcPrototype` sample scene

Until the next milestone is approved, do not add:

- Persistent or long-term memory
- Realtime voice
- Streaming STT/TTS, VAD, barge-in, custom voice training, lip sync, or audio caching
- Animator-based presentation
- Vector databases
- Remote deployment, client authentication, automatic retries, or streaming
- Changes that replace the existing Mock Play Mode path

## Architecture rules

- Runtime code must never reference `UnityEditor`.
- Core dialogue logic must not directly depend on Animator, UI, TextMeshPro, OpenAI, or HTTP.
- Transport DTOs, validation, and mapping must not depend on UnityEngine.
- JSON serialization must remain in the Unity boundary rather than Core or Transport.
- Unity networking must remain in `Runtime/Unity/Networking`.
- Backend vendor SDK code must remain under `server/`; Unity must not reference OpenAI.
- Use interfaces for external systems.
- Use `IAiConversationClient` for dialogue generation.
- Use `INpcPresentationDriver` for dialogue, emotion, and gesture presentation.
- Keep provider-neutral speech coordination in `AiCharacterKit.Speech`; it must not reference Unity, HTTP, or OpenAI.
- Use `ISpeechSynthesisClient` and `ISpeechPlaybackDriver` for optional TTS boundaries.
- Keep provider-neutral voice input coordination in `AiCharacterKit.Transcription`; it must not reference Unity, HTTP, or OpenAI.
- Use `IAudioCaptureDriver` and `ITranscriptionClient` for optional STT boundaries.
- Character-specific behavior must be stored in data, preferably ScriptableObject profiles.
- Do not place character personality directly inside MonoBehaviour code.
- Do not use static global state unless there is a documented reason.
- Avoid `async void` except Unity event handlers.
- Handle cancellation, duplicate requests, and error states.

## Unity safety rules

- Do not manually edit `.unity`, `.prefab`, or `.asset` YAML unless explicitly requested.
- Prefer an Editor script or documented Inspector setup over hand-written serialized YAML.
- Never modify `Library`, `Temp`, `Logs`, `obj`, or `UserSettings`.
- Preserve existing `.meta` files.
- When adding new Unity assets, let Unity generate their `.meta` files when practical.
- Do not delete or regenerate existing GUIDs.
- Do not add package dependencies without explaining why.

## Security

- Never store API keys in Unity source files, ScriptableObjects, Resources, StreamingAssets, or version control.
- OpenAI calls go through the local backend server only.
- Read `OPENAI_API_KEY` from the server process environment; never add a committed `.env` file.
- Do not log API keys, profile text, user messages, generated dialogue, or raw upstream errors.
- The backend binds only to `127.0.0.1`; remote exposure requires a separate security design.
- Phase 5 memory is process-local and must never be written to logs or disk.
- Unity stores only opaque voice preset IDs; OpenAI voice names, instructions, and speed remain in the server preset file.
- Microphone audio and transcription text must not be logged or persisted; Phase 7 sends only bounded WAV to the loopback backend.

## Completion requirements

For every implementation task:

1. State the files to be changed.
2. Keep the change limited to the requested milestone.
3. Run available tests or compilation checks.
4. Never claim Unity compilation succeeded unless Unity or an equivalent compiler was actually run.
5. Summarize changed files, verification performed, and remaining risks.
