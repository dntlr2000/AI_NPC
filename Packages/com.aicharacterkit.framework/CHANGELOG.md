# Changelog

All notable changes to this package are documented here.

## [0.3.0] - Unreleased

- Added consumer-owned `NpcActionProfile` data and `INpcActionHandler` extension boundaries.
- Added deterministic Mock trigger matching and one-action routing with final consumer authorization.
- Added action-aware V3 session transport, loopback Unity adapter, and reference Backend endpoints.
- Extended Character Builder with action profile authoring, handler wiring, validation, and preview.
- Added an importable network-free conversation-action sample and regression coverage.
- Split sample action handlers into persistent Unity `MonoScript` files so generated Scene references survive an Editor restart.
- Added an end-to-end Conversation Actions Quick Start with a compile-ready handler, Builder field mapping, Mock/V3 verification, limits, and troubleshooting.
- Preserved existing Mock, V1, V2, TTS, STT, and published `v0.2.0` paths.

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
