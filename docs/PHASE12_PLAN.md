# Phase 12 후보 — 선택적 Advanced Behavior Rules

> 상태: 후속 가안 — Phase 11 완료, 구현 승인 전 재계획 필요
> 선행 조건: Phase 11 Conversation Trigger → Action Handler 완료 충족
> package version: 미정

## 목적

Phase 11의 단순 trigger-to-handler 경계를 교체하지 않고, 누적 캐릭터 상태나 복합 조건이 실제로 필요한 consumer를 위한 선택형 Advanced 계층을 추가한다. 자연어 의미 판정은 Phase 11 trigger를 재사용하고, 변수·점수·조건 평가와 최종 행동 결정은 Unity가 소유한다.

예상 사용자 흐름은 다음과 같다.

```text
matched conversation triggers + consumer game facts
                         ↓
              per-NPC runtime variables
                         ↓
        structured threshold/compound conditions
                         ↓
              existing INpcActionHandler
```

## 후보 범위

- Character Builder의 명시적 **Advanced** 영역
- consumer가 정의하는 bool 및 bounded number 변수
- 기본값, 최소·최대값과 NPC 인스턴스별 runtime state
- matched trigger에 따른 결정적 변수 증감 또는 설정
- 비교 연산과 제한된 `ALL`/`ANY`/`NOT` 조건 조합
- action별 threshold, priority, cooldown과 once gate
- consumer game state를 읽는 provider-neutral fact/variable provider 경계
- 매 turn의 입력 signal, 변수 변화, 조건 결과와 선택 사유를 보여 주는 debug trace
- Phase 11의 `INpcActionHandler`와 동일한 최종 `CanExecute` 권한 검사

Character Builder의 시각적 구조화 규칙을 유일한 runtime source of truth로 둔다. 선택형 자연어 작성 도우미를 제공하더라도 구조화된 rule draft로 변환해 미리보기와 명시적 저장을 거치며 C#, Reflection 또는 임의 UnityEvent를 생성하지 않는다. Unity에는 provider API key를 저장하지 않는다.

## 데이터와 수명 원칙

- ScriptableObject에는 변수와 rule 정의만 저장하고 실행 중 값을 기록하지 않는다.
- 변수 값은 NPC 인스턴스별 runtime state에 둔다.
- reset 범위는 대화 session, GameObject lifetime과 consumer 명시 호출을 구분한다.
- 저장 파일, 계정, 장기 관계도와의 영속화는 Phase 12 기본 구현에 포함하지 않는다. 필요하면 consumer save adapter 또는 별도 persistence milestone로 설계한다.
- 모델이 반환한 trigger나 semantic confidence를 게임 사실로 간주하지 않는다. 퀘스트 완료, 인벤토리, 전투 상태 등은 consumer provider가 제공한다.

## Phase 11과의 관계

Phase 11의 Basic 구성은 Advanced 기능 없이 계속 동작해야 한다.

```text
Basic:
natural-language trigger → action handler

Advanced:
natural-language trigger
  + runtime variable threshold
  + optional game fact
  → same action handler
```

Phase 12는 새 행동 실행 계약을 만들지 않고 Phase 11 handler를 재사용한다. Advanced profile이 없으면 변수 저장소나 rule evaluator를 생성하지 않으며 기존 V1/V2/V3와 Mock 경로의 결과를 변경하지 않는다.

## 재계획 게이트

Phase 12 구현 전 다음 증거를 수집하고 이 문서를 다시 검토한다.

1. Phase 11 consumer에서 직접 trigger binding만으로 표현하기 어려운 실제 NPC 시나리오
2. 필요한 변수 타입, reset 범위와 저장 수명
3. 조건 조합의 최대 복잡도와 디자이너가 이해할 수 있는 Builder UX
4. 동일 turn의 여러 rule 충돌, cooldown과 action 실행 중 새 대화 정책
5. 자연어 rule 작성 도우미가 필요한지와 network-free 편집 대안

이 증거에 따라 변수 타입, 공개 API, package version과 세부 구현 순서는 변경할 수 있다. Phase 번호와 “선택형 Advanced 계층이며 Phase 11 Basic을 보존한다”는 경계만 현재 로드맵에 유지한다.

## 제외 범위

- 범용 프로그래밍 언어 또는 무제한 expression engine
- 모델이 직접 수정하는 변수와 게임 상태
- LLM tool/function calling과 임의 action parameter
- 자동 생성 C#과 Reflection 기반 메서드 호출
- Behavior Tree, Utility AI, planner와 자율 목표 선택
- 프레임워크가 구현하는 Quest, Inventory, Combat, NavMesh 또는 save system
- Vector DB, 장기 기억과 cross-player relationship service

## 후보 완료 기준

- Phase 11 Basic NPC는 설정 변경 없이 동일하게 동작한다.
- 디자이너가 코드 없이 변수·점수·복합 gate를 작성하고 평가 trace를 확인할 수 있다.
- 새 행동의 실제 효과에는 기존 consumer `INpcActionHandler`만 사용한다.
- 동일 입력과 동일 runtime/game state에서 같은 변수 변화와 행동 선택 결과가 나온다.
- AI 판정, 로컬 변수와 실제 게임 권한의 책임 경계가 테스트로 증명된다.
- 구체적인 완료 기준과 자동·수동 검증 수치는 구현 승인 시 갱신한다.
