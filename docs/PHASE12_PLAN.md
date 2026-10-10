# Phase 12 — 선택형 Advanced Behavior Rules

> 확정 계획: 사용자의 2026-10-03 구현 승인
> 상태: 승인 범위 구현 및 현재 소스 자동 실행 검증 완료, Unreleased
> 기준: Server 106개, Unity EditMode 248개. 이번 결과는 [별도 검증 기록](PHASE12_VALIDATION.md)에 남긴다.
> Unity 6000.5.3f1, package 0.4.0 및 기존 의존성·공개 태그 유지

## 목표와 범위

기존 Basic 트리거와 consumer 행동 핸들러 위에 NPC별 bool·범위 제한 숫자·복합 조건을 작성하는 선택형 Advanced 계층을 추가한다. 호감도는 대표 사용 사례다. AI는 설정된 대화 의도만 판정하고, 변경량·조건·행동 선택과 실행 권한은 Unity가 결정한다. 구조화 편집과 오프라인 미리보기를 제공하며 자연어 작성 도우미·코드 생성은 제외한다.

저장·불러오기, 장기 관계 서비스, 자율 행동, 범용 수식 언어, Phase 13·14는 구현하지 않는다. 기존 Phase 15 grounding과 Phase 16 로컬 대화·행동 결과 최종화를 보존한다. push·태그·릴리즈 발행은 포함하지 않는다.

## 모듈과 공개 경계

- `AiCharacterKit.Behavior`는 Core만 참조하는 순수 C# 모듈이다. 정의 검증, 불변 정의 복사, NPC별 상태, 평가와 trace를 소유한다.
- `NpcBehaviorProfile`은 변수·외부 값 선언·대화 신호·상태 변경 규칙·행동 규칙의 정의만 저장한다.
- `NpcBehaviorRuntime`은 초기화 시 정의를 복사하고 단조 증가 시간을 주입받을 수 있다.
- `INpcBehaviorValueProvider.CaptureValues()`가 consumer의 typed bool/number 상태를 제공한다. 자연어 context fact를 조건으로 해석하지 않는다.
- `NpcActionCoordinator`는 Basic(기본값) 또는 Advanced 프로필 하나만 평가한다. Basic에서는 Behavior 상태 저장소를 만들지 않는다.
- 상태 조회는 읽기 전용 snapshot이며 `TrySetBoolean`, `TrySetNumber`, `TryAddNumber`, `TryResetBehaviorState`와 마지막 평가 trace를 제공한다. 대화·행동·기억 reset 중 변경과 초기화는 Busy로 거부한다.
- `NpcTriggerDefinition.CreateSignal(...)`은 행동 없는 신호를 만든다. 기존 생성자·Basic 검증은 유지한다. Advanced 행동 선택 후 실제 action ID를 가진 기존 `NpcActionContext`로 공통 executor를 호출한다.
- `INpcActionHandler`, `IAiConversationClient`, V1~V4 wire/schema는 유지한다. Mock, V3, V4, LocalInference에서 Advanced를 지원하고 V1/V2 조합은 정적 검사에서 거부한다.

## 데이터와 평가 규칙

숫자는 유한한 double이며 기본값·최소·최대를 검사한다. 런타임 설정·증감은 범위로 제한하고 NaN/Infinity를 거부한다. bool 설정, 숫자 설정·증가·감소만 허용하며 값은 작성자가 정한 상수다. 숫자 동등 비교는 정확한 비교이고 점수 임계값에는 >=·<=를 사용한다.

조건은 변수/외부 값과 상수의 비교, ALL·ANY·NOT으로 구성한다. 반복·임의 수식은 없다. Unity 직렬화에는 부모 인덱스를 가진 제한된 조건 트리를 사용한다.

| 항목 | 상한 |
| --- | --- |
| 변수 / 외부 값 / 대화 신호 | 32 / 32 / 16 |
| 전체 규칙 / 규칙당 변경 | 64 / 16 |
| 조건 깊이 / 노드 | 4 / 32 |
| 명시적으로 켠 런타임 trace | 최근 32턴 |

한 응답의 처리 순서는 다음과 같다.

1. 미등록·중복 trigger ID와 취소를 검사한다. 생성 실패·취소·잘못된 응답에는 상태와 행동을 반영하지 않는다.
2. 신호가 있으면 외부 provider를 각각 한 번 캡처하고 불변 복사한다. null, 예외, 누락·중복·미등록 ID·타입 오류는 부분 변경 없이 해당 턴을 실패시킨다.
3. 모든 변경 규칙 조건은 변경 전 snapshot으로 판정한다. 일치한 변경을 선언 순서로 적용한 뒤 한 번에 확정한다. 변경으로 다른 규칙을 연쇄 실행하지 않는다.
4. 행동 조건은 변경 후 변수와 같은 외부 snapshot으로 판정한다. cooldown·once를 통과한 후보 중 높은 priority, 동률이면 선언 순서로 하나를 선택한다.
5. 실행 직전 `CanExecute`를 호출한다. 거절·실패 시 다른 행동으로 대체하지 않는다. 성공한 행동 ID에만 cooldown·once를 기록한다. 같은 action ID를 쓰는 규칙은 동일 정책과 실행 기록을 공유한다.

