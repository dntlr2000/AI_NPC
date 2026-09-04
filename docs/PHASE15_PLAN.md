# Phase 15 — Runtime Context & Lore Grounding

> 상태: 구현 및 자동 검증 완료 — live V4 수동 검증 전
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

수동 완료 조건:

1. Grounded Guard scene에서 gate/alarm 상태를 바꾸고 같은 질문의 근거가 달라지는지 확인한다.
2. 화면의 captured context와 `LastContextRevision`이 snapshot 변경 시 갱신되는지 확인한다.
3. reset 후 session 대화 기억은 사라지되 현재 provider 상태와 authored lore는 다시 제공되는지 확인한다.
4. action을 함께 구성했다면 model 결과와 관계없이 handler의 `CanExecute`가 최종 권한을 유지하는지 확인한다.

## 제외 범위

- 변수·점수·복합 행동 rule과 관계도
- 장기 기억, Vector DB, RAG 검색, 요약 기억
- 모델이 Unity 상태를 직접 수정하는 tool calling
- Backend 패키지화·원격 배포·client 인증
- Realtime 음성
- 로컬 모델 runtime, model file 다운로드와 native inference plugin

자동 검증과 위 수동 시나리오가 모두 통과한 뒤에만 Phase 15를 완료 처리한다. `v0.4.0` tag와 GitHub Release는 exact-commit에 대한 별도 승인이 있어야 한다.
