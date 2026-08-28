# Phase 2 Implementation Plan

> 상태: 자동 구현·검증 완료, Play Mode 수동 검증 대기
> 기준 커밋: `e2f2c8d (Phase1)`
> 목표: 네트워크 없이 동일한 프레임워크 코드로 서로 다른 Mock NPC 두 명을 구동한다.

## 구현 현황

- Luna·Guard 프로필과 `MultiCharacterMock.unity`를 Editor API로 생성했다.
- 프로필 필수 값 검증과 런타임 초기화 실패 처리를 추가했다.
- 기존 Core 계약과 Mock·Controller·Presentation 구현을 변경 없이 재사용한다.
- Unity 컴파일을 확인했고 전체 EditMode 테스트는 **20/20 통과**했다.
- `Packages/`, 기존 Mina 프로필, Phase 1 씬은 변경하지 않았다.
- 남은 종료 게이트는 새 씬의 Play Mode 수동 확인과 Phase 2 체크포인트 커밋이다.

## 목표와 범위

Phase 2는 `CharacterProfile`이 단순 보관 데이터가 아니라 캐릭터 교체 지점으로 작동함을 증명한다. Luna와 Guard가 같은 입력에 각자 결정적인 대사와 기본 감정을 반환하고, 두 NPC가 동일한 Core·Controller·Presentation 구현을 재사용해야 한다.

포함 범위:

- Luna와 Guard `CharacterProfile` 두 개
- 필수 프로필 값 검증
- 프로필별로 구분되는 결정적 Mock 응답
- 독립된 입력·출력 패널을 가진 NPC 두 명의 샘플 씬
- Editor API 기반 자산·씬 생성 및 복구
- Core, 프로필, 씬 연결에 대한 EditMode 테스트

제외 범위:

- OpenAI, HTTP, `server/`, JSON 전송 계약
- 기억, 관계도, 퀘스트, TTS, STT, Realtime
- Animator, 얼굴 표정, 립싱크
- DI 프레임워크, 전역 서비스 로케이터, 새 패키지

## 설계 결정

### 두 NPC를 동시에 배치

기존 Mina 프로필과 `MockNpcPrototype.unity`는 Phase 1 회귀 샘플로 보존한다. 새 `MultiCharacterMock.unity`에는 Luna와 Guard를 동시에 배치하고 각 NPC에 별도 입력 패널과 `NpcAIController` 인스턴스를 둔다. 프로필 전환 UI를 추가하지 않아 런타임 교체·취소 수명주기 복잡성을 만들지 않는다.

### 기존 Core 재사용

`AiNpcRequest`, `AiNpcResponse`, `IAiConversationClient`, `NpcAIController`, `INpcPresentationDriver`의 공개 계약은 변경하지 않는다. `MockConversationClient`는 이미 `DisplayName`, `ExampleDialogue`, `DefaultEmotion`을 사용하므로 같은 질문에 프로필별 응답을 만들 수 있다. `characterId`에 따른 조건문이나 캐릭터별 Controller 하위 클래스는 만들지 않는다.

`Personality`와 `SpeechStyle`은 필수 값으로 검증하고 요청 스냅샷에 유지한다. Mock에서 자연어 스타일 설명을 해석하는 규칙 엔진은 만들지 않으며, 실제 의미 반영은 Phase 4의 모델 클라이언트 책임으로 남긴다.

### 최소 프로필 검증

`CharacterProfile.TryValidate(out string error)`를 추가해 ID, 표시 이름, 성격, 말투, 예시 대사의 공백 여부와 감정 enum 유효성을 검사한다. `NpcConversationBehaviour`는 초기화 전에 이를 호출해 잘못된 프로필을 명확한 오류와 함께 거부한다. ID 중복은 전역 레지스트리 대신 Phase 2 샘플 생성기와 테스트에서만 검사한다.

## 샘플 프로필

| 필드 | Luna | Guard |
| --- | --- | --- |
| `characterId` | `sample-luna` | `sample-guard` |
| `displayName` | `Luna` | `Guard` |
| `personality` | Playful, curious, and friendly. | Disciplined, vigilant, and duty-bound. |
| `speechStyle` | Warm, casual, short sentences. | Formal, concise, respectful sentences. |
| `exampleDialogue` | 새로운 모험 이야기를 들려줄래? | 성문 주변에서는 질서를 지켜 주십시오. |
| `defaultEmotion` | `Happy` | `Concerned` |

검증 입력 `무엇을 좋아해?`에 대해 대사와 감정은 서로 달라야 하고, 같은 NPC에 반복하면 항상 같아야 한다. 질문 제스처는 기존 규칙대로 둘 다 `Nod`여도 된다.

## 예상 변경 파일

| 파일 | 계획된 변경 |
| --- | --- |
| `Runtime/Unity/Profiles/CharacterProfile.cs` | 최소 프로필 검증 메서드 추가 |
| `Runtime/Unity/Controllers/NpcConversationBehaviour.cs` | 초기화 시 프로필 검증 및 오류 보고 |
| `Editor/PrototypeSceneBuilder.cs` | 기존 Phase 1 경로를 보존하며 두 프로필·새 씬 생성 명령 추가; 필요한 생성 헬퍼만 매개변수화 |
| `Tests/EditMode/MockConversationClientTests.cs` | 서로 다른 프로필의 동일 입력 차별성 검증 |
| `Tests/EditMode/CharacterProfileTests.cs` | 유효·누락·공백 프로필 검증 추가 |
| `Tests/EditMode/MultiCharacterSceneConfigurationTests.cs` | 두 NPC, 고유 ID, 개별 UI·Presentation 참조 검증 |
| `Samples/MockNpc/Profiles/Luna.asset` | Editor API로 생성 |
| `Samples/MockNpc/Profiles/Guard.asset` | Editor API로 생성 |
| `Samples/MockNpc/Scenes/MultiCharacterMock.unity` | Editor API로 생성 |