확정 전 취소는 변경하지 않는다. 확정 후 행동 실패·거절·취소에는 대화로 인한 변수 변화를 유지한다. consumer의 실제 효과를 자동 롤백하지 않는다. 외부 상태 변화만으로 행동을 실행하거나 자동 재요청하지 않는다.

## 수명과 grounding

- NPC 비활성화·재활성화와 대화 기억 reset은 변수·cooldown·once를 유지한다.
- 행동 상태 reset은 변수·실행 기록·trace만 초기화하고 대화 기억은 유지한다.
- NPC 재생성과 새 Play 세션은 기본값으로 시작한다. 상태는 인스턴스별이며 정적 전역 저장소나 디스크에 보관하지 않는다.
- Builder·Awake 정적 검사에서는 provider·CanExecute·모델 실행기를 호출하지 않는다. 늦게 준비되는 provider는 다음 턴에 같은 NPC에서 복구할 수 있다.
- 선택한 변수는 읽기 전용 `NpcBehaviorContextProvider`가 요청 직전에 기존 V4 grounding으로 내보낸다. 기존 fact 수·크기·ID 충돌·revision 검사를 유지한다.
- 이번 응답으로 바뀐 값은 다음 요청부터 모델 입력에 반영된다. 같은 턴 행동 조건은 변경 후 값을 쓴다. 추가 모델 호출은 없다.
- V3에서는 규칙이 동작하지만 변수 grounding은 제공하지 않는다. LocalInference의 현재 상태 우선 처리와 실제 행동 결과에 따른 대사·기억 최종화를 유지하고 중복 실행을 막는다.

## Character Builder와 샘플

Advanced 편집에는 typed 변수·외부 값·신호, 상태 변경 규칙, 행동 조건·priority·cooldown·once가 포함된다. 저장 전 ID·참조·타입·범위·복잡도와 실제 handler 연결을 검사한다. Scene/Prefab 적용·재적용, 기존 설정 읽기와 Undo를 지원한다. Prefab에는 외부 Scene 참조를 저장하지 않는다.

Basic → Advanced는 명시적인 초안 가져오기로만 수행하고 원본 프로필은 유지한다. 미리보기는 입력한 synthetic 변수·외부 값·신호로 실제 평가기를 사용하되 provider·실제 handler·네트워크를 호출하지 않는다. trace에는 ID, 변수 전후 값, 조건 결과, 선택/제외 사유와 행동 결과만 둔다. 대화·grounding·예외 원문은 기록하지 않는다.

새 `Advanced Relationship NPC` 샘플은 Unity Editor API로 생성한다. 기본 Mock에서 두 NPC가 각자 affinity(40, 0~100)와 greeted(bool)를 갖고 문·경보·허가를 공유한다. 칭찬 +10, 모욕 -10, 인사 bool 설정 및 1회 행동을 보여 준다. 문 열기는 affinity >=60·허가·경보 비활성을 요구하며 5초 cooldown을 사용한다. UI는 변수·공유 상태·실제 행동 결과·대화 reset·행동 reset을 제공한다. 동일 규칙을 Backend V4 fake와 실제 로컬 모델로 검증한다. 기존 Local Grounded NPC 씬은 유지한다.

## 검증과 완료 기준

전체 Server/Unity 회귀에 다음을 추가한다.

- 숫자 경계·소수·범위 제한, 잘못된 숫자·타입·참조·조건 깊이
- 여러 신호·변경 순서·변경 전 조건·변경 후 임계값
- priority·동률·cooldown·once·거절 시 대체 미실행
- 생성/행동 실패 구분, 취소 확정 경계, 중복 제출와 상태 변경/reset Busy
- provider 지연 준비·오류 후 복구, NPC 격리와 reset/disable/재생성 수명
- Basic·Mock·V1~V4·음성·로컬 최종화 유지
- Builder 미리보기·Scene/Prefab 적용·재적용·Awake에서 provider 캡처·행동 실행·모델 시작 0회

라이선스를 확인하고 현재 수정 소스의 root 복제본 및 Built-in/Legacy consumer에서 compile·EditMode·Play Mode를 수행한다. 실제 Gemma로 호감도 누적, 허용/거절, 격리, 기억/행동 reset, 실제 결과 대사와 기억을 검증한다. Windows x64 Mono에서도 핵심 시나리오·실행기 정리를 검증한다. Backend는 fake만 사용하며 OpenAI를 호출하지 않는다. 기존 로컬 샘플 회귀도 실행한다. 실행 검증이 막히면 전체 완료로 표시하지 않는다.

[사용 안내](ADVANCED_BEHAVIOR_QUICKSTART.md), [검증 기록](PHASE12_VALIDATION.md), 로드맵·저장소 지침·README·Unreleased를 함께 갱신한다. 새로운/수정한 메서드에는 기능 주석을 달고 기존 YAML/GUID는 직접 변경하지 않는다.

검증은 `E:\CodexValidation\AI_NPC-phase12-20261003-a1`, TEMP/TMP는 `E:\CodexTemp\AI_NPC-phase12-20261003-a1`를 사용한다. 최종 빌드는 delivery, 소스 식별·명령·필수 증거는 evidence에 보존한다. 완료 전 이번 작업의 불필요한 복제본·캐시·중간 산출물을 소유권과 사용 여부를 확인하고 정리한다.
