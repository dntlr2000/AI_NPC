# AI Character Kit

AI Character Kit is a reusable Unity 6 framework for structured NPC dialogue. It includes a deterministic offline Mock path, versioned transport contracts, optional loopback-backend adapters, bounded sessions, TTS, STT, and replaceable presentation boundaries.

## Install

For local development, choose **Window > Package Management > Package Manager**, click **+**, select **Install package from disk**, and choose this package's `package.json`. The supported baseline is Unity `6000.5`; uGUI is installed as the only feature package dependency. URP and the Input System are optional.

Import **AI NPC Prototypes** from the package's Samples tab. Then run **Tools > AI Character Kit > Repair All Sample Scenes** so the imported assets match the active Legacy or Input System backend. **Import or Repair AI NPC Prototypes** combines those steps for local UPM installations.

## Start with Mock

1. Create a profile through **Assets > Create > AI Character Kit > Character Profile**.
2. Add `NpcConversationBehaviour` to an NPC GameObject.
3. Assign the profile, an input view, and a component implementing `INpcPresentationDriver`.
4. Keep the mode set to `Mock` for deterministic, network-free development.

The package root is treated as read-only. Editor automation writes only to an imported sample location or `Assets/AI Character Kit/Samples`. When versioned sample folders coexist, automation selects the sample matching the installed package version.

## Run package tests

Add `com.aicharacterkit.framework` to the consumer manifest's `testables` array and install Unity Test Framework `1.7.0`. Import and repair **AI NPC Prototypes** before running the full EditMode suite because scene-configuration tests intentionally verify imported consumer assets.

## Backend boundary

The UPM package never contains credentials or calls OpenAI directly. Backend, memory, TTS, and STT modes require a compatible loopback service. The repository's `server/` directory is the reference implementation and remains outside the Unity package.

See [Documentation](Documentation~/index.md) for architecture, lifecycle, migration, and contract references.
