# AI Character Kit Transcription Contract V1

## 범위

Transcription V1은 Unity가 캡처한 한 개의 완성된 WAV를 local Backend에 보내고 검증된 text를 받는 선택형 계약이다. Unity에는 OpenAI model·API key가 없고 Backend는 오디오나 전사문을 저장하지 않는다.

## 요청

`POST http://127.0.0.1:8787/v1/speech/transcribe`

Body는 `Content-Type: audio/wav`인 canonical WAV 전체다.

| Header | 값 |
| --- | --- |
| `X-Ai-Character-Kit-Transcription-Version` | `1` |
| `X-Ai-Character-Kit-Request-Id` | 공백이 아닌 최대 128자 opaque ID |

WAV는 정확한 44-byte RIFF/WAVE header, PCM format 1, 16-bit mono, 8–48 kHz여야 한다. data는 비어 있지 않고 최대 15초·전체 2 MiB 이하여야 하며 header byte count와 실제 body 길이가 일치해야 한다.

## 성공 응답

```json
{
  "schemaVersion": 1,
  "requestId": "transcription-001",
  "status": "success",
  "result": {
    "text": "안녕하세요, Luna."
  }
}
```

`text`는 공백이 아니고 최대 4,096자이자 UTF-8 8 KiB 이하다. 같은 V1의 알 수 없는 추가 필드는 무시한다.

## 오류 응답

```json
{
  "schemaVersion": 1,
  "requestId": "transcription-001",
  "status": "error",
  "error": {
    "code": "invalid_audio",
    "message": "The request must contain canonical PCM16 mono WAV audio.",
    "retryable": false
  }
}
```

성공은 `result`만, 오류는 `error`만 가진다. 알 수 없는 version/status, branch overlap, 빈 필수값은 거부한다. 주요 코드는 `invalid_request`, `unsupported_schema_version`, `invalid_audio`, `audio_too_long`, `rate_limited`, `upstream_timeout`, `upstream_unavailable`, `upstream_invalid_response`, `internal_error`다. Unity local 오류는 `backend_unreachable`, `backend_timeout`, `backend_protocol_error`로 정규화한다.

## 보안과 로그

Endpoint는 loopback 전용이며 redirect를 허용하지 않는다. 요청에는 credential을 넣지 않는다. 성공 로그는 correlation ID, WAV byte 수, duration과 latency만 포함하며 audio, transcript, microphone device, profile 또는 conversation content는 기록하지 않는다.
