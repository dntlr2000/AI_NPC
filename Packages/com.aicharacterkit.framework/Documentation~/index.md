# AI Character Kit Documentation

AI Character Kit separates provider-neutral dialogue, request-time grounding, conversation actions, speech, and transcription coordination from Unity adapters and the external backend.

## Contents

- [Installation and lifecycle](INSTALLATION.md)
- [Architecture and extension boundaries](ARCHITECTURE.md)
- [Character Builder](CHARACTER_BUILDER.md)
- [Runtime context and lore quick start](GROUNDING_QUICKSTART.md)
- [Conversation actions quick start](ACTIONS_QUICKSTART.md)
- [Conversation contract V1](CONTRACT_V1.md)
- [Conversation contract V2](CONTRACT_V2.md)
- [Conversation actions contract V3](CONTRACT_V3.md)
- [Context-grounded conversation contract V4](CONTRACT_V4.md)
- [Speech contract V1](SPEECH_CONTRACT_V1.md)
- [Transcription contract V1](TRANSCRIPTION_CONTRACT_V1.md)

Published release `v0.3.0` supports tagged Git repository subfolder, local disk, and embedded installation and includes the Phase 11 conversation-action pipeline. The current `0.4.0` source candidate adds Phase 15 character canon, reusable lore, live game-state providers, and V4 grounding. No `v0.4.0` tag exists until separate release approval. Registry publishing, Backend packaging, offline model distribution, and remote deployment remain separate future milestones.
