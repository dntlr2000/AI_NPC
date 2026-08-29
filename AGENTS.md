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
- `docs/`: requirements, architecture, plans, and decisions; see `docs/ROADMAP.md`, `docs/PHASE5_PLAN.md`, `docs/CONTRACT_V1.md`, and `docs/CONTRACT_V2.md`
- `server/`: Node.js 24, TypeScript, Fastify, and OpenAI SDK local backend for the V1 stateless and V2 session paths

The Unity project root is the repository root. Do not assume a separate `unity/` directory.

## Source of truth

- Read `ProjectSettings/ProjectVersion.txt` before assuming the Unity version.
- Read `Packages/manifest.json` and `Packages/packages-lock.json` before assuming installed packages.
- Do not upgrade Unity or package versions unless explicitly requested.

## Current milestone

Phase 5 is complete on `main` at `d8ae5f7 (Phase5)`, including automated regression coverage and manual live-memory Play Mode verification. Phase 6 has not been planned or approved yet, so preserve this checkpoint until a new plan is accepted.

The completed Phase 5 checkpoint includes:

- The unchanged stateless V1 and Mock paths
- A V2 contract with `/v2/npc/respond` and `/v2/npc/sessions/reset`
- Bounded process-memory sessions with turn, byte, TTL, count, and concurrency limits
- A component-lifetime Unity session client and shared send/reset operation gate
- An Editor-generated two-character `MemoryNpcPrototype` sample scene
- Server tests and full Unity EditMode regression coverage

Until the next milestone is approved, do not add:

- Persistent or long-term memory
- TTS
- STT
- Realtime voice
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

## Completion requirements

For every implementation task:

1. State the files to be changed.
2. Keep the change limited to the requested milestone.
3. Run available tests or compilation checks.
4. Never claim Unity compilation succeeded unless Unity or an equivalent compiler was actually run.
5. Summarize changed files, verification performed, and remaining risks.