Unity가 새 자산의 `.meta`를 생성하도록 하며 `.asset` 또는 `.unity` YAML을 직접 작성하지 않는다. `Packages/manifest.json`, Core 계약, 기존 Mina 자산과 Phase 1 씬은 변경 대상이 아니다.

## 데이터 흐름

각 UI는 자신의 NPC에만 연결된다.

`입력 패널 → NpcConversationBehaviour(프로필 스냅샷) → NpcAIController → MockConversationClient → AiNpcResponse → 해당 INpcPresentationDriver`

Luna와 Guard는 같은 클래스들을 사용하지만 Controller와 요청 상태는 인스턴스별로 독립적이다. 한 NPC가 요청 중이어도 다른 NPC의 버튼이나 출력은 영향을 받지 않아야 한다.

## 구현 순서

1. 현재 11개 테스트와 Phase 1 샘플을 회귀 기준으로 고정한다.
2. 프로필 유효성 및 프로필별 Mock 차이를 표현하는 실패 테스트를 먼저 추가한다.
3. `CharacterProfile` 검증과 `NpcConversationBehaviour`의 fail-fast 처리를 구현한다.
4. `PrototypeSceneBuilder`의 프로필·NPC·UI 생성 메서드만 필요한 범위에서 매개변수화하고 기존 Phase 1 테스트를 통과시킨다.
5. Luna·Guard 자산과 다중 NPC 씬을 Editor API로 생성한다.
6. 새 씬의 고유 ID, 컴포넌트, 프로필, 입력, 출력 연결 테스트를 추가한다.
7. Unity 컴파일, 전체 EditMode 테스트, 정적 의존성 검사를 수행한다.
8. Play Mode에서 두 NPC를 수동 검증하고 리뷰 후 Phase 2 커밋을 만든다.

## 테스트 전략

### EditMode

- 완전한 프로필은 유효하고 각 필수 문자열의 null·빈 값·공백은 거부된다.
- 동일 프로필과 입력은 대사·감정·제스처가 반복 실행마다 같다.
- Luna와 Guard의 동일 질문은 대사와 기본 감정이 다르다.
- 기존 Controller 성공·중복·실패·취소 테스트가 계속 통과한다.
- Phase 1 샘플 씬의 연결 테스트가 계속 통과한다.
- 새 씬에는 정확히 두 NPC가 있고 각자 올바른 프로필, Driver, 입력 필드, 버튼, 출력 필드를 참조한다.
- 두 샘플 프로필의 `characterId`는 비어 있지 않고 서로 다르다.

### Play Mode

1. 두 패널에 같은 질문을 보내 결과 차이를 확인한다.
2. 각 패널에서 같은 질문을 반복해 결정성을 확인한다.
3. Luna 요청 중 Guard 요청을 보내 상태와 버튼이 독립적인지 확인한다.
4. 각 패널의 빈 입력과 연속 클릭이 안전하게 처리되는지 확인한다.
5. 대사·감정 라벨과 NPC 색상·제스처가 해당 NPC에만 적용되는지 확인한다.

### 검증 환경

- Unity 버전은 `6000.5.3f1`을 유지한다.
- 검증 루트와 로그·결과는 `E:\CodexValidation`, 임시 경로와 `TEMP`·`TMP`는 `E:\CodexTemp`를 사용한다.
- Unity Test Runner가 추가 결과를 C 드라이브에 쓰는 방식은 사용하지 않는다. E 드라이브만 사용하는 실행법이 확보되지 않으면 batch 테스트를 실행하지 않고 제한 사항을 보고한다.
- 테스트 후 `Packages/`, `ProjectSettings/`, Runtime의 `UnityEditor` 참조와 금지 범위 키워드를 재검사한다.

## 완료 기준

- [x] Luna와 Guard가 같은 코드 경로에서 서로 다른 결정적 결과를 낸다.
- [x] `characterId` 기반 분기와 캐릭터별 코드 복제가 없다.
- [x] 잘못된 프로필은 요청 전에 명확히 거부된다.
- [x] 두 NPC의 입력·상태·표현 참조가 인스턴스별로 분리돼 있다.
- [x] 기존 11개 테스트를 포함한 전체 EditMode 테스트 20개가 통과한다.
- [ ] Play Mode 수동 체크리스트와 Console 오류 0을 확인한다.
- [x] 패키지 변경, 네트워크, 서버, 기억, 음성, Animator 구현이 없다.
- [ ] 변경 리뷰 후 Phase 2 체크포인트 커밋을 만든다.

## 위험 요소와 통제

- `PrototypeSceneBuilder`가 이미 크다. 새 프레임워크를 만들지 않고 실제 중복이 생기는 생성 메서드만 매개변수화한다.
- 두 패널은 해상도에 따라 겹칠 수 있다. 기준 해상도와 anchor를 다르게 두고 Play Mode에서 확인한다.
- 기존 자산을 재생성하며 사용자 값을 덮어쓸 수 있다. 생성기는 최초 생성 후에는 참조만 복구하고 프로필 내용은 보존한다.
- 자연어 `Personality`를 Mock 규칙으로 해석하면 취약한 키워드 시스템이 된다. Phase 2에서는 명시적 샘플 대사와 기본 감정만 동작 데이터로 사용한다.
- 전역 ID 검색은 패키지 경계를 불필요하게 넓힌다. 이번 단계에서는 샘플 쌍의 고유성만 자동 검증한다.
