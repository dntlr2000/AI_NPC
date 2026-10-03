# Grounded Guard Sample

This sample demonstrates V4 request-time lore and live game-state grounding. It does not embed a model or API key.

1. Import **AI NPC Prototypes** from Package Manager.
2. Run **Tools > AI Character Kit > Samples > Create Grounded Guard Prototype**.
3. Start the local reference backend and open `GroundedNpc/Scenes/GroundedNpcPrototype.unity`.
4. Enter Play Mode, change **Gate Open** or **Town Alarm**, then ask the Guard about the current situation.

`SampleGuardContextProvider` is consumer-owned example code. It reads the two toggles and returns immutable `NpcContextFact` observations immediately before each request. `DawnfallLore.asset` stores reusable world facts, while `GroundedGuard.asset` stores the character's background, goals, rules, and dialogue examples.

The captured state label shows which toggle values entered the request, the resulting `ctx-` revision, and any facts omitted by the bounded budget. Session reset clears only short dialogue history; it does not change lore or current game state.
