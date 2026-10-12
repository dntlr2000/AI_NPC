# Optional two-stage API inference — Unreleased

Current source qualifies **BackendContext/V4 with `gpt-5.6-luna` only**. SinglePass remains the default. E4B two-stage remains disabled after its unsuccessful remediation development; mobile/LiteRT/E2B and new LFM2.5 support are outside this work. Public v0.4.0 does not include these changes.

## Configure an NPC

1. Run the current reference server from `server/` with `npm.cmd run dev`, keeping `OPENAI_API_KEY` only in its environment. `OPENAI_MODEL=gpt-5.6-luna` is the tested default. Restart an older server to load updated source.
2. In **Tools > AI Character Kit > Character Builder**, select **BackendContext** and **ClassifyThenRespond**. An existing NPC can also be configured through its Inspector.
3. Keep the separate two-stage endpoint at `http://127.0.0.1:8787/v4/npc/respond-two-stage` and reset at `http://127.0.0.1:8787/v4/npc/sessions/reset`. The original single-pass endpoint is preserved. One server hosts both routes; a separate production server is unnecessary.
4. Configure Basic or Advanced actions and request-time grounding as before. Apply to the Scene or Prefab. Static validation does not capture providers, execute handlers or prepare a local model.

Import **AI NPC Prototypes**, then use **Tools > AI Character Kit > Samples > Create API Inference Comparison**. The separate Scene shows a SinglePass NPC and a two-stage NPC, full submission time, current signals, variables and actual action results. They have independent memory/affinity and share a gate. Restore the same memory, variables and world state before comparing a sequence.

Unsupported modes/model combinations fail explicitly; they do not switch to SinglePass or another server automatically. Existing constructors and assets default to SinglePass. A custom runner's optional capability declaration does not establish its model's semantic accuracy.

## Processing and diagnostics

The same model first returns an ordered `match/no_match/uncertain` array, then generates dialogue without permission to change the validated trigger IDs. Both stages must succeed before Basic/Advanced processes the response once. Unity keeps deterministic variable changes and final `CanExecute` authorization. Conversation reset and behavior reset remain independent.

The new endpoint keeps V4 DTOs, schema version and the 524,288-byte request limit. Both routes and reset share a session store and lock; only completed dialogue is stored. Trigger-free turns make one generation; other turns make at most two. Backend stages share the configured upstream deadline (default 30 seconds), each with at most 256 output tokens. There are no retries or online fallbacks.

`NpcConversationBehaviour.LastSubmissionSeconds` measures the complete admitted submission. Local `LastInferenceDiagnostics` is request-correlated; unknown usage is -1. Backend stage metrics remain server-side without extending V4. `NpcActionCoordinator.LastBehaviorTurnOutcome` distinguishes pending, generation/provider failure and actual evaluation, with immutable before/after state. Busy rejection does not replace the running outcome. No ordinary dialogue, grounding or model reasoning is logged.

Advanced authored match/non-match examples remain optional: four entries per list, 128 UTF-8 bytes per entry, and 512 bytes for the combined transmitted condition. Empty examples preserve earlier input; Mock matching and Phase 12 calculation are unchanged.

## Qualification and limits

The frozen API follow-up matched 184/192 regression, 47/48 observed-holdout and 70/72 fresh-holdout signal sets, with zero false-positive action signals. State/recall scores were 38/38, 24/24 and 24/24; fresh task answers 66/72. Server 147/147 and root/consumer EditMode 362/362 passed. Seventy-two core live turns passed across root, Built-in/Legacy consumer and Windows x64 Mono; full submission p95 was 3.798, 3.906 and 3.351 seconds respectively. Individual maxima exceeded five seconds in consumer/player; p95 is not a per-turn guarantee.

**Two-stage is not universally superior.** The fresh paired SinglePass comparison was better: 72/72 signals, 70/72 task answers and 2.296-second request p95 versus the candidate's 3.662 seconds. Missed normal requests, wrong relationship signals, invented procedures and past/current explanations remain possible. The tested API identifier is an alias, not an immutable model revision. See repository `docs/PHASE17_REMEDIATION_VALIDATION.md` for fixed inputs, failures, timing boundaries, Unity editor harness interruptions and retained evidence.

Backend dialogue is generated before the final game effect and receives no post-action receipt. It may promise an action later refused by `CanExecute`; **Actual action** is authoritative. Existing opt-in local action-response finalization remains unchanged. General dialogue accuracy, all authored profiles and all game-state races are not guaranteed by this qualification.

The original E4B/API candidate failures remain separately recorded in `docs/PHASE17_VALIDATION.md`; they were not overwritten by the follow-up. No new package version, release, dependency or model distribution is included.
