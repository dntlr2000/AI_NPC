# AI Character Kit Local Backend

This local server exposes the stateless V1 and session-aware V2 AI NPC contracts on loopback. It keeps the OpenAI API key outside Unity and Git, and stores Phase 5 history only in process memory.

## Requirements

- Node.js 24 or newer
- An OpenAI Platform project with access to the configured model
- `OPENAI_API_KEY` set only in the server process environment

## Install and verify

From `E:\Unity\AI_NPC\server` in PowerShell:

```powershell
$env:TEMP = 'E:\CodexTemp'
$env:TMP = 'E:\CodexTemp'
$env:npm_config_cache = 'E:\CodexTemp\npm-cache'
npm ci
npm run build
npm test
```

These commands do not call OpenAI.

## Start the local server

Set the key only in the current terminal session:

```powershell
$env:OPENAI_API_KEY = '<your OpenAI Platform API key>'
$env:OPENAI_MODEL = 'gpt-5.6-luna'
npm run dev
```

The server listens on `http://127.0.0.1:8787`. `PORT`, `OPENAI_MODEL`, and `OPENAI_TIMEOUT_MS` are optional; the address remains loopback-only. The API key, session ID, profile text, user message, and generated dialogue are never written to application logs.

Phase 5 session limits can be changed before startup:

| Environment variable | Default | Valid range |
| --- | ---: | ---: |
| `NPC_SESSION_MAX_TURNS` | 8 | 1–32 |
| `NPC_SESSION_MAX_CONTEXT_BYTES` | 16384 | 4096–131072 |
| `NPC_SESSION_IDLE_TTL_SECONDS` | 1800 | 60–86400 |
| `NPC_SESSION_MAX_COUNT` | 128 | 1–4096 |

Invalid values stop startup. Limits remove the oldest complete turn or least-recently-used idle session; text is never partially truncated.

## Endpoints

- `GET /healthz`
- `POST /v1/npc/respond`
- `POST /v2/npc/respond`
- `POST /v2/npc/sessions/reset`

V1 follows `docs/CONTRACT_V1.md` and remains stateless. V2 follows `docs/CONTRACT_V2.md`; it stores only successful user/assistant turns, uses `store: false` for OpenAI requests, and loses all history when the process stops. This local vertical slice has no client authentication, remote deployment, persistent or long-term memory, streaming, tool calling, or voice features.
