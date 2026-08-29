# AI Character Kit Local Backend

This Phase 4 server exposes the V1 AI NPC contract on loopback and keeps the OpenAI API key outside Unity and Git.

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

The server listens on `http://127.0.0.1:8787`. Optional settings are `PORT` and `OPENAI_TIMEOUT_MS`; the address remains loopback-only. The API key, profile text, user message, and generated dialogue are never written to application logs.

## Endpoints

- `GET /healthz`
- `POST /v1/npc/respond`

The POST body and response follow `docs/CONTRACT_V1.md`. This local vertical slice has no client authentication, remote deployment, memory, streaming, tool calling, or voice features.
