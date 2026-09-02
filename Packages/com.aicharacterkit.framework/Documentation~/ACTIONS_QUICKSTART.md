# Conversation Actions Quick Start

This guide creates one NPC that talks and opens a consumer-owned gate when the conversation matches an authored condition. It starts with the network-free Mock path and then shows the optional semantic V3 Backend path.

> Conversation actions require AI Character Kit `0.3.0` or later. Release `v0.2.0` does not contain the action APIs or Builder controls described here.

## Requirements

- Unity `6000.5` or a compatible later Unity 6 editor
- AI Character Kit `0.3.0` or later installed from a release tag or local disk
- A loaded Scene GameObject or writable regular/variant Prefab
- A MonoBehaviour implementing `INpcPresentationDriver`
- Optional existing `NpcTextInputView` and uGUI controls for Play Mode input

The Builder connects existing consumer objects. It does not generate a model, presentation driver, UI, gameplay action, or Prefab.

If the project does not yet have a conversational NPC, begin with the generated Action sample and replace its profile, visuals, and handlers incrementally. Building from a blank Scene also requires the normal presentation and input setup described in [Character Builder](CHARACTER_BUILDER.md).

## Verify the supplied sample first

1. Import **AI NPC Prototypes** from the Package Manager Samples tab.
2. Wait for script compilation to finish.
3. Run **Tools > AI Character Kit > Samples > Create Conversation Action Prototype**.
4. Open the generated `ActionNpcPrototype.unity` under the imported `ActionNpc/Scenes` folder.
5. In Play Mode, send `hello`. The NPC rotates and reports `wave_to_player executed`.
6. Send `open the gate`. Dialogue succeeds, but the locked handler rejects only the action.
7. Enable **Gate Unlocked** on `SampleGuardedActionHandler` and send it again. The gate indicator disappears.

If this works, the package, imported sample, input View, presentation, and action routing are ready. The [sample README](<../Samples~/AI NPC Prototypes/ActionNpc/README.md>) describes the same checks.

## 1. Implement a consumer action

Create `OpenGateActionHandler.cs` in the consumer project's `Assets/` folder. Keep a public MonoBehaviour in a source file with the same name so Unity can persist its Scene and Prefab reference.

```csharp
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Actions;
using UnityEngine;

public sealed class OpenGateActionHandler : NpcActionHandlerBase
{
    [SerializeField]
    private GameObject gate;

    [SerializeField]
    private bool gateUnlocked;

    public override string ActionId => "open_gate";

    /// <summary>
    /// Performs the final consumer-owned game-state authorization.
    /// </summary>
    public override bool CanExecute(
        NpcActionContext context,
        out string rejectionReason)
    {
        if (!base.CanExecute(context, out rejectionReason))
        {
            return false;
        }

        if (gate == null)
        {
            rejectionReason = "The gate reference is missing.";
            return false;
        }

        if (!gateUnlocked)
        {
            rejectionReason = "The gate is locked.";
            return false;
        }

        rejectionReason = string.Empty;
        return true;
    }

    /// <summary>
    /// Executes the authorized consumer effect while honoring cancellation.
    /// </summary>
    public override Task ExecuteAsync(
        NpcActionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        gate.SetActive(false);
        return Task.CompletedTask;
    }
}
```

Attach the component to the NPC or one of its children and assign the gate reference. `CanExecute` is the final authority for distance, inventory, quest, combat, target validity, or permissions. `ExecuteAsync` performs the real game effect. Do not treat model output or the user's wording as trusted game state.

`NpcActionContext` exposes the successful `Request`, `Response`, and selected `Trigger`. Use it for context, diagnostics, or consumer routing, but keep authorization in consumer-owned state. Implement `INpcActionHandler` directly instead when a Unity base class is not appropriate.

## 2. Create the character and binding

Open **Tools > AI Character Kit > Character Builder**.

1. Under **1. Character Profile**, create or select a valid `CharacterProfile`.
2. Under **3. NPC Connection**, select **Scene / Prefab Target** and **Visual Presentation**.
3. Set **Conversation Mode** to `Mock`.
4. Assign an existing **Text Input View** if the NPC uses the package uGUI input path.
5. Under **4. Optional Conversation Actions**, enable **Configure Actions**.
6. Create a new action profile with this binding:

| Builder field | Example | Meaning |
| --- | --- | --- |
| Trigger ID | `request_open_gate` | ID the Backend may return |
| Natural-language Condition | `The player asks the NPC to open the gate.` | Semantic condition sent to V3 |
| Mock Example User Text | `open the gate` | Exact normalized Mock input |
| Action ID | `open_gate` | Local handler lookup ID |
| Priority | `10` | Higher values win when multiple triggers match |

7. Select **Create Action Profile**. Profiles remain consumer-owned assets under the selected `Assets/` folder.
8. In **Handler: open_gate**, select the attached `OpenGateActionHandler`.
9. Select **Validate Configuration**, fix every blocking diagnostic, and then select **Apply to Target**.

