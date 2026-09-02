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
- Data-driven conversation triggers that invoke consumer-owned game actions
- Reuse across multiple Unity projects

## Repository layout

- `Assets/`: consumer-owned Unity assets and imported/generated package samples; framework source does not live here after Phase 9
- `Packages/com.aicharacterkit.framework/`: AI Character Kit UPM Runtime, Editor, Tests, Samples~, Documentation~, and package metadata
- `Packages/`: Unity project manifest and dependency lock file in addition to the embedded kit package
- `ProjectSettings/`: project-wide Unity editor and runtime settings
- `docs/`: requirements, architecture, plans, and decisions; see `docs/ROADMAP.md`, `docs/PHASE11_PLAN.md`, `docs/PHASE12_PLAN.md` through `docs/PHASE14_PLAN.md`, `docs/REUSE_GUIDE.md`, and the versioned contract documents
- `server/`: Node.js 24, TypeScript, Fastify, and OpenAI SDK local backend for conversation and optional speech paths

The Unity project root is the repository root. Do not assume a separate `unity/` directory.

## Source of truth

- Read `ProjectSettings/ProjectVersion.txt` before assuming the Unity version.
- Read `Packages/manifest.json` and `Packages/packages-lock.json` before assuming installed packages.
- Do not upgrade Unity or package versions unless explicitly requested.

## Current milestone

Phase 9 is complete on `main` at checkpoint `cd5825b`. Phase 10 Character Builder is complete at `4dc478f`; automatic validation and consumer manual Builder/Mock/Prefab/TTS validation have passed. The current UPM release candidate is `com.aicharacterkit.framework` version `0.3.0`; `v0.2.0` remains the latest published tag until separate final approval creates and publishes `v0.3.0`.

Public GitHub release `v0.2.0` was published from exact commit `dcad604a510b767b18154eb01408218a2e1e51f2` under the MIT License with `dntlr2000` as author and copyright holder. Distribution uses the package subfolder Git URL; `server/` remains unpackaged reference source. Never move or overwrite the published tag. Repository visibility, registry publishing and later releases still require separate explicit approval.

Phase 11 implementation is checkpointed at `c38b5a0`, with its completed user documentation based at `7cf0a63`. Automatic validation, separate consumer Builder/Mock action validation, guarded action checks, and live V3 semantic trigger verification have passed. Its source of truth is `docs/PHASE11_PLAN.md`. Package `0.3.0` is being prepared as a public release candidate; no `v0.3.0` tag or GitHub Release exists until the user approves an exact release-preparation commit. The completed minimal conversation-trigger action pipeline:

- Author natural-language trigger-to-action bindings in Character Builder.
- Let the Backend return only configured matched trigger IDs in one structured response.
- Validate and deterministically select at most one action in Unity.
- Let consumers implement only `INpcActionHandler` or an optional Unity base class for the actual game effect.
- Preserve V1/V2 and the existing action-free Mock path; provide deterministic example-based Mock trigger matching.
- Keep arbitrary variables, scores, relationship systems and a generic rule engine out of Phase 11.

Phase 12 is recorded only as a provisional optional Advanced Behavior milestone in `docs/PHASE12_PLAN.md`. Phase 13 records Backend distribution and operations in `docs/PHASE13_PLAN.md`; Phase 14 records optional Realtime voice in `docs/PHASE14_PLAN.md`. Do not begin them without a new implementation plan. Their scope, order, public API and package version may be re-planned from consumer evidence, but preserve their responsibility boundaries in the roadmap.

The Phase 11 completion boundary excludes:

- Persistent or long-term memory
- Realtime voice
- Streaming STT/TTS, VAD, barge-in, custom voice training, lip sync, or audio caching
- Animator-based presentation
- Vector databases
- Remote deployment, client authentication, automatic retries, or streaming
- Changes that replace the existing Mock Play Mode path
- Package registry publishing until a separate release milestone is explicitly planned and approved
- Generic variable/score editors, relationship systems or persistent action state
- LLM tool calling, model-generated action parameters or Reflection-based method invocation
- Moving any published package tag, or creating/pushing `v0.3.0` without exact-commit approval

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
- Keep gameplay actions separate from `INpcPresentationDriver`; Phase 11 action routing uses configured IDs and consumer-owned handlers.
- Treat model-returned trigger IDs as untrusted input. Reject unknown IDs and let Unity perform the final game-state authorization before execution.
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
