# AI NPC JSON Contract V2

## 범위

V2는 V1의 캐릭터·대사·감정·제스처 형식을 유지하면서 호출자 소유의 opaque `sessionId`와 명시적 reset을 추가한다. Backend는 응답에서 `requestId`만 반향하며 세션 ID, 저장된 대화 또는 모델 공급자 정보를 반환하지 않는다.

## 대화 요청과 응답

```json
{
  "schemaVersion": 2,
  "requestId": "req-001",
  "sessionId": "session-0123456789abcdef0123456789abcdef",
  "character": {
    "characterId": "sample-luna",
    "displayName": "Luna",
    "personality": "Playful, curious, and friendly.",
    "speechStyle": "Warm, casual, short sentences.",
    "exampleDialogue": "새로운 모험 이야기를 들려줄래?",
    "defaultEmotion": "happy"
  },
  "userText": "내가 좋아하는 색은 파란색이야."
}
```

```json
{
  "schemaVersion": 2,
  "requestId": "req-001",
  "status": "success",
  "result": {
    "dialogue": "파란색을 좋아하는구나! 기억해 둘게.",
    "emotion": "happy",
    "gesture": "nod"
  }
}
```

`sessionId`는 공백이 아닌 최대 128자 문자열이다. `userText`는 공백이 아니며 UTF-8 기준 최대 8 KiB다. `requestId`는 각 작업의 상관관계 ID이고, Unity의 session client는 GameObject 수명 동안 동일한 `sessionId`를 재사용한다.

## Reset

```json
{
  "schemaVersion": 2,
  "requestId": "req-reset-001",
  "sessionId": "session-0123456789abcdef0123456789abcdef",
  "characterId": "sample-luna"
}
```

```json
{
  "schemaVersion": 2,
  "requestId": "req-reset-001",
  "status": "success",
  "result": { "reset": true }
}
```

Reset은 멱등적이다. 존재하지 않거나 만료된 세션도 성공하고 새 세션을 만들지 않는다. 존재하는 세션은 기록만 비우며 기존 `characterId` 결합은 유지한다.

## 오류 응답과 HTTP binding

오류 envelope는 V1과 동일하다.

```json
{
  "schemaVersion": 2,
  "requestId": "req-001",
  "status": "error",
  "error": {
    "code": "session_busy",
    "message": "The conversation session is already processing a request.",
    "retryable": true
  }
}
```

- `POST http://127.0.0.1:8787/v2/npc/respond`
- `POST http://127.0.0.1:8787/v2/npc/sessions/reset`
- UTF-8 `application/json`, 전체 HTTP body 최대 16 KiB
- `2xx`는 success, 비-`2xx`는 error branch여야 한다.

| HTTP | `error.code` | 재시도 | 의미 |
| --- | --- | --- | --- |
| 400 | `invalid_request` | 아니요 | JSON 또는 필수 값 오류 |
| 400 | `unsupported_schema_version` | 아니요 | V2 이외 버전 |
| 409 | `session_busy` | 예 | 같은 세션의 respond/reset 진행 중 |
| 409 | `session_character_mismatch` | 아니요 | 세션을 다른 캐릭터 ID로 재사용 |
| 503 | `session_capacity_reached` | 예 | 모든 session slot이 작업 중 |

모델 거절·timeout·upstream 오류는 [`CONTRACT_V1.md`](CONTRACT_V1.md)의 안전한 오류 코드를 그대로 사용한다. Unity transport 이전 오류도 `backend_unreachable`, `backend_timeout`, `backend_protocol_error`로 매핑한다.

## 검증 및 고정 토큰

- `schemaVersion`은 정수 `2`만 허용한다.
- `status`, emotion, gesture token은 V1과 동일하며 대소문자를 구분한다.
- success에는 `result`만, error에는 `error`만 존재해야 한다. 비활성 branch는 누락 또는 `null`만 허용한다.
- Reset success는 반드시 `result.reset == true`다.
- 같은 V2의 알 수 없는 추가 필드는 무시한다.
- malformed JSON, 누락 필드, 알 수 없는 enum/status, 두 branch 동시 존재는 거부한다.

## 단기 기억 의미

Backend는 성공한 완전한 turn의 `userText`와 assistant `dialogue`만 프로세스 메모리에 저장한다. 실패·취소·거절, 감정과 제스처는 저장하지 않는다. 기본 한계는 최근 8 turn, UTF-8 텍스트 합계 16 KiB, idle TTL 1,800초, 최대 128세션이다. 초과 시 가장 오래된 완전한 turn 또는 가장 오래 idle인 세션부터 제거한다. 서버 재시작 시 모든 기록이 사라지며 디스크 저장, 장기 기억, Vector DB, 요약 기억은 없다.

## 코드 경계

Unity 비의존 DTO·validator·mapper는 `AiCharacterKit.Transport.V2`, JSON codec은 Unity 경계, HTTP gateway는 `Runtime/Unity/Networking`에 있다. Backend의 Zod 계약은 `server/src/contracts/v2.ts`, 세션 저장소는 `server/src/sessions.ts`에 있다. V1과 기존 `IAiConversationClient`는 변경하지 않는다.
