# AI Character Kit Local Backend

This local server exposes stateless V1, session-aware V2, action-aware V3, context-grounded V4, optional Speech V1, and optional Transcription V1 contracts on loopback. It keeps the OpenAI API key and provider settings outside Unity and Git, and stores conversation history only in process memory.

## Distribution boundary

Release `0.4.0` distributes the Unity framework as the Git-subfolder UPM package at `Packages/com.aicharacterkit.framework` and includes the Phase 11 V3 action contract plus the Phase 15 V4 grounding contract. The `server/` directory is matching reference source and is not published as a UPM or npm package. Consumers that need Backend modes must check out the matching repository revision and run this service separately. Mock mode does not require it.

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

The server listens on `http://127.0.0.1:8787`. `PORT`, `OPENAI_MODEL`, and `OPENAI_TIMEOUT_MS` are optional; the address remains loopback-only. The API key, session ID, profile text, user message, generated dialogue, synthesis text, voice instructions, microphone audio, and transcript are never written to application logs.

Optional speech settings are loaded at startup:

| Environment variable | Default | Purpose |
| --- | --- | --- |
| `OPENAI_TTS_MODEL` | `gpt-4o-mini-tts` | OpenAI Speech model |
| `OPENAI_TTS_TIMEOUT_MS` | `30000` | 1,000–120,000 ms SDK timeout |
| `NPC_TTS_VOICE_PRESETS_PATH` | `config/voice-presets.json` | Server-owned preset mapping |

Run the server from `server/` when using the relative default preset path. Each preset maps a stable project ID such as `warm-friendly` to an OpenAI voice, instructions, and speed. Do not put secrets in the preset file.

Optional transcription settings are loaded at startup:

| Environment variable | Default | Purpose |
| --- | --- | --- |
| `OPENAI_TRANSCRIPTION_MODEL` | `gpt-transcribe` | OpenAI file transcription model |
| `OPENAI_TRANSCRIPTION_TIMEOUT_MS` | `30000` | 1,000–120,000 ms SDK timeout |

Transcription accepts only canonical PCM16 mono WAV, 8–48 kHz, at most 15 seconds and 2 MiB. It uses an in-memory upload and never writes recordings to disk.

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
- `POST /v3/npc/respond`
- `POST /v3/npc/sessions/reset`
- `POST /v4/npc/respond`
- `POST /v4/npc/sessions/reset`
- `POST /v1/speech/synthesize`
- `POST /v1/speech/transcribe`

Conversation V1 follows `docs/CONTRACT_V1.md` and remains stateless. V2 follows `docs/CONTRACT_V2.md`; it stores only successful user/assistant turns, uses `store: false` for OpenAI requests, and loses all history when the process stops. V3 follows `docs/CONTRACT_V3.md`; it reuses V2 session behavior and asks the same structured response to return only IDs from a bounded request trigger snapshot. It never receives Unity action IDs, methods, parameters, or object references. V4 follows `docs/CONTRACT_V4.md`; it adds normalized character canon and bounded request-time lore, belief, and observation facts to the generation instruction. Grounding and its revision are not logged or stored in session history. Speech follows `docs/SPEECH_CONTRACT_V1.md` and returns complete PCM16LE 24 kHz mono buffers. Transcription follows `docs/TRANSCRIPTION_CONTRACT_V1.md` and returns reviewed text input. This local vertical slice has no client authentication, remote deployment, persistent memory, streaming, Realtime, VAD, custom voice, or bundled offline inference features.
