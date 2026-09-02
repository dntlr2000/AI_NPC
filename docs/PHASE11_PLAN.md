# Phase 11 — 대화 트리거 기반 NPC 행동

> 상태: 완료 — 구현·자동 검증·별도 consumer 수동 Play Mode 검증 통과
> 기준선: `dcad604` (`v0.2.0` 공개 릴리즈)
> 목표 package: `com.aicharacterkit.framework` `0.3.0`

## 목표

Character Builder에서 캐릭터별 자연어 대화 조건을 action ID에 연결하고, consumer는 실제 게임 행동을 수행하는 handler만 구현한다. AI는 등록된 조건의 충족 여부만 구조화해 반환하며, 어떤 행동이 허용되고 실제로 실행 가능한지는 Unity가 최종 결정한다.

Phase 11은 다음 사용자 흐름을 목표로 한다.

1. Character Builder에서 `NpcActionProfile`을 만들거나 선택한다.
2. trigger ID, 자연어 조건, Mock 예시 입력, action ID와 우선순위를 작성한다.
3. Scene 또는 Prefab NPC에서 해당 action ID를 제공하는 handler를 선택한다.
4. Mock에서는 예시 입력으로 wiring을 결정적으로 검증한다.
5. Backend Action mode에서는 같은 대화 응답에 포함된 matched trigger ID로 행동을 실행한다.

## 고정 설계

```text
CharacterProfile + NpcActionProfile + user text
                    ↓
       IAiConversationClient / V3 Backend
                    ↓
 dialogue + emotion + gesture + matched trigger IDs
                    ↓
       deterministic trigger selection
                    ↓
          INpcActionHandler.CanExecute
                    ↓
          INpcActionHandler.ExecuteAsync
```

- `NpcActionProfile`은 consumer-owned ScriptableObject이며 trigger/action binding을 데이터로 보관한다.
- 각 binding은 `triggerId`, 자연어 `conditionDescription`, 결정적 Mock용 `exampleUserText`, `actionId`, `priority`만 가진다.
- 자연어 조건을 C#이나 Reflection 호출로 변환하지 않는다.
- Backend는 요청에 등록된 trigger ID만 반환할 수 있다. action ID, 메서드명, 타입명과 Scene target은 생성하지 않는다.
- 여러 trigger가 일치하면 높은 priority, 같은 priority에서는 profile 선언 순서로 한 개만 선택한다.
- handler의 `CanExecute`가 거리, 퀘스트, 인벤토리, 대상 존재 여부 등 실제 게임 조건을 최종 검사한다.
- 행동 실패는 이미 성공한 대화를 실패로 되돌리지 않으며 별도 행동 결과로 보고한다.

## 공개 확장 경계

- 순수 Core에 성공 turn을 전달하는 선택형 turn observer와 provider-neutral action context/result 계약을 추가한다.
- `INpcActionHandler`는 action ID, 실행 가능 여부와 취소 가능한 실행 계약을 제공한다.
- Unity에는 선택형 `NpcActionHandlerBase : MonoBehaviour, INpcActionHandler`를 제공한다. 기본 검사는 `virtual`, 실제 실행은 consumer가 구현하게 한다.
- 기존 `IAiConversationClient`, `INpcPresentationDriver`와 `NpcAIController` 생성 경로는 유지한다. 새 observer를 사용하지 않으면 기존 동작과 결과가 바뀌지 않는다.
- `AiNpcResponse`의 기존 생성자를 보존하고 matched trigger가 없는 기존 Mock/V1/V2 응답은 빈 결과로 취급한다.
- Character Builder는 consumer MonoBehaviour의 `INpcActionHandler` 구현을 찾아 연결하지만 게임별 행동 스크립트를 생성하지 않는다.

## V3 대화 계약

V1 stateless와 V2 session 계약은 변경하지 않는다. V3는 V2 session/reset 의미를 계승하고 bounded trigger snapshot과 matched trigger IDs를 추가한다.

