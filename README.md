# AI Character Kit

AI Character Kit is a reusable Unity 6 framework for structured AI-driven NPCs. It separates character data, conversation providers, presentation, speech, and transcription behind replaceable boundaries so game-specific models and UI remain consumer-owned.

Version `0.2.0` includes deterministic offline Mock dialogue, versioned V1/V2 transport contracts, bounded process-local sessions, optional TTS and push-to-talk STT adapters, samples, and the Character Builder Editor tool.

## Requirements

- Unity `6000.5` or a compatible later Unity 6 editor
- Git available on `PATH` for Git URL installation
- Node.js 24 only when running the optional reference backend

The package declares uGUI `2.5.0` and required Unity modules. URP and the Input System are not required.

## Install the Unity package

In **Window > Package Management > Package Manager**, choose **Install package from git URL** and enter:

```text
https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.2.0
```

Alternatively, add the same URL to the consumer project's `Packages/manifest.json`:

```json
"com.aicharacterkit.framework": "https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.2.0"
```

Import **AI NPC Prototypes** from the package Samples tab, then run **Tools > AI Character Kit > Repair All Sample Scenes**. Open **Tools > AI Character Kit > Character Builder** to create a consumer-owned profile, preview Mock output, and connect an existing Scene object or writable Prefab.

See the [reuse guide](docs/REUSE_GUIDE.md) and [package documentation](Packages/com.aicharacterkit.framework/Documentation~/index.md) for lifecycle and extension guidance.

## Package and backend boundary

The installable UPM package lives at `Packages/com.aicharacterkit.framework`. It works without a network when using Mock mode and never contains API keys or calls OpenAI directly.

The `server/` directory is a separate, loopback-only reference implementation for optional conversation, memory, TTS, and STT paths. It is included as repository source but is not published as a UPM or npm package in release `0.2.0`. Remote deployment, client authentication, Realtime voice, and persistent memory remain out of scope.

## Repository layout

- `Packages/com.aicharacterkit.framework/`: reusable UPM Runtime, Editor tools, tests, samples, and documentation
- `Assets/`: consumer-owned assets used by this development project
- `server/`: optional local reference backend
- `docs/`: roadmap, contracts, implementation records, and release notes

## License

This repository is licensed under the [MIT License](LICENSE.md).
