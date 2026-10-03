# Phase 15 — Runtime Context & Lore Grounding

> 상태: 완료 — 자동 검증 및 live V4 수동 Play Mode 검증 통과
> 기준선: `42f5c59` (`v0.3.0` 공개 릴리즈)
> 목표 package: `com.aicharacterkit.framework` `0.4.0`

## 목표

특정 캐릭터를 코드에 고정하지 않고, 디자이너가 작성한 캐릭터 canon과 세계관 lore 및 매 요청 시점의 게임 상태를 하나의 제한된 snapshot으로 대화에 제공한다. 모델 응답은 이 정보에 근거하되 실제 게임 상태 변경과 행동 허가는 계속 Unity consumer가 소유한다.

```text
CharacterProfile canon + NpcLoreProfile
               + INpcContextProvider live facts
                            ↓
              NpcContextCoordinator
                            ↓
       immutable, bounded NpcGroundingSnapshot
                            ↓
        AiNpcRequest → V4 Backend → dialogue/actions
```

## 구현 범위

- Core: `NpcContextFact`, `NpcGroundingSnapshot`, `INpcContextProvider`, `NpcContextAssembler`
- Unity: `NpcLoreProfile`, `NpcContextProviderBehaviour`, `NpcContextCoordinator`
- CharacterProfile: background, goals/values, behavioral rules, 추가 대화 예시
- Transport: Unity 비의존 `AiCharacterKit.Transport.V4` DTO, validator, mapper
- Unity networking: V4 codec, gateway, session client와 `BackendContext` mode
- Backend: `POST /v4/npc/respond`, `POST /v4/npc/sessions/reset`, structured grounding instructions
- Editor: Character Builder canon/lore/provider 작성·연결과 authored snapshot preview
- Sample: Editor API가 생성하는 Grounded Guard, gate/alarm live observation UI

V1–V3, Mock, TTS, STT와 기존 action handler 계약은 변경하지 않는다. V4 trigger 배열은 비어 있어도 되며, 사용할 경우 V3와 같은 subset 검증과 Unity 최종 `CanExecute`를 유지한다.

## 고정 데이터 규칙

- fact ID: lower `snake_case`, 최대 64자, snapshot 내 중복 금지
- fact kind: `lore`, `belief`, `observation`
- fact: 최대 32개, statement당 512 UTF-8 bytes, 합계 12 KiB, priority 0–100
- behavioral rules: 최대 16개, 항목당 512 UTF-8 bytes
- dialogue examples: 최대 8개, 항목당 1 KiB
- background와 goals/values: 각각 최대 2 KiB
- 초과 fact는 높은 priority, 같은 priority에서는 ID 순으로 선택하고 누락 ID를 진단용으로 반환한다.
- snapshot revision은 정규화된 전체 내용에서 계산한 결정적 `ctx-<sha256>` 값이다.

## 수명과 신뢰 경계

`NpcConversationBehaviour`는 Send 직전에 provider를 읽고 snapshot을 만든다. provider의 mutable collection은 보관하지 않는다. Backend는 grounding을 해당 OpenAI 요청 instruction에만 넣으며 세션 history, log 또는 disk에 저장하지 않는다. 세션에는 기존처럼 성공한 user/assistant text만 남는다.

Grounding은 응답 품질을 위한 입력이지 권한 판정이 아니다. 모델이 gate가 열렸다고 말해도 실제 문 열기, 아이템 지급, 퀘스트 진행은 consumer action handler가 현재 상태를 다시 검사한 뒤 수행한다. 동일 turn에서 대화 생성과 게임 상태 변경을 원자적으로 commit하는 기능은 포함하지 않는다.

## 검증

- Server TypeScript build와 Vitest **95/95** 통과
- Unity 6000.5.3f1 batchmode compile 통과
- sample import/repair, Action/Grounded scene Editor 생성 통과
- Unity 전체 EditMode **186/186** 통과
- Core/Transport Unity 비의존, Runtime `UnityEditor` 비참조, loopback/security 경계를 정적 검사한다.

수동 검증 결과:

1. Grounded Guard scene에서 gate/alarm 상태를 바꿨을 때 같은 질문의 근거가 달라지는 것을 확인했다.
2. 화면의 captured context와 `LastContextRevision`이 snapshot 변경 시 갱신되는 것을 확인했다.
3. reset 후 session 대화 기억은 사라지되 현재 provider 상태와 authored lore가 다시 제공되는 것을 확인했다.
4. gameplay action 권한은 기존과 같이 consumer handler의 `CanExecute`가 최종 소유함을 자동 회귀와 기존 수동 시나리오로 확인했다.

## 제외 범위

- 변수·점수·복합 행동 rule과 관계도
- 장기 기억, Vector DB, RAG 검색, 요약 기억
- 모델이 Unity 상태를 직접 수정하는 tool calling
- Backend 패키지화·원격 배포·client 인증
- Realtime 음성
- 로컬 모델 runtime, model file 다운로드와 native inference plugin

