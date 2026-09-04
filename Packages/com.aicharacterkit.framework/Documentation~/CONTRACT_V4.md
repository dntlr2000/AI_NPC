# Conversation Contract V4

V4 preserves V2 session semantics and V3 optional semantic triggers while adding one immutable request-time grounding snapshot.

## Endpoints

- `POST /v4/npc/respond`
- `POST /v4/npc/sessions/reset`

The reference Backend accepts these endpoints on loopback only.

## Request shape

```json
{
  "schemaVersion": 4,
  "requestId": "req-v4-001",
  "sessionId": "session-v4-001",
  "character": {
    "characterId": "sample-guard",
    "displayName": "Guard",
    "personality": "Disciplined and observant.",
    "speechStyle": "Formal and concise.",
    "exampleDialogue": "State your business.",
    "defaultEmotion": "neutral"
  },
  "grounding": {
    "revision": "ctx-0fbb1fef8071da13b9476369537500347025c3762df5df65449f89b5275022bc",
    "background": "The western gate protects Dawnfall.",
    "goalsAndValues": "Protect citizens and honor lawful travelers.",
    "behavioralRules": [
      "Never reveal guard rotations.",
      "Prefer de-escalation."
    ],
    "dialogueExamples": ["Guard: State your business."],
    "facts": [
      {
        "factId": "gate_status",
        "kind": "observation",
        "statement": "The western gate is closed.",
        "priority": 90
      },
      {
        "factId": "city_founder",
        "kind": "lore",
        "statement": "Dawnfall was founded by Queen Mira.",
        "priority": 40
      },
      {
        "factId": "guard_suspicion",
        "kind": "belief",
        "statement": "The traveler may be hiding something.",
        "priority": 50
      }
    ]
  },
  "userText": "May I enter?",
  "triggers": []
}
```

Grounding strings are trimmed and use LF line endings. Fact kinds are `lore`, `belief`, or `observation`; IDs are unique lower `snake_case`, statements are at most 512 UTF-8 bytes, priorities are 0–100, and the snapshot allows 32 facts/12 KiB total. Background and goals/values allow 2 KiB each, rules allow 16 × 512 bytes, and dialogue examples allow 8 × 1 KiB. V3-compatible triggers are optional and bounded to 16.

The `ctx-<sha256>` revision covers normalized canon and facts. The SDK mapper computes it; consumers should not hand-author revisions.

## Success and error

```json
{
  "schemaVersion": 4,
  "requestId": "req-v4-001",
  "status": "success",
  "result": {
    "dialogue": "Guard: The western gate is closed.",
    "emotion": "neutral",
    "gesture": "nod",
    "matchedTriggerIds": []
  }
}
```

The response uses the existing emotion and gesture tokens. Matched IDs must be unique and drawn only from the request. Errors use only `error: { code, message, retryable }`; success uses only `result`. Reset sends `schemaVersion`, `requestId`, `sessionId`, and `characterId`, then receives `result: { "reset": true }` on success.

Unknown same-version fields are ignored. Missing fields, invalid bounds/tokens, duplicate IDs, incorrect revisions, unknown versions/statuses, malformed JSON, and mixed result/error branches are rejected without leaking parser or provider exceptions.

Only successful user/assistant text enters bounded process-local session history. The Backend does not log or retain canon, lore, beliefs, observations, revisions, or triggers. See [Runtime Context and Lore Quick Start](GROUNDING_QUICKSTART.md) for Unity setup.
