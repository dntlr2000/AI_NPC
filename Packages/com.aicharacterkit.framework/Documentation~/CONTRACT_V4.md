# AI NPC Conversation Contract V4

V4 extends the V3 session/action contract with a bounded, immutable request-time grounding snapshot. It does not let the Backend mutate Unity state and does not persist grounding in session history.

## Endpoints

- `POST /v4/npc/respond`
- `POST /v4/npc/sessions/reset`

Both endpoints use UTF-8 JSON. The reference server remains bound to loopback.

### Phase 17 optional two-stage route — Unreleased

The current source also implements `POST /v4/npc/respond-two-stage` using the **same V4 request and response DTOs and schema version**. It separates ordered trigger classification from dialogue generation and combines only validated results. A turn with no triggers makes one generation; other turns make at most two, sharing the configured upstream deadline. There is no automatic retry or fallback. Classification output is never session memory.

This candidate route has the same **524,288-byte** serialized UTF-8 body limit and V4 HTTP 413 response as `/v4/npc/respond`. Both routes and `/v4/npc/sessions/reset` use one session store and lock. Only successful complete dialogue is committed once. Busy, cancellation, invalid output and failed dialogue leave memory uncommitted.

The current reference server enables this route only for `OPENAI_MODEL=gpt-5.6-luna`, qualified by the separate Phase 17 remediation campaign. Other configured models without a qualified service receive HTTP 503, `upstream_unavailable`, `retryable=false`. The ordinary server exposes both endpoints on its configured port (default 8787); a second production server is unnecessary. Existing single-pass asset endpoints remain unchanged; Builder stores a separate two-stage endpoint. Stage telemetry stays server-side. Backend still receives no Unity action-completion receipt: generated promises can be followed by a final authorization refusal. Qualification does not guarantee all semantic answers or changes in a mutable model alias.

## Conversation request

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

`sessionId` is a non-blank opaque string up to 128 characters. `userText` is non-blank and at most 8 KiB. `triggers` contains 0–16 unique V3-compatible semantic triggers. Unknown same-version fields are ignored.

Grounding uses canonical line endings (`LF`) and trimmed surrounding whitespace. `background` and `goalsAndValues` may be empty and are each limited to 2 KiB. Rules allow 16 entries of 512 bytes; dialogue examples allow 8 entries of 1 KiB. Facts allow 32 unique lower `snake_case` IDs, `lore|belief|observation`, priority 0–100, 512 bytes per statement, and 12 KiB of statements in total.

## Serialized request body limit — Unreleased stabilization

`POST /v4/npc/respond` accepts at most **512 KiB (524,288 bytes)** of serialized UTF-8 JSON, including character profile fields, grounding, user text, triggers, same-version extension fields, JSON structure, and JSON string escaping. Exactly 524,288 bytes is allowed; one byte more is rejected. Individual field and snapshot limits above are unchanged and still apply independently.

The Unity gateway checks the actual UTF-8 byte count after serialization and before creating an HTTP request. An oversized body fails with `invalid_request`, `retryable=false`, and the fixed message `NPC 대화 요청이 최대 허용 크기(512 KiB)를 초과했습니다.` The gateway does not remove or truncate data to fit. A directly submitted oversized server request returns HTTP 413 in the existing V4 error envelope without invoking the generator.

The matching constants are `AiNpcContractV4.MaxRequestBodyUtf8Bytes` in C# and `MAX_REQUEST_BODY_UTF8_BYTES` in `server/src/contracts/v4.ts`. This route-specific limit leaves the server's global 32 KiB limit, V1–V3 routes, V4 reset, and speech route limits unchanged. These fixes belong to `Unreleased`; the immutable public `v0.4.0` release used the prior global limit for V4 respond.

## Revision

`revision` is `ctx-` plus lowercase SHA-256. The canonical input appends background, goals/values, array counts and ordered values, then facts sorted by descending priority and ordinal fact ID. Each value is represented as `<UTF-16-code-unit-length>:<value>|`; the resulting text is hashed as UTF-8. Unity and the reference Backend reject a revision that does not match its content.

## Responses

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

Emotion tokens are `neutral`, `happy`, `sad`, `angry`, and `concerned`; gestures are `none`, `nod`, and `wave`. Matched IDs must be unique and a subset of the request trigger IDs.

Errors use only the `error` branch:

```json
{
  "schemaVersion": 4,
  "requestId": "req-v4-001",
  "status": "error",
  "error": {
    "code": "invalid_request",
    "message": "The context-grounded AI NPC request is invalid.",
    "retryable": false
  }
}
```

Success has only `result`; error has only `error`. Inactive fields may be absent or `null`. Malformed JSON, missing fields, invalid bounds or tokens, duplicate IDs, incorrect revision, unknown status/version, and mixed branches are rejected without exposing parser or provider errors.

## Reset and storage

Reset uses `schemaVersion`, `requestId`, `sessionId`, and `characterId`; success returns `result: { "reset": true }`. V4 shares V2 session binding, busy, TTL, LRU, capacity, and idempotent reset behavior. Only successful user/assistant text enters process-local history. Canon, lore, beliefs, live facts, revision, and triggers are not logged or stored in that history.
