# Runtime Context and Lore Quick Start

Use V4 grounding when an NPC must respect stable character canon and answer from current game state. Grounding affects what the NPC says; consumer code still owns every gameplay mutation and authorization decision.

## 1. Author character canon

Open **Tools > AI Character Kit > Character Builder** and create or load a `CharacterProfile`. Fill the existing identity fields, then author:

- Background: stable history and role
- Goals and Values: motivations that guide choices
- Behavioral Rules: short mandatory constraints
- Additional Dialogue Examples: examples that demonstrate voice and boundaries

Use facts, not hidden instructions or API credentials. The builder validates the same byte/count limits used by V4 before saving.

## 2. Add reusable lore and beliefs

Enable **Runtime Grounding**, then create or select an `NpcLoreProfile`. Lore entries are objective world facts; belief entries are the character's subjective understanding and may be wrong. Each entry needs a unique lower `snake_case` fact ID, statement, and priority from 0 to 100.

Multiple lore profiles may be assigned to one NPC. Reusing a world lore asset across characters avoids copying worldbuilding into MonoBehaviours.

## 3. Expose current game state

Implement a consumer component derived from `NpcContextProviderBehaviour`. Capture only the facts relevant to the current turn and do not mutate state inside `CaptureFacts`.

```csharp
using System.Collections.Generic;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using UnityEngine;

public sealed class DoorContextProvider : NpcContextProviderBehaviour
{
    [SerializeField] private GameObject openGateVisual;

    /// <summary>
    /// Captures the door state at the moment a conversation request starts.
    /// </summary>
    public override IReadOnlyList<NpcContextFact> CaptureFacts()
    {
        var gateIsOpen = openGateVisual != null
            && openGateVisual.activeInHierarchy;
        return new[]
        {
            new NpcContextFact(
                "west_gate_status",
                NpcContextFactKind.Observation,
                gateIsOpen
                    ? "The western gate is currently open."
                    : "The western gate is currently closed.",
                100)
        };
    }
}
```

Add the component to the target NPC, then select it under **Runtime Context Providers**. IDs must remain unique across all assigned lore and providers. Provider exceptions or invalid facts fail the turn safely without exposing raw exception details.

## 4. Connect V4

In **NPC Connection**, choose the existing target and presentation driver, select **BackendContext**, and keep the default loopback endpoints unless the matching local service uses another permitted loopback port:

- Respond: `http://127.0.0.1:8787/v4/npc/respond`
- Reset: `http://127.0.0.1:8787/v4/npc/sessions/reset`

Validate and apply. The builder creates or reuses one `NpcContextCoordinator` and wires the selected lore and providers. Reapplying is non-destructive and does not duplicate consumer components.

## 5. Verify

Import **AI NPC Prototypes**, then run **Tools > AI Character Kit > Samples > Create Grounded Guard Prototype**. Start the matching reference Backend and open `GroundedNpcPrototype.unity`. Ask about the gate and town, toggle **Gate Open** or **Town Alarm**, and ask again. The captured status and context revision should change with the snapshot.

Reset clears bounded conversation history only. Authored canon, lore, and the latest provider facts are supplied again on the next request. Grounding is never a substitute for `INpcActionHandler.CanExecute`; check current game state again before opening a door, granting an item, or advancing a quest.

## Limits

One snapshot permits 32 facts and 12 KiB of fact statements. Lower-priority facts are omitted deterministically when the budget is full, and their IDs are available from `NpcConversationBehaviour.LastOmittedContextFactIds`. This is deliberate bounded context, not long-term memory, RAG, or a generic rule engine.
