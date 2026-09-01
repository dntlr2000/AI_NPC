# Architecture and Extension Boundaries

```text
CharacterProfile + user text
        -> NpcAIController
        -> IAiConversationClient
             -> Mock or Unity backend adapter
        -> AiNpcResponse
        -> INpcPresentationDriver
```

`AiCharacterKit.Core`, `AiCharacterKit.Transport`, `AiCharacterKit.Speech`, and `AiCharacterKit.Transcription` are provider-neutral assemblies without UnityEngine references. JSON, UnityWebRequest, uGUI, microphone capture, and audio playback stay in Unity adapter assemblies. OpenAI SDK usage and credentials stay outside Unity in a backend service.

Use `CharacterProfile` assets for character-specific data. Implement `INpcPresentationDriver` in the consumer for Animator, Sprite, UI Toolkit, or project-specific presentation. Implement the existing client/driver interfaces to replace dialogue, speech, transcription, capture, or playback providers without modifying Core.

V1 dialogue is stateless. V2 adds an opaque component-owned session ID and explicit reset. Session history remains bounded and process-local in the reference backend. TTS and push-to-talk STT are optional adapters; neither changes the core dialogue contract.

The Editor assembly may use UnityEditor APIs. Runtime assemblies must not reference UnityEditor, and the package never writes into its own installed package directory.
