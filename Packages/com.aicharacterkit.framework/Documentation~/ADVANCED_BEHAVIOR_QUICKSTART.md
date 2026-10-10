# Advanced Behavior Quick Start — Phase 12 / Unreleased

Advanced Behavior adds per-NPC variables and structured gates to the existing conversation action pipeline. It is optional current-source functionality, not part of the immutable public `v0.4.0` package tag. Basic NPCs keep their existing action profile and behavior.

## Try the sample

Import the **AI NPC Prototypes** sample from the current package and choose **Tools → AI Character Kit → Samples → Create Advanced Relationship NPC**. The Editor generates the scene and consumer-owned profiles using Unity APIs. The initial scene uses Mock and needs no server or model.

Mira and Toma share one gate, alarm and permission flag, while their affinity and greeted variables are independent. Mock matches the normalized examples exactly:

| Input | Authored effect |
| --- | --- |
| `정말 친절하네요.` | affinity +10 |
| `불친절하군요.` | affinity −10 |
| `안녕하세요.` | greeted = true; special greeting succeeds once |
| `문을 열어 주세요.` | open gate only if affinity ≥60, permission is true, alarm is false, and final consumer authorization succeeds |

Affinity starts at 40 and clamps to 0–100. Opening the gate has a five-second cooldown. Closing the shared gate does not erase either NPC's score or action execution records. The panel shows actual action results separately from the generated reply.

## Author an NPC

1. Open **Tools → AI Character Kit → Character Builder** and select a consumer Scene object or Prefab asset, CharacterProfile and presentation component.
2. Enable **Configure Actions**, choose **Advanced**, and create a Behavior Profile. Basic profiles can be copied with **Import Basic into new unsaved draft**; the original is unchanged.
3. Define bool or bounded number variables and their defaults. Enable **Send as grounding** only for values that the NPC may know. Grounding uses the variable ID as its fact ID; avoid IDs already used by lore or other providers.
4. Define conversation signals with IDs, natural-language meaning and exact Mock examples. A signal can change state without an action handler.
5. Add state-change rules and action rules. Each rule requires a matched signal. Conditions compare a variable or external value with a typed constant. Add an ALL/ANY/NOT root and child conditions using the tree controls. The node labels identify their parent. Empty conditions mean true for that matched signal.
6. Select one consumer handler per required action ID. Declare typed external values and connect explicit `INpcBehaviorValueProvider` components under the target hierarchy. These values remain consumer-owned and read-only to the evaluator.
7. Set synthetic variables, external values and matched signals in **Advanced preview**. It evaluates one turn with simulated allowed handlers; it never calls live providers, actual handlers, networking or inference. The trace shows deltas and rule decisions.
8. Save the Behavior Profile and apply to the Scene or Prefab. Apply/reapply and Awake validate configuration only. Prefab references must stay within the Prefab hierarchy. Runtime definitions are copied at initialization; recreate the NPC to adopt later authoring changes.

Mock, BackendActions (V3), BackendContext (V4) and LocalInference support Advanced. V1/V2 modes do not carry semantic triggers and are rejected for this configuration. V3 runs the rules but cannot send variable grounding. For V4 or LocalInference enable grounding; Builder adds one `NpcBehaviorContextProvider` for selected variables. LocalInference also requires an explicit or parent `LocalInferenceRuntime` with the existing external installation profile.

## Consumer interfaces

`AiCharacterKit.Behavior` is Unity-independent and references Core only. `NpcBehaviorProfile` lives in `AiCharacterKit.Unity.Behavior`; `NpcActionCoordinator` remains in `AiCharacterKit.Unity.Actions`. A typed provider may be an ordinary MonoBehaviour implementing:

```csharp
public IReadOnlyDictionary<string, NpcBehaviorValue> CaptureValues()
{
    // Read current consumer-owned permission without interpreting model dialogue.
    return new Dictionary<string, NpcBehaviorValue>
    {
        ["permission"] = new NpcBehaviorValue(hasGatePermission)
    };
}
```

The snippet assumes `System.Collections.Generic`, `AiCharacterKit.Behavior`, and a consumer-owned bool `hasGatePermission`. Return exactly the declared external IDs across the selected providers. Missing, duplicate, unknown or mistyped values, null collections and exceptions stop that turn's Advanced processing before any changes. Original exception text is not displayed. A later successful capture recovers on the same NPC.

Game code can read `BehaviorState` and call `TrySetBoolean`, `TrySetNumber`, `TryAddNumber` or `TryResetBehaviorState` on the coordinator. Inspect each returned bool and error token. Writes/reset return `busy` while the NPC owns a conversation, memory reset or action. Numeric assignments and deltas clamp to the authored bounds; NaN and Infinity are rejected. Numbers use finite double values and exact equality; prefer threshold comparisons for scores accumulated with fractional changes.

Actual game effects still use `INpcActionHandler`. `CanExecute` must check authoritative state immediately before execution. A passed rule or an AI trigger never grants game permission. The framework neither invokes arbitrary methods nor accepts model-generated score deltas or action parameters.

## Order, lifetime and diagnostics

For a validated matched response, providers are captured once immediately before evaluation. All change-rule conditions read the same pre-change state. Matching changes apply in declaration order and commit together. Action conditions read the committed state; highest priority wins, with declaration order breaking ties. A rejected or failed selected action never falls through to another handler. Rules sharing an action ID must use the same cooldown/once policy and share its successful execution record.

Generation failures, invalid trigger IDs and cancellation before commit change nothing. Once variable changes commit, a later action rejection, failure or cancellation retains them. Cooldown and once are recorded only after successful action completion. Already applied consumer game effects are not rolled back. There are no automatic retries or continuous/tick-driven actions.

Current selected variables enter the next request's immutable grounding snapshot. A change caused by that response affects the next generated reply, while its action gate uses the changed value in the same turn. No second model call is made. Normal model dialogue remains probabilistic. The local sample opts into `INpcActionResponseFormatter`, so actual action receipts are finalized before presentation and memory; V3/V4 server memory and ordinary Basic presentation ordering are unchanged.

Semantic trigger classification is also probabilistic. State changes are deterministic for the IDs returned, so an incorrect praise signal can still change affinity. Author each signal narrowly around the current message, include clear exclusions, and classify repeated requests independently of whether an action is currently permitted. The sample includes such guidance; arbitrary wording or another model still requires your own evaluation.

| Operation | Variables / cooldown / once | Conversation memory |
| --- | --- | --- |
| Memory reset | Retained | Cleared if supported by the selected client |
| Disable / re-enable NPC | Retained | Existing client behavior retained |
| `TryResetBehaviorState` | Defaults; gates and trace cleared | Retained |
| Destroy / recreate or new Play session | Defaults | New client lifetime |

There is no save/load or cross-player relationship service. ScriptableObjects store definitions only. Runtime trace is opt-in, keeps the last 32 turns in memory, and is cleared on disable of trace or behavior reset. Inspect it in the coordinator Inspector during Play, the sample panel, or `LastBehaviorTrace`/`BehaviorTraces`. It contains IDs, typed deltas and fixed reason codes, not dialogue, grounding text or original exceptions; it is never automatically logged or persisted.

Limits: 32 variables, 32 external values, 16 signals, 64 total rules, 16 changes per rule, and 32 condition nodes with depth at most four. Existing V4 fact and serialized-request limits still apply, including deterministic grounding omission reporting and the 512 KiB UTF-8 request budget.
