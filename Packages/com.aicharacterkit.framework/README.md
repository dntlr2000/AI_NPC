# AI Character Kit

AI Character Kit is a reusable Unity 6 framework for structured NPC dialogue and consumer-owned game actions. It includes a deterministic offline Mock path, versioned transport contracts, optional loopback-backend adapters, bounded sessions, TTS, STT, and replaceable presentation and action boundaries.

## Install

The latest published release remains `v0.2.0`. Choose **Window > Package Management > Package Manager**, click **+**, select **Install package from git URL**, and enter:

```text
https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.2.0
```

To evaluate the completed but unreleased Phase 11 source, select **Install package from disk** and choose this `0.3.0` package's `package.json`. The supported baseline is Unity `6000.5`; uGUI is installed as the only feature package dependency. URP and the Input System are optional.

Import **AI NPC Prototypes** from the package's Samples tab. Then run **Tools > AI Character Kit > Repair All Sample Scenes** so the imported assets match the active Legacy or Input System backend. **Import or Repair AI NPC Prototypes** combines those steps for local UPM installations.

## Build a character

Open **Tools > AI Character Kit > Character Builder** to create or edit a consumer-owned `CharacterProfile`, preview deterministic Mock output, and connect an existing Scene GameObject or writable Prefab to a consumer `INpcPresentationDriver`. The tool can also connect existing package uGUI views, configure optional TTS with an opaque `NpcVoiceProfile` preset ID, and author optional natural-language trigger-to-action bindings.

For actions, create an `NpcActionProfile`, assign one consumer MonoBehaviour implementing `INpcActionHandler` for every configured `actionId`, and use Mock mode to verify the exact example input without a network. Backend Actions mode uses V3 to return only matched configured trigger IDs; Unity still selects at most one action and the handler's `CanExecute` makes the final game-state decision.

Follow the [Conversation Actions Quick Start](Documentation~/ACTIONS_QUICKSTART.md) for a compile-ready handler, exact Character Builder fields, offline and live V3 verification, and troubleshooting.

The builder never generates a game model, UI, presentation implementation, action implementation, or Prefab. Reapplying updates only the displayed Kit references and settings; it does not remove consumer components or assets. All created profiles remain under a user-selected `Assets/` folder.

## Start with Mock

1. Create a profile through **Assets > Create > AI Character Kit > Character Profile**.
2. Add `NpcConversationBehaviour` to an NPC GameObject.
3. Assign the profile, an input view, and a component implementing `INpcPresentationDriver`.
4. Keep the mode set to `Mock` for deterministic, network-free development.

The package root is treated as read-only. Editor automation writes only to an imported sample location or `Assets/AI Character Kit/Samples`. When versioned sample folders coexist, automation selects the sample matching the installed package version.

## Run package tests

Add `com.aicharacterkit.framework` to the consumer manifest's `testables` array and install Unity Test Framework `1.7.0`. Import and repair **AI NPC Prototypes** before running the full EditMode suite because scene-configuration tests intentionally verify imported consumer assets.

## Backend boundary

The UPM package never contains credentials or calls OpenAI directly. Backend, memory, action-aware V3, TTS, and STT modes require a compatible loopback service. The repository's `server/` directory is reference source at the matching repository revision; it remains outside the Unity package and is not published as an npm package.

See [Documentation](Documentation~/index.md) for architecture, lifecycle, migration, and contract references.

## License

AI Character Kit is available under the [MIT License](LICENSE.md).
