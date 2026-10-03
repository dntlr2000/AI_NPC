# Changelog

All notable changes to this package are documented here.

## [Unreleased]

- Corrected Local Grounded NPC action/dialogue consistency: opt-in local action result formatting precedes presentation and memory commit; both guards share one gate/alarm and display actual action status. Updated the imported sample without replacing scenes, profiles or GUIDs.
- Prioritized fresh grounding after historical dialogue in local prompts and added finalization, cancellation and memory regressions. Ordinary model dialogue remains probabilistic; authored gate action receipts cover the sample's Korean/English outcomes.
- Verified the 2026-10-02 correction with Server 106/106, root and Built-in/Legacy EditMode 248/248 each, and 54/54 state/action checks in each of root, consumer and Windows Mono actual-model runs, plus personal recall, isolation, reset and process cleanup. These results supersede the earlier sample-consistency coverage, not its separately recorded performance measurements.
- Added optional Windows local NPC inference using external pinned Gemma/llama.cpp installations, shared process hosts, separate bounded NPC memory, strict V4/JSON validation, token budgeting and cancellation-safe FIFO admission.
- Added LocalInference mode, profile/setup tooling, static Character Builder Scene/Prefab wiring, a two-NPC local sample and installation/process regressions. Models, binaries and new dependencies are not bundled; package version remains 0.4.0.
- Documented the approved Phase 16 scope and current-source verification separately from public release results.
- Verified Server 106/106, Unity EditMode 240/240 in both root and Built-in/Legacy consumer, repeated Play Mode, and Windows x64 Mono real-model checks 35/35. Basic 16-case player p95 was 3.199 s; preparation was 67.734 s. Measured frame stalls and platform limits remain documented.
- Aligned V4 conversation HTTP requests with a 512 KiB serialized UTF-8 body budget and added Unity rejection before HTTP, preserving the existing per-field limits and other routes.
- Added `NpcContextCoordinator.TryValidateConfiguration` for authored data and provider references without live capture; Builder and Awake use it while the existing live validation APIs retain their behavior.
- Added request-size, deferred provider readiness, Scene/Prefab authoring, and recovery regressions.
- Corrected roadmap and release records to reflect the published `v0.4.0` commit; these stabilization changes are not included in that immutable tag.

## [0.4.0] - 2026-09-04

- Added immutable bounded per-turn grounding models and deterministic context revisions in Core.
- Extended `CharacterProfile` with background, goals/values, behavioral rules, and additional dialogue examples.
- Added reusable `NpcLoreProfile` assets and consumer `INpcContextProvider` runtime state adapters.
- Added V4 DTO, validation, mapping, JSON codec, session client, and loopback Backend routes.
- Extended Character Builder with canon/lore authoring, provider wiring, and authored grounding preview.
- Added an Editor-generated Grounded Guard sample and V4 contract, quick-start, and regression coverage.
- Kept grounding out of Backend logs and session history while preserving V1–V3, Mock, actions, TTS, and STT.
- Recorded offline local inference as a separate optional future milestone; no model runtime or weights are bundled.

## [0.3.0] - 2026-09-02

- Added consumer-owned `NpcActionProfile` data and `INpcActionHandler` extension boundaries.
- Added deterministic Mock trigger matching and one-action routing with final consumer authorization.
- Added action-aware V3 session transport, loopback Unity adapter, and reference Backend endpoints.
- Extended Character Builder with action profile authoring, handler wiring, validation, and preview.
- Added an importable network-free conversation-action sample and regression coverage.
- Split sample action handlers into persistent Unity `MonoScript` files so generated Scene references survive an Editor restart.
- Added an end-to-end Conversation Actions Quick Start with a compile-ready handler, Builder field mapping, Mock/V3 verification, limits, and troubleshooting.
- Made V3 contract fixture checks deterministic across LF and CRLF Git checkouts.
- Preserved compatibility with existing action-free Mock, V1, V2, TTS, STT, and `v0.2.0` usage paths.

## [0.2.0] - 2026-09-02

- Added an Editor-only Character Builder for consumer-owned profile authoring and deterministic Mock previews.
- Added non-destructive Scene GameObject and regular/variant Prefab composition for existing presentation drivers.
- Added optional existing uGUI view wiring and provider-neutral TTS profile/component setup.
- Added MIT licensing, public Git URL installation guidance, and release metadata.
- Preserved all Runtime APIs, transport contracts, dependencies, and existing sample content from `0.1.0`.

## [0.1.0] - 2026-09-01

- Added the first local UPM package checkpoint.
- Preserved the Phase 1–8 runtime APIs, assembly names, namespaces, and asset GUIDs.
- Added a consolidated importable sample containing six prototype scenes.
- Added package-aware Editor paths with writable imported/generated sample output.
- Preferred the installed package version when older imported sample folders coexist.
- Kept URP, the Input System, Test Framework, and the reference backend outside production package dependencies.
