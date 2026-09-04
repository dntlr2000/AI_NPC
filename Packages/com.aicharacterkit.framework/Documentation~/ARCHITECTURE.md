# Architecture and Extension Boundaries

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

The Editor assembly may use UnityEditor APIs. Runtime assemblies must not reference UnityEditor, and the package never writes into its own installed package directory. A future offline model runner can implement `IAiConversationClient`, but no model weights, tokenizer, or native inference runtime are bundled in `0.4.0`.
