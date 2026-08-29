# Phase 4 — Local Backend and OpenAI Structured Output

> 상태: 완료 — `main`의 `Phase4` 체크포인트
> 기준 커밋: `cfc5b04 (Phase3)`
> 목표: API 키를 Unity 밖에 둔 채 V1 계약으로 실제 모델 응답 하나를 화면에 표시한다.

## 구현 범위

- Node.js 24 + TypeScript + Fastify 기반 loopback backend
- OpenAI Responses API와 strict Structured Output
- `BackendConversationClient`와 교체 가능한 `IAiNpcBackendGateway`
- `UnityWebRequest` POST, request ID 상관관계, 취소와 timeout
- 안전한 V1 오류/HTTP 매핑과 민감정보 없는 구조화 로그
- 기존 Luna 프로필을 재사용하는 별도 Backend NPC 샘플 씬
- 기존 Mock 씬과 Phase 1~3 공개 계약의 회귀 보존

기억, 음성, streaming, tool calling, 원격 배포, client auth, 자동 재시도는 구현하지 않는다.

## 데이터 흐름과 경계

```text
CharacterProfile + user text
  → Core AiNpcRequest
  → BackendConversationClient
  → Contract V1 request + generated requestId
  → UnityWebRequest (loopback JSON)
  → Fastify validation
  → OpenAI Responses Structured Output
  → server-owned V1 success/error envelope
  → correlation + contract validation
  → Core AiNpcResponse
  → existing INpcPresentationDriver
```

- Core는 Unity, JSON, HTTP, OpenAI를 참조하지 않는다.
- Transport는 Core만 참조하고 `noEngineReferences: true`다.
- Unity networking은 API 키나 OpenAI SDK를 알지 못한다.
- 서버는 `OPENAI_API_KEY`를 process environment에서만 읽고 prompt/response body를 기록하지 않는다.

## 확정 설정

- Endpoint: `http://127.0.0.1:8787/v1/npc/respond`
- Model default: `gpt-5.6-luna` (`OPENAI_MODEL`로 교체 가능)
- OpenAI timeout 30초, Unity timeout 35초, SDK retry 0
- `store: false`, reasoning effort `none`, max output tokens 256
- 요청 본문 최대 16 KiB, 모델 dialogue 최대 600자

오류 코드와 HTTP binding은 [`CONTRACT_V1.md`](CONTRACT_V1.md)를 따른다.

## 자동 검증 결과

- `npm run build`: 통과
- Server Vitest: **20/20 통과**, 실제 OpenAI 호출 없음
- Unity 6000.5.3f1 batchmode 컴파일: 통과
- Unity EditMode: **54/54 통과**, 실패·건너뜀 0
- Editor API scene builder: Backend scene 생성 및 복구 경로 통과
- 기존 Mock scenes: `conversationMode = Mock` 회귀 확인

## 수동 검증 결과

- 2026-08-29 사용자가 로컬 환경변수로 API 키를 주입하고 Backend NPC scene을 Play Mode에서 검증했다.
- 실제 OpenAI Structured Output이 V1 응답으로 전달되어 대사·감정·제스처 표현까지 정상 동작함을 확인했다.
- 검증 후 커밋 제목이 `Phase4`인 체크포인트를 `main`에 생성했다.

## 완료 기준

- [x] 서버와 Unity가 동일 V1 fixture 및 token을 검증한다.
- [x] 성공, 거절, rate limit, timeout, upstream 및 protocol 오류가 안전하게 매핑된다.
- [x] API 키와 raw dialogue가 Unity asset 또는 application log에 없다.
- [x] Packages와 ProjectSettings를 변경하지 않았다.
- [x] 서버와 Unity 자동 테스트가 모두 통과했다.
- [x] 사용자가 환경변수로 키를 설정하고 Backend scene에서 실제 응답 1회를 확인했다.
- [x] 라이브 검증 후 `main`에 Phase 4 체크포인트 커밋을 만들었다.

## 남은 위험

- 현재 개발 환경의 모델 접근 권한과 end-to-end 경로는 smoke test로 확인했지만, 향후 quota와 네트워크 상태는 실행 시점에 따라 달라질 수 있다.
- 로컬 서버에는 인증과 rate limiting이 없으므로 원격 노출하면 안 된다.
- 모델 응답의 자연스러움과 캐릭터 일관성은 단 1회 smoke test만으로 보장되지 않으므로 별도 평가 기준이 필요하다.
- Unity의 플랫폼별 `UnityWebRequest` timeout 문구 차이는 현재 Windows Editor 경로만 대상으로 한다.