Trigger and action IDs must be unique lower `snake_case`, start with a letter, and contain at most 64 characters. A profile supports at most 16 bindings. Conditions are limited to 512 UTF-8 bytes and Mock examples to 1,024 UTF-8 bytes.

The Builder adds or reuses one `NpcConversationBehaviour` and one `NpcActionCoordinator`. Reapplying is idempotent: it updates Kit wiring without deleting consumer components or assets. A Scene Prefab instance receives overrides; its source Prefab is not changed implicitly.

## 3. Verify without a network

Return to **2. Mock Preview**, enter `open the gate` in **User Text**, and select **Preview Mock Response**. The preview should show:

- `Matched Triggers: request_open_gate`
- `Selected Action: open_gate`

Then enter Play Mode and submit the same text through the connected input View.

- With **Gate Unlocked** disabled, dialogue remains successful and only the action is rejected.
- With **Gate Unlocked** enabled, the handler executes and hides the gate.
- A different phrase does not match merely because it has the same meaning. Mock matching lowercases, trims, and collapses whitespace around an otherwise exact example.

When several triggers match, the highest priority wins. Declaration order breaks a priority tie. Unity executes at most one action per successful turn. Missing, rejected, failed, or cancelled actions do not turn an already successful dialogue into a conversation failure.

## 4. Enable semantic V3 matching

The Backend is optional and is not included in the UPM package. Use the `server/` source from the same repository revision as the Unity package. It requires Node.js 24 or newer and an OpenAI Platform project with access to the configured model.

From the matching repository's `server/` directory in PowerShell:

```powershell
npm ci
npm run build
npm test

$env:OPENAI_API_KEY = '<your OpenAI Platform API key>'
$env:OPENAI_MODEL = '<a model available to your project>'
npm run dev
```

Keep the key only in the Backend process environment. Never store it in Unity assets or source control.

In Character Builder:

1. Change **Conversation Mode** to `BackendActions`.
2. Keep **V3 Respond Endpoint** as `http://127.0.0.1:8787/v3/npc/respond`.
3. Keep **V3 Reset Endpoint** as `http://127.0.0.1:8787/v3/npc/sessions/reset`.
4. Keep **Timeout Seconds** at `35` unless local requirements justify another value.
5. Validate and apply again.

Now a semantically equivalent phrase such as `Could you let me through that gate?` may match `request_open_gate` even though it differs from the Mock example. The Backend receives only trigger IDs and condition descriptions. It never receives action IDs, method names, component types, or Scene references. Unity rejects unknown returned IDs and the handler still performs final authorization.

## Add more actions

Add one consumer handler and one unique binding for each action. An action can call project-owned systems such as Animator, NavMesh, doors, quests, inventory, combat, or UI, provided those dependencies remain inside the consumer handler.

Phase 11 deliberately does not support model-generated parameters, Reflection method calls, persistent variables, relationship scores, generic condition trees, action sequences, or parallel actions. Use separate fixed action IDs for distinct effects. Advanced variables and compound rules require a later optional milestone.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| No Conversation Actions controls | Confirm the installed package is `0.3.0` or later; `v0.2.0` predates actions. |
| `NPC 대화 구성이 준비되지 않았습니다.` | Exit Play Mode, wait for compilation, confirm the profile and visual presentation, ensure every action ID has exactly one compatible handler, validate, and apply again. |
| A generated sample has a missing script | Reimport the current-version sample, wait for compilation, and rerun **Create Conversation Action Prototype**. Do not reuse an older generated Scene. |
| Mock preview shows no trigger | Enter the configured Mock example; semantic paraphrases require `BackendActions`. |
| Dialogue appears but the action does not run | Inspect the selected handler, `ActionId`, serialized references, enabled state, and `CanExecute` rejection reason. |
| Profile validation fails | Use unique lower `snake_case` trigger and action IDs and stay within the binding and UTF-8 limits. |
| Backend Actions fails | Confirm the matching Backend is listening on loopback, the V3 endpoints are selected, and the API project can access `OPENAI_MODEL`. |
| Reapplying reports duplicates | Keep exactly one `NpcConversationBehaviour`; the Builder refuses ambiguous existing configurations instead of deleting components. |
| Prefab cannot be selected | Use a writable regular or variant Prefab under `Assets/`; Model Prefabs and package-owned assets are read-only. |

See [Character Builder](CHARACTER_BUILDER.md), [architecture boundaries](ARCHITECTURE.md), and the [V3 contract](CONTRACT_V3.md) for the complete authoring and security model.

## Completion checklist

- The action profile validates and each action ID has one selected handler.
- Mock preview shows the expected matched trigger and selected action.
- Locked game state rejects only the action while dialogue remains visible.
- Authorized game state executes the action once.
- Reapplying the Builder creates no duplicate Kit components.
- If Backend Actions is enabled, a non-exact semantic phrase works through the matching loopback Backend.
