# AI Character Kit Speech Contract V1

## 범위

Speech V1은 생성된 NPC 대사와 opaque voice preset ID를 Backend에 보내고, 고정 형식 PCM을 받는 선택형 계약이다. Unity는 OpenAI model·voice·instructions를 알지 못하며 API 키도 보유하지 않는다.

## 요청

`POST http://127.0.0.1:8787/v1/speech/synthesize`

```json
{
  "schemaVersion": 1,
  "requestId": "speech-001",
  "voicePresetId": "warm-friendly",
  "text": "Luna: 반가워! 오늘은 어떤 이야기를 나눌까?"
}
```

- `requestId`: 공백이 아닌 최대 128자 opaque correlation ID
- `voicePresetId`: 최대 64자의 소문자·숫자·단일 하이픈 token
- `text`: 공백이 아닌 최대 4,096자이자 UTF-8 8 KiB 이하
- 같은 V1의 알 수 없는 추가 필드는 무시한다.

## 성공 응답

HTTP 200 body는 JSON이 아닌 `application/octet-stream` PCM이다. 최대 8 MiB이며 비어 있지 않은 완전한 16-bit sample이어야 한다.

| Header | 고정값 |
| --- | --- |
| `X-Ai-Character-Kit-Speech-Version` | `1` |
| `X-Ai-Character-Kit-Request-Id` | 요청 `requestId` |
| `X-Ai-Character-Kit-Audio-Format` | `pcm_s16le` |
| `X-Ai-Character-Kit-Sample-Rate` | `24000` |
| `X-Ai-Character-Kit-Channels` | `1` |

Unity는 모든 header, correlation, byte 수와 sample 정렬을 검증한 뒤 transient `AudioClip`으로 변환한다.

## 오류 응답

비-2xx body는 UTF-8 JSON이다.

```json
{
  "schemaVersion": 1,
  "requestId": "speech-001",
  "status": "error",
  "error": {
    "code": "voice_preset_not_found",
    "message": "The requested voice preset is not configured.",
    "retryable": false
  }
}
```

주요 코드는 `invalid_request`, `unsupported_schema_version`, `voice_preset_not_found`, `rate_limited`, `upstream_timeout`, `upstream_unavailable`, `upstream_invalid_response`, `internal_error`다. Unity가 만든 연결 오류는 `speech_backend_unreachable`, `speech_backend_timeout`, `speech_backend_protocol_error`다. 음성 오류는 기존 대화 응답을 실패로 바꾸지 않는다.

## Voice preset

`server/config/voice-presets.json`은 project-owned ID를 공급자 설정에 매핑한다. 시작 시 전체 파일과 중복 ID를 검증하며 알 수 없는 ID는 OpenAI 호출 전에 거부한다. Unity asset에는 preset ID만 저장하고 실제 voice·instructions·speed는 Backend가 소유한다.

## 보안과 로그

Endpoint는 loopback 전용이고 인증·원격 배포는 Phase 6 범위가 아니다. API 키, 합성 text, voice instructions와 raw upstream 오류는 기록하지 않는다. 성공 로그는 correlation ID, PCM byte 수와 지연시간만 포함한다.