- `POST /v3/npc/respond`
- `POST /v3/npc/sessions/reset`
- 요청은 trigger ID와 자연어 조건만 전송하며 action binding과 Scene 참조는 Unity 밖으로 보내지 않는다.
- 응답은 요청에 포함된 알려진 trigger ID만 반환할 수 있다.
- trigger 수와 ID·설명 길이를 제한하고 중복·알 수 없는 ID·잘못된 branch를 거부한다.
- OpenAI Structured Output은 대사·감정·제스처와 trigger 판정을 한 번의 응답으로 생성한다. 별도 조건 평가 API 호출은 만들지 않는다.
- 기존 V1/V2와 action 없는 Mock 경로는 회귀 테스트로 보존한다.

## Mock과 Editor 동작

Mock은 자연어 의미를 해석한다고 가장하지 않는다. 정규화된 사용자 입력이 binding의 `exampleUserText`와 일치할 때만 해당 trigger를 결정적으로 match한다.

Character Builder에는 선택형 **Conversation Actions** 영역을 추가한다.

- action profile 생성·편집 및 binding 추가·삭제
- 비어 있거나 중복된 trigger/action ID, condition과 Mock 예시 검증
- 대상 GameObject 또는 Prefab의 action handler 검색과 action ID 연결 확인
- Mock trigger preview와 선택 결과 표시
- Scene Undo, Prefab 격리 저장, 재적용 멱등성과 consumer asset 소유권 유지

기존 profile, presentation, View, TTS 구성은 그대로 유지하며 action 기능은 선택하지 않으면 아무 component도 추가하지 않는다.

## 테스트 전략

- profile 필수값, 중복 ID, priority와 안정적인 한 개 선택
- 결정적 Mock example match와 미일치 처리
- 알 수 없는 Backend trigger ID 거부와 등록된 ID routing
- handler 없음, `CanExecute` 거부, 성공, 실패, 취소와 중복 실행
- 행동 실패가 dialogue 성공 상태를 변경하지 않는지 확인
- V3 DTO·validator·mapper·codec golden fixture와 V1/V2 회귀
- Backend structured output, session/reset, trigger bounds와 잘못된 응답 검증
- Character Builder의 Scene/Regular·Variant Prefab 적용, Undo와 재적용
- consumer-owned custom handler 검색 및 package 제거·업그레이드 후 보존
- 기존 Server, Unity EditMode, sample와 Built-in/Legacy consumer 전체 회귀

샘플은 자연어 인사에 반응하는 즉시 완료형 행동 하나와, `CanExecute`가 게임 상태를 거부할 수 있는 행동 하나만 제공한다. 실제 게임별 NavMesh, Door, Quest와 Combat 구현은 consumer 테스트 fixture에 둔다.

## 구현 및 자동 검증 기록

- local UPM package를 `0.3.0`으로 갱신하고 Core action 경계, `NpcActionProfile`, V3 Transport/Unity networking, Backend V3와 Character Builder action 영역을 구현했다.
- importable `ActionNpc` sample은 package에 script와 Editor builder만 포함하며, Scene과 profile asset은 import 후 Editor API가 생성한다.
- Server TypeScript build와 Vitest **85/85**가 통과했다.
- Unity `6000.5.3f1` root compile, sample import/repair, Action Scene 생성과 EditMode **167/167**이 통과했다.
- 별도 Built-in/Legacy consumer가 local `0.3.0`을 resolve했고 EditMode **167/167**, consumer-owned handler PlayMode **2/2**, Windows Development Player build가 통과했다.
- 첫 수동 Play Mode에서 action sample handler 두 개가 단일 source file에 있어 Editor 재시작 후 Scene 참조가 유실되는 결함을 발견했다. handler를 클래스명별 `MonoScript` 파일로 분리하고 missing-script 회귀를 추가한 뒤 root compile, 집중 Scene 테스트 **1/1**과 전체 EditMode **167/167**을 다시 통과했다.
- 수정된 별도 consumer에서 Character Builder 재적용과 Mock `hello`, 잠긴 `open_gate` 거부, unlock 뒤 실행이 모두 정상 동작했다.
- matching local Backend/OpenAI 환경에서 Mock 예시와 다른 동의 표현의 V3 semantic trigger가 정상적으로 consumer action을 실행했다.
- `0.3.0` 릴리즈 전 문서 보강으로 package `ACTIONS_QUICKSTART.md`를 추가하고 handler 작성부터 Builder binding, Mock/live V3, 제한과 troubleshooting까지 단일 경로로 연결했다. 문서의 `OpenGateActionHandler` 예제는 Unity `6000.5.3f1`에서 실제 compile했고 상대 링크 검사를 통과했다.
- package dependency, root `Packages/manifest.json`, `Packages/packages-lock.json`과 `ProjectSettings`는 변경하지 않았다.
- 검증 log/result는 `E:\CodexValidation`, TEMP/TMP는 `E:\CodexTemp`에만 만들었다. Root에 임시 import한 `Assets/Samples`는 검증 후 제거했으며 별도 consumer의 검증 asset은 `E:\CodexValidation`에 남겼다.

