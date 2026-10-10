# Architecture and Extension Boundaries

Current Unreleased source adds `AiCharacterKit.Behavior` (Core-only) and the existing pure LocalInference client. In Advanced mode, `NpcActionCoordinator` reserves instance state during a conversation, validates returned signal IDs, captures typed consumer values, commits ordered state changes, and selects one action through the shared Core executor. The existing `INpcActionHandler.CanExecute` remains authoritative. Basic uses its original router. See [Advanced Behavior](ADVANCED_BEHAVIOR_QUICKSTART.md) for commit, cancellation, trace and reset boundaries.

```text
CharacterProfile + user text
        + optional NpcLoreProfile
        + INpcContextProvider current facts
        -> bounded NpcGroundingSnapshot
        -> NpcAIController
        -> IAiConversationClient
             -> Mock or Unity backend adapter
        -> AiNpcResponse
             -> INpcPresentationDriver
             -> optional INpcTurnObserver
                  -> deterministic NpcActionRouter
                  -> INpcActionHandler.CanExecute
                  -> consumer-owned action
```

`AiCharacterKit.Core`, `AiCharacterKit.Transport`, `AiCharacterKit.Speech`, and `AiCharacterKit.Transcription` are provider-neutral assemblies without UnityEngine references. JSON, UnityWebRequest, uGUI, microphone capture, and audio playback stay in Unity adapter assemblies. OpenAI SDK usage and credentials stay outside Unity in a backend service.

Use `CharacterProfile` assets for character-specific data. Implement `INpcPresentationDriver` in the consumer for Animator, Sprite, UI Toolkit, or project-specific presentation. Implement the existing client/driver interfaces to replace dialogue, speech, transcription, capture, or playback providers without modifying Core.

V1 dialogue is stateless. V2 adds an opaque component-owned session ID and explicit reset. V3 preserves V2 session semantics and adds a bounded natural-language trigger snapshot plus matched trigger IDs. V4 preserves both features and adds a bounded immutable grounding snapshot captured immediately before each request. Session history remains bounded and process-local in the reference backend. Grounding is used only for the current generation and is not stored in history or logs. TTS and push-to-talk STT are optional adapters; neither changes the core dialogue contract.

Stable identity, background, goals, rules, and dialogue examples belong to consumer-owned `CharacterProfile` assets. Reusable objective lore and subjective beliefs belong to `NpcLoreProfile`. Mutable world state is read through `INpcContextProvider`; `NpcContextCoordinator` copies and deterministically trims those facts into Core data. Providers should be read-only snapshots and must not perform game actions.

Gameplay actions are deliberately separate from presentation. `NpcActionProfile` maps authored trigger IDs to opaque action IDs and priority. A model may only return IDs present in the current request; Unity rejects unknown IDs, selects at most one deterministically, and lets the consumer handler authorize real game state through `CanExecute`. The framework does not invoke methods through reflection or generate action parameters.

The Editor assembly may use UnityEditor APIs. Runtime assemblies must not reference UnityEditor, and the package never writes into its own installed package directory. Unreleased LocalInference borrows an external Windows runner; process, HTTP and JSON stay in Unity. No model weights, tokenizer or native runtime are bundled. Local opt-in action formatting executes the coordinator once before presentation and memory; other clients retain the existing observer ordering.
