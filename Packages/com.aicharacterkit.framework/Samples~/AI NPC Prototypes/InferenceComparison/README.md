# API Inference Comparison

Import **AI NPC Prototypes**, then use **Tools > AI Character Kit > Samples > Create API Inference Comparison**. This creates a separate consumer-owned Scene; existing samples remain unchanged.

Run the current reference backend with `OPENAI_MODEL=gpt-5.6-luna` (also the default). Both strategies use the same server on port 8787. Mira uses `/v4/npc/respond`; Toma uses `/v4/npc/respond-two-stage`. Both reset through `/v4/npc/sessions/reset`. No second production server or client-side API key is needed.

The NPCs have independent affinity and memory but share gate, permission and alarm state. Reset both memories and behavior states and restore the same world state before comparing a sequence. The panel shows strategy, full submission time, current signals and actual action result. A single example is not an accuracy benchmark.

Try greeting, two direct compliments and a gate-opening request. Permission must be granted, the alarm inactive and affinity at least 60. Memory reset preserves relationship state; behavior reset preserves conversation memory.

Backend dialogue is generated before final `CanExecute`. It may promise an action that is subsequently refused if game state changes. Read **Actual action** for the authoritative outcome; this sample does not add a Backend action-result synchronization protocol. General semantic errors and missed/incorrect relationship signals remain possible. SinglePass remains the default for new NPCs; the two-stage option is qualified only for the documented API configuration, not E4B or mobile.
