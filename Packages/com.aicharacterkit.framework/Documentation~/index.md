# AI Character Kit Documentation

AI Character Kit separates provider-neutral dialogue, request-time grounding, conversation actions, speech, and transcription coordination from Unity adapters and the external backend.

## Contents

- [Installation and lifecycle](INSTALLATION.md)
- [Architecture and extension boundaries](ARCHITECTURE.md)
- [Character Builder](CHARACTER_BUILDER.md)
- [Runtime context and lore quick start](GROUNDING_QUICKSTART.md)
- [Conversation actions quick start](ACTIONS_QUICKSTART.md)
- [Advanced Behavior quick start (Unreleased)](ADVANCED_BEHAVIOR_QUICKSTART.md)
- [Phase 17 two-stage API setup and limits (Unreleased)](INFERENCE_RELIABILITY.md)
- [Conversation contract V1](CONTRACT_V1.md)
- [Conversation contract V2](CONTRACT_V2.md)
- [Conversation actions contract V3](CONTRACT_V3.md)
- [Context-grounded conversation contract V4](CONTRACT_V4.md)
- [Speech contract V1](SPEECH_CONTRACT_V1.md)
- [Transcription contract V1](TRANSCRIPTION_CONTRACT_V1.md)

Release `v0.4.0` supports tagged Git repository subfolder, local disk, and embedded installation. It includes the Phase 11 conversation-action pipeline and Phase 15 character canon, reusable lore, live game-state providers, and V4 grounding. It was published on 2026-09-04 from commit `027b61f053c620d4e5202830b18692a6d45d3458`; its tag is immutable. Current-source Unreleased work and any later release remain separate. Registry publishing, Backend packaging, offline model distribution, and remote deployment remain separate future milestones.