자동·수동 완료 gate를 모두 통과했으므로 Phase 11은 package `0.3.0` 체크포인트로 완료 처리했다. 구현 체크포인트는 `c38b5a0`, 완료 문서 기준선은 `7cf0a63`이며, 공개 릴리즈는 별도 준비·검증과 exact-commit 승인을 거친다.

## 완료 조건

- 사용자는 Character Builder에서 자연어 조건과 action handler를 연결할 수 있다.
- 새 행동 추가에는 Core, Backend schema나 Character Builder 수정 없이 consumer handler 구현과 profile binding만 필요하다.
- AI가 임의 action, 메서드, 타입 또는 Scene object를 지정할 수 없다.
- 한 turn에서 최대 한 행동이 결정적으로 선택되며 실패·취소가 안전하게 격리된다.
- Mock은 network 없이 동일 입력에 동일 trigger/action 결과를 낸다.
- V1/V2, 기존 Mock, 대화·기억·TTS·STT와 `v0.2.0` 사용 경로의 호환성이 유지된다.
- 자동 검증과 별도 consumer 수동 Play Mode가 끝난 뒤에만 package를 `0.3.0` 체크포인트로 완료 처리한다.

## 명시적 제외 범위

- 사용자 정의 변수, 누적 점수, 호감도·관계도와 영구 저장
- 범용 `AND`/`OR` 조건 트리, 수식 또는 자연어-to-code 생성
- 여러 행동의 병렬·연속 실행, planner, Behavior Tree와 Utility AI
- LLM tool/function calling과 모델이 생성하는 action parameter
- Reflection 메서드 호출, 임의 UnityEvent 호출과 Scene object 이름 해석
- NavMesh, Animator, Quest, Inventory, Combat 등 게임별 시스템 구현
- Realtime, remote Backend deployment와 client authentication

변수·점수와 복합 규칙은 [`PHASE12_PLAN.md`](PHASE12_PLAN.md)의 선택형 Advanced Behavior 후보로 분리한다. Phase 11 consumer에서 직접 trigger binding만으로 해결할 수 없는 실제 요구를 확인한 뒤 Phase 12의 세부 범위와 API를 재계획한다.

## 구현 순서

1. [완료] Core turn observer, action context/result와 handler 계약을 추가한다.
2. [완료] action profile 검증, 결정적 Mock match와 action routing을 구현한다.
3. [완료] V3 Transport/Unity codec와 Backend schema·generator·session endpoint를 추가한다.
4. [완료] `NpcConversationBehaviour`에 선택형 action composition을 연결한다.
5. [완료] Character Builder의 action profile 편집·preflight·Scene/Prefab 적용을 추가한다.
6. [완료] 최소 sample과 전체 자동 회귀를 실행한다.
7. [완료] 별도 consumer에서 custom handler와 live Backend trigger를 수동 검증한다.