자동 검증과 위 수동 시나리오가 모두 통과해 Phase 15를 완료 처리했다. 2026-09-04 exact commit `027b61f053c620d4e5202830b18692a6d45d3458`의 `v0.4.0` tag와 GitHub Release가 공개됐다. 상세 기록은 [RELEASE_0.4.0.md](RELEASE_0.4.0.md)를 따른다.

## 2026-09-08 안정화 — Unreleased

공개 이후 검토에서 발견한 다음 세 항목만 보완한다. 기존 필드 한도, version, dependency와 공개 tag를 유지하며 Phase 12~14·16은 구현하거나 재계획하지 않는다.

- `/v4/npc/respond`에만 실제 직렬화된 UTF-8 JSON 기준 512 KiB 상한을 적용한다. Unity gateway도 전송 전에 같은 상한을 검사하며 초과 시 `invalid_request`, `retryable=false`로 실패한다. 기존 경로의 32 KiB 및 STT audio 제한은 유지한다.
- `NpcContextCoordinator.TryValidateConfiguration`은 authored 데이터와 provider 참조만 검사한다. Builder/Awake는 이 메서드를 사용하며 `CaptureFacts()`를 실행하지 않는다. 기존 `TryValidate`와 `TryCreateSnapshot`의 live 검사 의미는 유지한다. `Start` 이후 준비되는 provider의 캡처 실패는 해당 제출만 실패시키고 다음 제출에서 다시 현재 상태를 읽는다.
- 로드맵과 릴리즈 문서를 실제 `v0.4.0` 공개 기록에 맞추고 공개 시점의 검증과 이번 수정본 검증을 구분한다.

### 수정본 검증 결과 — 2026-09-08

세 항목의 구현과 회귀 검증을 완료했다. 앞의 95/95·186/186과 live 수동 결과는 공개 시점의 기록이며, 아래 수치는 이번 `Unreleased` 수정본에서 새로 실행한 결과다.

| 검증 | 실제 결과 |
| --- | --- |
| Server TypeScript build 및 전체 Vitest | build 성공, **106/106** 통과(기존 95 + 새 회귀 11) |
| 현재 소스의 root 프로젝트 복제본 | Unity `6000.5.3f1` 컴파일 성공, 전체 EditMode **203/203** 통과(기존 186 + 새 회귀 17) |
| Built-in/Legacy consumer 복제본 | 수정 package의 로컬 snapshot으로 컴파일 성공, 전체 EditMode **203/203** 통과 |
| 실제 Play Mode 수명 주기 | EditMode suite에서 Play Mode에 진입해 `Awake` 호출 0회, `Start` 준비 전 실패와 이후 같은 NPC/controller 복구, snapshot/revision 변경 및 reset 통과 |
| Scene/Prefab Builder | 적용·재적용과 정적 검사에서 provider 캡처 0회; 기존 참조와 중복 방지 회귀 통과 |
| 요청 크기 | 34,994바이트 재현, 최대 필드/한글/이스케이프 조합, 524,288바이트 허용 및 1바이트 초과 거부 통과 |

Unity gateway의 정확한 상한 요청은 loopback fake HTTP 서버가 수신한 바이트 수로 확인했다. 초과 요청은 HTTP 전송 전에 고정 `invalid_request`로 실패했다. Server 초과 요청은 generator 호출 없이 V4 HTTP 413을 반환했으며 V1~V3, reset, TTS·STT 회귀도 통과했다. Provider 예외·null 결과·fact 충돌의 안전한 실패, 기존 live 검사 API와 `CanExecute` 회귀를 포함한다. 실제 OpenAI 호출은 사용하지 않았다.

라이선스 사전 확인과 Unity 실행은 정상 종료했다. 검증 root는 `E:\CodexValidation\AI_NPC-phase15-stabilization-20260908`, `TEMP`·`TMP`는 `E:\CodexTemp`다. Consumer package lock은 공개 Git 캐시가 아닌 `package-snapshot`의 `source: local`을 가리키며, 두 검증 환경의 C# 소스와 asmdef를 현재 작업 트리와 대조했다. 최종 결과 파일은 `server-tests-final.json`, `root-editmode-final.xml`, `consumer-editmode.xml`이다.

첫 root 검증에서 확인한 샘플 미설치와 테스트용 Editor 컴포넌트/HTTP fake 구성 문제는 샘플의 Editor API 생성 및 테스트 구성 보완 후 해소했다. 기존 `.meta`와 직렬화 asset YAML은 변경하지 않았고, 새 테스트 자산의 `.meta`는 검증 Unity가 생성한 값을 보존했다. 패키지에 중복 제공하는 계약/grounding 안내와 릴리즈 기록을 함께 갱신했다. Package/Unity version, 의존성, 후속 후보 문서와 공개 tag는 그대로이며, 이후 후보 구현·push·릴리즈 발행은 수행하지 않았다.
