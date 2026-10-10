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
- Request-time character canon, world lore, beliefs, and live game-state grounding
- Reuse across multiple Unity projects

## Repository layout

- `Assets/`: consumer-owned Unity assets and imported/generated package samples; framework source does not live here after Phase 9
- `Packages/com.aicharacterkit.framework/`: AI Character Kit UPM Runtime, Editor, Tests, Samples~, Documentation~, and package metadata
- `Packages/`: Unity project manifest and dependency lock file in addition to the embedded kit package
- `ProjectSettings/`: project-wide Unity editor and runtime settings
- `docs/`: requirements, architecture, plans, and decisions; see `docs/ROADMAP.md`, `docs/PHASE15_PLAN.md`, `docs/PHASE12_PLAN.md` through `docs/PHASE16_PLAN.md`, `docs/REUSE_GUIDE.md`, and the versioned contract documents
- `server/`: Node.js 24, TypeScript, Fastify, and OpenAI SDK local backend for conversation and optional speech paths

The Unity project root is the repository root. Do not assume a separate `unity/` directory.

## Source of truth

- Read `ProjectSettings/ProjectVersion.txt` before assuming the Unity version.
- Read `Packages/manifest.json` and `Packages/packages-lock.json` before assuming installed packages.
- Do not upgrade Unity or package versions unless explicitly requested.

## Current milestone

Public GitHub releases `v0.2.0`, `v0.3.0`, and `v0.4.0` are immutable. `v0.3.0` was published from exact commit `42f5c5916f5e8fb20cfcec742b91a7451e062a0e`; `v0.4.0` was published on 2026-09-04 from exact commit `027b61f053c620d4e5202830b18692a6d45d3458`, under the MIT License with `dntlr2000` as author and copyright holder. Distribution uses the package subfolder Git URL; `server/` remains unpackaged reference source. Never move or overwrite a published tag. Registry publishing and later releases require separate explicit approval.

Phase 11 is complete and published in `v0.3.0`. Its minimal conversation-trigger action pipeline:

- Author natural-language trigger-to-action bindings in Character Builder.
- Let the Backend return only configured matched trigger IDs in one structured response.
- Validate and deterministically select at most one action in Unity.
- Let consumers implement only `INpcActionHandler` or an optional Unity base class for the actual game effect.
- Preserve V1/V2 and the existing action-free Mock path; provide deterministic example-based Mock trigger matching.
- Keep arbitrary variables, scores, relationship systems and a generic rule engine out of Phase 11.

Phase 15 Runtime Context & Lore Grounding is complete and published as package `0.4.0`. It adds immutable per-turn grounding snapshots, V4 transport/backend routes, Character Builder authoring and a Grounded Guard sample while preserving V1–V3. Release-time automatic Server and Unity validation plus user-confirmed live V4 Play Mode validation passed. Its source of truth is `docs/PHASE15_PLAN.md`, `docs/CONTRACT_V4.md`, and `docs/RELEASE_0.4.0.md`.

The Phase 15 Unreleased stabilization preserves the V4 serialized request budget, static provider validation, regressions and corrected release records. Keep its changes and validation separate from the published release. Builder/Awake configuration checks must not call live providers; retain the explicit live validation APIs.

Phase 16 is now authorized by the user's 2026-09-30 implementation plan: optional Windows Editor and Windows x64 Mono local dialogue with Gemma 4 E4B IT Q4_K_M and llama.cpp b11146 Vulkan. Follow docs/PHASE16_PLAN.md and record current-source verification in docs/PHASE16_VALIDATION.md. Keep model/runtime files external, reuse original weights, and never auto-download, copy weights, or bundle native binaries into UPM. Preserve Unity/package versions, dependencies, V4/Core interfaces and public tags. Pure LocalInference owns bounded per-NPC memory; Unity owns process/HTTP/JSON; final action authorization remains CanExecute. No automatic retries or online fallback. Do not claim completion without actual Unity, consumer and Windows player verification.

Phase 12 is authorized by the user's 2026-10-03 implementation plan in docs/PHASE12_PLAN.md. Implement the optional Advanced Behavior layer while preserving Basic and the Phase 15/16 corrections. Behavior references only Core; typed consumer values, deterministic bounded state changes and one selected action remain separate from AI trigger classification. Keep memory reset and behavior reset independent, retain instance state across disable/re-enable, and never call providers/handlers/model preparation during Builder or Awake checks. Verify current source separately in docs/PHASE12_VALIDATION.md, including consumer and Windows Mono execution. Phase 13 Backend distribution and Phase 14 Realtime remain candidates with their existing priority and scope.

The approved Phase 16 Windows scope is implemented in Unreleased source. The 2026-10-02 local sample consistency correction passed Server 106/106, root and Built-in/Legacy Unity EditMode 248/248 each, and 54/54 state/action checks in each of root Play Mode, consumer Play Mode and Windows Mono player, plus personal memory/isolation/reset and process cleanup. Local action response formatting is opt-in and precedes memory/presentation; the sample shares one gate/alarm while memory stays per NPC. Preserve current-state priority and bounded quoted user recall in prompts. See docs/PHASE16_VALIDATION.md for exact source evidence, the separately retained initial validation, measured frame stalls and remaining model/platform limits. This does not authorize a version change, tag or public release.

Phase 12 is implemented in Unreleased source with separate current-source evidence in docs/PHASE12_VALIDATION.md: Server 106/106; root and Built-in/Legacy EditMode 303/303 each; Advanced real-model checks 50/50 in two root Play sessions, consumer and Windows Mono; V4 fake HTTP 31/31; existing local sample checks 54/54 in each environment plus memory and process shutdown. Preserve Basic/Advanced switching, signal-only triggers, atomic pre/post-state evaluation, independent reset lifetimes, final authorization and opt-in trace privacy. Intent classification remains probabilistic. No version change or publication is authorized.

The Phase 15 boundary excludes:

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
- Generic variables, scores, relationship systems, persistent action state, or long-term memory
- Bundling or downloading a local language model, inference runtime, tokenizer, or native plugin
- Moving or recreating any published package tag, or publishing a later release without exact-commit approval

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
- Keep character canon and reusable lore in consumer-owned assets; capture mutable game state through `INpcContextProvider` immediately before a turn.
- Treat grounding as a bounded immutable snapshot. Do not let the backend mutate Unity state or use context as action authorization.
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
- Phase 15 grounding, context revisions, lore, beliefs, and provider facts must not be written to application logs or session history.
- Unity stores only opaque voice preset IDs; OpenAI voice names, instructions, and speed remain in the server preset file.
- Microphone audio and transcription text must not be logged or persisted; Phase 7 sends only bounded WAV to the loopback backend.

## Completion requirements

For every implementation task:

1. State the files to be changed.
2. Keep the change limited to the requested milestone.
3. Run available tests or compilation checks.
4. Never claim Unity compilation succeeded unless Unity or an equivalent compiler was actually run.
5. Summarize changed files, verification performed, and remaining risks.
