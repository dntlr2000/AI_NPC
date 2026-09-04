# Installation and Lifecycle

## Requirements

- Unity `6000.5` or a compatible later Unity 6 editor
- uGUI `2.5.0`
- Test Framework `1.7.0` only when running package tests

URP and the Input System are not required. Audio, JSON serialization, and UnityWebRequest are declared Unity module dependencies.

## Tagged Git installation

The latest published release is `v0.3.0`. Select **Install package from git URL** in Package Manager and enter:

```text
https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.3.0
```

The equivalent consumer manifest entry is:

```json
"com.aicharacterkit.framework": "https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.3.0"
```

Keep the `path` query before the `#v0.3.0` revision. Pin a release tag instead of the default branch so the consumer lock file resolves an immutable source revision.

## Local installation

Use Package Manager's **Install package from disk** action and select `Packages/com.aicharacterkit.framework/package.json`, or add a file dependency to the consumer's `Packages/manifest.json`. Use this mode for local source development; tagged Git installation is the reproducible release path.

```json
"com.aicharacterkit.framework": "file:E:/path/to/repository/Packages/com.aicharacterkit.framework"
```

Do not keep a raw `Assets/AiCharacterKit` copy and the UPM package installed together. The editor resolver rejects duplicate installations rather than choosing one silently.

## Samples

Import **AI NPC Prototypes** from the Package Manager Samples tab. Imported samples are writable under `Assets/Samples/AI Character Kit/<installed-version>/AI NPC Prototypes`. Run **Tools > AI Character Kit > Repair All Sample Scenes** after import or after changing the active input backend. In `0.3.0` and later, run **Tools > AI Character Kit > Samples > Create Conversation Action Prototype** once after import to generate the action Scene through Editor APIs. In the local `0.4.0` source candidate, run **Create Grounded Guard Prototype** to generate the V4 context/lore Scene as well.

After verifying the generated Action Scene, follow the [Conversation Actions Quick Start](ACTIONS_QUICKSTART.md) to implement and connect a consumer-owned action handler. For V4 canon, lore, and live state setup, follow the [Runtime Context and Lore Quick Start](GROUNDING_QUICKSTART.md).

## Upgrade and removal

For a Git installation, change only the URL revision to the desired immutable release tag. Close Unity before replacing a local package folder. Preserve user-owned profiles, presentation drivers, and imported samples under `Assets`; package removal does not own those assets. Import the new version's sample after an upgrade. If older versioned sample folders coexist, Editor automation selects the currently installed package version; archive or remove older consumer-owned copies only when they are no longer needed. Let Unity recompile, repair the sample scenes, and run project tests before committing the lock file.

The reference Node backend is source in the same repository but is not part of the UPM package or an npm release. Check out the matching repository tag and follow `server/README.md` when optional Backend modes are required. Removing the Unity package does not remove or modify server files.

## Package tests

Install Test Framework `1.7.0`, add `com.aicharacterkit.framework` to the project's `testables` array, import/repair **AI NPC Prototypes**, and generate the Action and Grounded Guard scenes from their sample menu entries. The full EditMode suite includes fixture, architecture, and imported scene-configuration checks, so it expects those writable sample assets under `Assets`.
