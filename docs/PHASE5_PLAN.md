# Phase 5 — Sessions and Bounded Short-Term Memory

> 상태: 구현 및 자동 검증 완료, 실제 OpenAI Play Mode 검증과 체크포인트 커밋 대기
> 기준 커밋: `ab53815 (Phase4_1)`
> 목표: 기존 V1·Mock 경로를 보존하면서 NPC별 제한된 최근 대화와 명시적 reset을 제공한다.

## 구현 범위

- V2 conversation/reset DTO, validator, mapper, Unity JSON codec
- `/v2/npc/respond`와 `/v2/npc/sessions/reset`
- 프로세스 메모리 기반 bounded session store
- 성공 turn의 user/assistant 텍스트만 원자적으로 commit
- TTL, LRU, turn 수, UTF-8 byte, session 수 제한
- 같은 session의 respond/reset 동시 실행 거부와 캐릭터 결합
- `IResettableAiConversationClient`, session gateway/client, Controller reset gate
- Luna·Guard가 독립 session을 쓰는 Editor 생성 샘플

V1 stateless endpoint, Mock scene, 기존 Backend scene과 공개 `IAiConversationClient`는 변경하지 않는다. 디스크·장기·요약·Vector 기억, OpenAI-managed conversation, 자동 재시도, 음성은 제외한다.

## 데이터 흐름과 경계

```text
CharacterProfile + user text
  → NpcAIController (send/reset shared gate)
  → SessionBackendConversationClient (stable sessionId)
  → Contract V2 + UnityWebRequest
  → Fastify V2 validation
  → session lease + committed bounded history
  → OpenAI input: history user/assistant pairs + current user
  → Structured Output (`store: false`)
  → successful turn commit
  → V2 response → Core response → presentation
```

- Core에는 reset 가능 여부를 나타내는 선택형 interface만 추가한다.
- Transport V2는 Core만 참조하고 UnityEngine에 의존하지 않는다.
- Unity가 session ID와 HTTP를 소유하며 OpenAI SDK를 참조하지 않는다.
- Backend만 history를 보유하고 로그에는 session ID나 대화 본문을 남기지 않는다.

## 확정 제한과 정책

| 항목 | 기본값 | 환경변수 | 허용 범위 |
| --- | ---: | --- | ---: |
| 완료 turn 수 | 8 | `NPC_SESSION_MAX_TURNS` | 1–32 |
| context UTF-8 bytes | 16 KiB | `NPC_SESSION_MAX_CONTEXT_BYTES` | 4–128 KiB |
| idle TTL | 1,800초 | `NPC_SESSION_IDLE_TTL_SECONDS` | 60–86,400초 |
| session 수 | 128 | `NPC_SESSION_MAX_COUNT` | 1–4,096 |

문자열 일부는 자르지 않고 가장 오래된 완전한 turn부터 제거한다. 만료 session은 다음 접근에서 빈 session으로 시작한다. capacity 도달 시 만료 항목을 정리하고 가장 오래 idle인 session을 제거하며, 모두 busy면 `session_capacity_reached`를 반환한다.

## 구현 결과

- Server generator 입력을 wire DTO에서 분리하고 V1에는 항상 빈 history를 전달한다.
- V2는 같은 session의 성공한 대화만 순서대로 replay한다.
- 실패·취소 시 pending 입력을 저장하지 않고 session lease를 해제한다.
- Reset은 알 수 없는 ID에도 성공하고 기존 session의 캐릭터 결합은 보존한다.
- Unity session ID는 `session-<32자리 GUID>`이고 component가 disable/enable되거나 reset되어도 유지된다.
- `NpcAIController`는 send와 reset에 하나의 busy/cancellation gate를 사용한다.
- Reset 성공 시 초기 대사, 기본 감정, `None` 제스처로 복귀한다.
- `MemoryNpcPrototype.unity`는 Editor API로 생성했고 기존 세 씬은 재생성하지 않았다.

## 자동 검증 결과

- `npm.cmd run build`: 통과
- Server Vitest: **43/43 통과**, 실제 OpenAI 호출 없음
- Unity 6000.5.3f1 batchmode compile/scene builder: 통과
- Unity EditMode: **72/72 통과**, 실패·건너뜀 0
- 결과: `E:\CodexValidation\Phase5\TestResults.xml`
- 로그: `E:\CodexValidation\Phase5\MemorySceneBuilder.log`, `EditMode.log`
- `Packages/`와 `ProjectSettings/` 변경 없음

## 수동 검증

1. `server/`에서 `OPENAI_API_KEY`를 현재 process 환경에만 설정하고 `npm run dev`를 실행한다.
2. `MemoryNpcPrototype.unity`를 열어 Play Mode로 진입한다.
3. Luna와 Guard에게 서로 다른 사실을 말한 뒤 각각 재질문해 독립적으로 기억하는지 확인한다.
4. Luna만 Reset하고 Luna의 기억은 사라지며 Guard의 기억은 유지되는지 확인한다.
5. Reset 후 Luna가 초기 대사·기본 감정·`None` 제스처로 돌아가는지 확인한다.
6. 응답 또는 reset 진행 중 Send와 Reset 버튼이 모두 비활성화되는지 확인한다.

## 완료 기준

- [x] V1·Mock 경로와 공개 계약을 보존한다.
- [x] V2 계약, session 제한, 동시성, reset과 오류 경로를 자동 테스트한다.
- [x] 실제 OpenAI 없이 전체 Server/Unity 회귀가 통과한다.
- [x] API 키·대화·session ID를 asset, source 또는 log에 저장하지 않는다.
- [ ] 사용자가 실제 모델로 두 NPC의 기억 분리와 reset을 확인한다.
- [ ] 수동 검증 후 Phase 5 체크포인트 커밋을 만든다.

## 남은 위험

- 서버 재시작, TTL 또는 LRU eviction은 의도적으로 기억을 잃게 한다.
- UTF-8 byte 제한은 모델 token 수와 일치하지 않는다.
- 모델이 저장된 사실을 항상 정확히 회상하는지는 prompt와 모델 품질에 좌우된다.
- 같은 session에서 이미 upstream으로 전송된 요청은 client 연결 취소 시 저장되지 않지만 공급자 측 계산이 즉시 중단된다고 보장할 수 없다.
- 로컬 Backend에는 인증과 rate limiting이 없으므로 loopback 밖으로 노출하면 안 된다.
