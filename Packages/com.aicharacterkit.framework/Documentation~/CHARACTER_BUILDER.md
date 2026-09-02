# Character Builder

Open **Tools > AI Character Kit > Character Builder** to author character data and connect existing consumer objects without changing Runtime contracts.

## Profile and preview

Create a new `CharacterProfile` under the default `Assets/AI Character Kit/Characters` folder or choose another writable `Assets/` location. Existing consumer profiles can be loaded and saved explicitly. Duplicate character IDs are warnings because the same identity may intentionally appear in multiple imported versions or configurations.

The Mock preview uses the deterministic local conversation client with zero latency. It displays dialogue, emotion, gesture, matched triggers, and the selected action without entering Play Mode, calling a backend, or synthesizing speech.

## Scene and Prefab configuration

Select a loaded Scene GameObject or a regular/variant Prefab asset, then choose an existing MonoBehaviour that implements `INpcPresentationDriver`. The builder adds or reuses one `NpcConversationBehaviour` and configures Mock, stateless Backend, session Backend, or Backend Actions settings. Model Prefabs and package-owned Prefabs are read-only.

Existing `NpcTextInputView`, `NpcSessionControlView`, and `NpcSpeechControlView` components can be connected optionally. Their consumer-created uGUI control references must already be complete. Scene prefab instances receive local overrides; the source Prefab is never changed implicitly.

## Optional conversation actions

Enable **Conversation Actions**, then create or select a consumer-owned `NpcActionProfile`. Each binding contains a lower `snake_case` trigger ID, a natural-language condition, one exact Mock example, an opaque action ID, and priority. Select one existing MonoBehaviour implementing `INpcActionHandler` for every action ID before applying.

Mock matching normalizes case and whitespace but otherwise requires the example text to match exactly. Backend Actions sends only trigger IDs and condition descriptions through V3; action IDs and Unity object references never leave Unity. When multiple triggers match, the highest priority wins and declaration order breaks ties. The selected handler's `CanExecute` is always the final authorization check.

Implement `INpcActionHandler` directly for pure adapters or derive a consumer MonoBehaviour from `NpcActionHandlerBase`. The builder wires handlers but never generates gameplay code, method names, parameters, or Scene targets. Reapplying the same configuration reuses the coordinator and does not duplicate or delete consumer components.

## Optional TTS

Enable TTS and select or create an `NpcVoiceProfile` containing only an opaque backend preset ID. The builder creates or reuses the speech decorator, output, PCM playback driver, and a dedicated AudioSource. Disabling TTS reconnects the visual driver but does not delete speech components or voice assets.

The builder never stores API keys, provider voice names, personality text in logs, or remote endpoints. Current networking remains restricted to explicit loopback URLs.
