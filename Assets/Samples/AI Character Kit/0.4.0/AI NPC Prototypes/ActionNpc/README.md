# Conversation Action Prototype

After importing **AI NPC Prototypes**, run **Tools > AI Character Kit > Samples > Create Conversation Action Prototype**. The Editor API creates `ActionNpcPrototype.unity`, its `CharacterProfile`, and its `NpcActionProfile` under the imported sample folder.

- Enter `hello` to match `greet_player` and execute `wave_to_player` immediately.
- Enter `open the gate` to match `request_open_gate`. It is rejected by `SampleGuardedActionHandler.CanExecute` while **Gate Unlocked** is disabled.
- Enable **Gate Unlocked** on the handler and retry to execute `open_gate`.

The Mock path performs normalized exact example matching; it does not claim natural-language understanding. Use **BackendActions (V3)** for semantic trigger evaluation. The handlers are sample-owned consumer code: new actions require another `INpcActionHandler` implementation and profile binding, not a framework or backend schema change.

Continue with the [Conversation Actions Quick Start](<../../../Documentation~/ACTIONS_QUICKSTART.md>) to build a new consumer-owned action NPC from scratch.
