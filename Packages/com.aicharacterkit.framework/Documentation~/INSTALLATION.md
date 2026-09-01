# Installation and Lifecycle

## Requirements

- Unity `6000.5` or a compatible later Unity 6 editor
- uGUI `2.5.0`
- Test Framework `1.7.0` only when running package tests

URP and the Input System are not required. Audio, JSON serialization, and UnityWebRequest are declared Unity module dependencies.

## Local installation

Use Package Manager's **Install package from disk** action and select `Packages/com.aicharacterkit.framework/package.json`, or add a file dependency to the consumer's `Packages/manifest.json`:

```json
"com.aicharacterkit.framework": "file:E:/path/to/repository/Packages/com.aicharacterkit.framework"
```

Do not keep a raw `Assets/AiCharacterKit` copy and the UPM package installed together. The editor resolver rejects duplicate installations rather than choosing one silently.

## Samples

Import **AI NPC Prototypes** from the Package Manager Samples tab. Imported samples are writable under `Assets/Samples/AI Character Kit/0.2.0/AI NPC Prototypes`. Run **Tools > AI Character Kit > Repair All Sample Scenes** after import or after changing the active input backend.

## Upgrade and removal

Close Unity before replacing a local package folder. Preserve user-owned profiles, presentation drivers, and imported samples under `Assets`; package removal does not own those assets. Import the new version's sample after an upgrade. If older versioned sample folders coexist, Editor automation selects the currently installed package version; archive or remove older consumer-owned copies only when they are no longer needed. Let Unity recompile, repair the sample scenes, and run project tests before committing the lock file.

The reference Node backend is a separate repository component. Removing the package does not remove or modify server files.

## Package tests

Install Test Framework `1.7.0`, add `com.aicharacterkit.framework` to the project's `testables` array, and import/repair **AI NPC Prototypes**. The full EditMode suite includes fixture, architecture, and imported scene-configuration checks, so it expects the sample to exist under `Assets`.
