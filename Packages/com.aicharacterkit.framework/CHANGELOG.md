# Changelog

All notable changes to this package are documented here.

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
