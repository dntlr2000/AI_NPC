# AI NPC JSON Contract V1

## 범위

V1은 한 번의 독립된 NPC 대화 요청과 그 성공 또는 오류 응답을 정의한다. 세션, 기억, 인증, 재시도와 OpenAI 공급자 형식은 포함하지 않는다. Phase 4는 이 payload 계약에 아래의 로컬 HTTP binding을 추가한다.

## 요청

```json
{
  "schemaVersion": 1,
  "requestId": "req-001",
  "character": {
    "characterId": "sample-luna",
    "displayName": "Luna",
    "personality": "Playful, curious, and friendly.",
    "speechStyle": "Warm, casual, short sentences.",
    "exampleDialogue": "새로운 모험 이야기를 들려줄래?",
    "defaultEmotion": "happy"
  },
  "userText": "무엇을 좋아해?"
}
```

모든 문자열 필드는 공백이 아닌 값이 필요하다. `requestId`는 형식을 강제하지 않는 opaque correlation ID이며 요청 생성자가 부여한다.

## 성공 응답

```json
{
  "schemaVersion": 1,
  "requestId": "req-001",
  "status": "success",
  "result": {
    "dialogue": "Luna: 새로운 모험 이야기를 들려줄래?",
    "emotion": "happy",
    "gesture": "nod"
  }
}
```

## 오류 응답

```json
{
  "schemaVersion": 1,
  "requestId": "req-001",
  "status": "error",
  "error": {
    "code": "invalid_request",
    "message": "요청을 확인해 주세요.",
    "retryable": false
  }
}
```

`code`는 `[a-z][a-z0-9]*(?:_[a-z0-9]+)*` 형태의 확장 가능한 token이다. 알 수 없는 유효 code도 Unity에서 일반 오류로 보존한다. `retryable`이 누락되면 `false`다.

## Phase 4 HTTP binding

- Backend는 `127.0.0.1`에만 bind하며 기본 URL은 `http://127.0.0.1:8787/v1/npc/respond`다.
- `POST` 요청과 응답은 UTF-8 `application/json`이고 요청 본문 제한은 16 KiB다.
- `2xx`는 `status: "success"`, 비-`2xx`는 `status: "error"`여야 한다. 조합이 다르면 Unity가 protocol error로 거부한다.
- Unity timeout은 35초, OpenAI upstream timeout은 30초다. 자동 재시도는 양쪽 모두 하지 않는다.
- 로컬 vertical slice에는 client authentication이 없다. 원격 bind 또는 배포는 허용하지 않는다.

| HTTP | V1 `error.code` | `retryable` | 의미 |
| --- | --- | --- | --- |
| 400 | `invalid_request` | false | JSON 또는 필수 값 오류 |
| 400 | `unsupported_schema_version` | false | V1 이외 버전 |
| 422 | `content_refused` | false | 모델의 구조화된 거절 |
| 429 | `rate_limited` | true | 모델 서비스 제한 |
| 504 | `upstream_timeout` | true | OpenAI timeout |
| 502 | `upstream_unavailable` | true | OpenAI 연결·서비스 실패 |
| 502 | `upstream_invalid_response` | true | 구조화 결과가 없거나 잘못됨 |
| 500 | `internal_error` | false | 안전하게 일반화한 내부 실패 |

HTTP 응답을 받기 전 Unity에서 생긴 오류는 V1 서버 envelope가 아니다. `backend_unreachable`, `backend_timeout`, `backend_protocol_error`를 `AiConversationException`으로 전달한다.

## 고정 토큰

| 종류 | 허용 값 |
| --- | --- |
| `status` | `success`, `error` |
| emotion | `neutral`, `happy`, `sad`, `angry`, `concerned` |
| gesture | `none`, `nod`, `wave` |

토큰은 대소문자를 구분한다. 알 수 없는 값은 fallback 없이 거부한다.

## 검증과 호환성

- `schemaVersion`은 정수 `1`이어야 하며 누락되거나 다른 버전이면 거부한다.
- 성공 응답은 `result`만, 오류 응답은 `error`만 포함한다. 비활성 branch는 누락 또는 `null`이어야 한다.
- 활성 branch 누락, 두 branch 동시 존재, 빈 필수 문자열과 malformed JSON은 거부한다.
- 같은 V1의 알 수 없는 추가 필드는 forward-compatible additive data로 무시한다.
- JSON object의 필드 순서와 공백은 의미가 없다.
- Unity 6 codec은 최상위 branch가 누락 또는 명시적 `null`이고 `JsonUtility`가 빈 객체를 만든 경우에만 비활성 객체를 `null`로 정규화한다. JSON에 명시된 빈 객체는 거부한다.

## 코드 경계

DTO, validator와 Core mapper는 `AiCharacterKit.Transport.V1`에 있으며 Unity에 의존하지 않는다. `AiNpcJsonCodec`과 `UnityWebRequestAiNpcBackendGateway`만 Unity 경계에 있다. `BackendConversationClient`가 V1 envelope를 기존 `IAiConversationClient`에 연결하며 Mock 구현은 그대로 유지한다. OpenAI SDK는 `server/`에만 존재한다.
