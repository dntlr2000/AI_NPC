# AI NPC Conversation Contract V3

V3 extends the V2 process-local session contract with a bounded semantic trigger snapshot. It does not transmit Unity action IDs, method names, parameters, or object references.

## Endpoints

- `POST /v3/npc/respond`
- `POST /v3/npc/sessions/reset`

Both endpoints accept and return UTF-8 JSON. The reference server remains loopback-only.

## Conversation request

```json
{
  "schemaVersion": 3,
  "requestId": "req-v3-001",
  "sessionId": "session-v3-001",
  "character": {
    "characterId": "sample-guide",
    "displayName": "Guide",
    "personality": "Helpful and observant.",
    "speechStyle": "Warm and brief.",
    "exampleDialogue": "How can I help?",
    "defaultEmotion": "neutral"
  },
  "userText": "문을 열어 줘",
  "triggers": [
    {
      "triggerId": "request_open_gate",
      "conditionDescription": "The player asks the guide to open the gate."
    }
  ]
}
```

`sessionId` is a non-blank opaque string up to 128 characters. `userText` is non-blank and at most 8 KiB in UTF-8. A request contains 1–16 unique triggers. Each trigger ID is a lower `snake_case` token up to 64 characters; each non-blank condition is at most 512 UTF-8 bytes.

## Success and error responses

```json
{
  "schemaVersion": 3,
  "requestId": "req-v3-001",
  "status": "success",
  "result": {
    "dialogue": "Guide: 문을 확인해 볼게요.",
    "emotion": "happy",
    "gesture": "nod",
    "matchedTriggerIds": ["request_open_gate"]
  }
}
```

`matchedTriggerIds` may be empty but must be unique and must be a subset of the request trigger IDs. Allowed emotion tokens are `neutral`, `happy`, `sad`, `angry`, and `concerned`; gesture tokens are `none`, `nod`, and `wave`.

Errors use the V1/V2 exclusive branch:

```json
{
  "schemaVersion": 3,
  "requestId": "req-v3-001",
  "status": "error",
  "error": {
    "code": "session_busy",
    "message": "The session is busy.",
    "retryable": true
  }
}
```

Success has only `result`; error has only `error`. Inactive fields may be absent or `null`. Unknown same-version fields are ignored. Missing required fields, malformed JSON, unknown tokens, duplicate IDs, unsupported versions, invalid branches, and response IDs outside the request snapshot are rejected without exposing parser exceptions.

## Session reset

Reset uses the V2 shape with `schemaVersion: 3`, `requestId`, `sessionId`, and `characterId`. Success returns `status: "success"` with `result: { "reset": true }`. V3 shares the bounded process-local session behavior, character binding, busy response, TTL, LRU, and reset semantics defined by V2.

## Trust boundary

Matched IDs are model output and remain untrusted. Unity validates the response subset, selects at most one binding by descending priority and declaration order, and calls the corresponding consumer `INpcActionHandler`. The handler's `CanExecute` performs the final game-state authorization. Action failure never changes an already successful dialogue result.
