# AI NPC Framework 진행 점검 및 로드맵

> 기준일: 2026-08-27  
> 비교 기준: ChatGPT 대화 **“Unity Ai NPC 만들기”**의 초기 구상과 이후 합의된 Phase 1·2 범위

## 결론

저장소는 수정된 로드맵의 순서와 제약을 잘 따르고 있다. 현재 위치는 **Phase 1 기능 구현과 자동 검증은 완료했지만 마일스톤 종료 절차는 남은 상태**다. 실제 OpenAI 연동을 먼저 만들지 않고, Mock으로 요청·응답·표현 경계를 검증한 것은 초기 구상을 안전하게 구체화한 의도적인 변경이다.

다음 구현은 API가 아니라 **Phase 2: 다중 CharacterProfile 기반 재사용성 검증**이어야 한다. 단, 그 전에 Play Mode 수동 확인과 Git 체크포인트로 Phase 1을 닫는다.

## 현재 기준선

- Unity: `6000.5.3f1`
- 주요 설치 패키지: URP `17.5.0`, Input System `1.19.0`, uGUI `2.5.0`, Test Framework `1.7.0`
- 구현 위치: `Assets/AiCharacterKit/`
- 샘플: `Samples/MockNpc/Scenes/MockNpcPrototype.unity`
- Git: Phase 1 전체가 아직 추적되지 않은 신규 파일이며 `main`에는 초기 커밋만 존재
- 제외 범위: OpenAI, HTTP, `server/`, 기억, TTS, STT, Realtime은 없음

## 초기 로드맵과의 비교

초기 구상의 순서는 **텍스트 NPC → 실제 GPT/구조화 JSON → 감정·애니메이션 명령 → CharacterProfile → 기억 → 서버 정리 → 패키지 → 음성 → Character Builder → Realtime**이었다. 이후 대화에서 위험을 줄이기 위해 **오프라인 Mock → 다중 프로필 → 전송 계약 → 실제 모델 → 단기 기억 → TTS → STT/Realtime → 두 번째 프로젝트 → UPM** 순서로 정제됐다.

| 계획 축 | 현재 상태 | 판단 |
| --- | --- | --- |
| 텍스트 NPC vertical slice | 입력, 결정적 Mock 응답, 출력 UI와 샘플 NPC 구현 | 완료 |
| 구조화 응답 | `AiNpcResponse`가 대사·감정·제스처를 전달 | Phase 1 목표 충족; JSON 전송 규격은 Phase 3으로 보류 |
| 캐릭터 데이터 | `CharacterProfile`에 신원·성격·말투·예시·기본 감정 저장 | 기반 완료; 현재 프로필은 1개 |
| 표현 명령 | 색상으로 감정, 회전으로 제스처를 확인 | vertical slice 충족; Animator 연동은 의도적으로 미구현 |
| 실제 모델·백엔드 | 구현하지 않음 | 수정된 로드맵과 보안 원칙에 부합 |
| 기억·음성·패키지화 | 구현하지 않음 | 선행 구현을 피한 올바른 상태 |

초기 대화에서는 실제 GPT/JSON 응답이 비교적 앞에 있었으나, 이후 계획은 Mock → 프로필 재사용 → 전송 계약 → 백엔드 순서로 정리됐다. 현재 저장소는 이 수정된 순서를 따른다.

## 구현 및 검증 현황

- Core: 요청/응답 모델, `IAiConversationClient`, 결정적 `MockConversationClient`, 중복·취소·오류를 처리하는 `NpcAIController`
- Unity 경계: `CharacterProfile`, `NpcConversationBehaviour`, uGUI 입력, `INpcPresentationDriver` 구현
- 자동 설정: `PrototypeSceneBuilder`가 Editor API로 프로필과 샘플 씬을 생성·복구
- 의존성: Core asmdef는 `noEngineReferences: true`; Runtime에는 `UnityEditor` 참조가 없음
- 자동 검증: Unity EditMode **11/11 통과**, 실패·건너뜀 0. 결과는 로컬 `E:\CodexValidation\AI_NPC_RoadmapAudit\EditModeFinalResults.xml`에 보관
- 미검증: 이번 점검에서는 실제 Play Mode 상호작용을 사람이 확인하지 않음

## Phase 1 종료 게이트 — 지금 수행

1. `MockNpcPrototype.unity`를 열고 Console 오류가 없는지 확인한다.
2. `안녕`, `고마워`, `무엇을 좋아해?`를 입력해 대사·감정·제스처가 함께 바뀌는지 확인한다.
3. 빈 입력과 빠른 연속 클릭이 안전하게 거부되는지 확인한다.
4. 같은 입력을 반복해 동일한 구조화 응답이 나오는지 확인한다.
5. 변경 사항을 리뷰하고 `Packages/`가 그대로인지 확인한 뒤 Phase 1 커밋을 만든다.

**종료 조건:** Console 오류 0, EditMode 전체 통과, 위 Play Mode 항목 통과, 차단 수준 리뷰 이슈 0, Git 체크포인트 생성.

## 향후 작업 순서

### Phase 2 — 다중 캐릭터와 데이터 주도 재사용

- 성격과 말투가 대비되는 프로필 2개(예: Luna, Guard)를 만든다.
- 같은 입력이 같은 프로필에서는 항상 같고, 다른 프로필에서는 구분되는 응답을 내도록 Mock 규칙을 확장한다.
- `characterId` 분기 없이 프로필 데이터만 사용하며 필수 필드 검증을 추가한다.
- 동일한 Core와 Controller를 두 NPC 또는 프로필 선택 UI에서 재사용한다.
- 프로필별 결정성·차별성·유효성 테스트를 추가한다.

**종료 조건:** 두 캐릭터가 코드 복제 없이 구분되고 기존 11개 테스트를 포함한 전체 테스트가 통과한다.

### Phase 3 — Unity ↔ Backend 전송 계약

- Core 도메인 모델과 직렬화 DTO를 분리하고 버전이 있는 JSON 계약을 정의한다.
- 요청 ID, 캐릭터 스냅샷, 사용자 입력, 대사·감정·제스처, 오류 형식을 고정한다.
- 정상·누락·알 수 없는 enum·잘못된 JSON에 대한 golden fixture 테스트를 만든다.
- 이 단계에서는 실제 OpenAI 호출이 없어도 된다. `server/` 생성은 별도 구현 승인을 받은 뒤 진행한다.

### Phase 4 — Backend와 실제 OpenAI Structured Output

- API 키를 서버에만 두고 `IAiConversationClient`의 네트워크 어댑터를 추가한다.
- 스키마 검증, 취소, timeout, 재시도 제한, 오류 매핑, 민감정보 없는 로그를 구현한다.
- Mock 경로를 유지해 오프라인 개발과 회귀 테스트가 계속 가능하게 한다.

### Phase 5 — 세션과 단기 기억

- 먼저 최근 대화의 제한된 turn buffer와 명시적 reset을 구현한다.
- 컨텍스트 길이 제한, 캐릭터별 세션 분리, 저장 여부를 테스트한다.
- 장기 기억이나 Vector DB는 실제 요구와 평가 기준이 생길 때까지 보류한다.

### Phase 6 — TTS

- 텍스트 응답이 안정된 뒤 음성 출력 어댑터를 추가한다.
- 재생 취소, 새 응답으로의 교체, 텍스트 fallback을 먼저 검증한다.

### Phase 7 — STT 이후 Realtime

- push-to-talk STT를 먼저 만들고 인식 실패·취소 흐름을 검증한다.
- 지연시간과 대화 중 끼어들기 요구가 확인된 뒤에만 Realtime으로 확장한다.

### Phase 8 — 두 번째 Unity 프로젝트 재사용 검증

- 다른 프로젝트로 옮겨 경로·Input System·URP·샘플 자산 가정을 찾아낸다.
- 필요하면 2D 또는 다른 표현 드라이버를 추가하되 Core는 변경하지 않는다.

### Phase 9 — UPM 패키지화

- 두 프로젝트에서 검증된 최소 의존성을 기준으로 Runtime, Editor, Tests, Samples를 패키지 구조로 이동한다.
- 설치·제거·업그레이드와 샘플 import를 검증한다.

### Phase 10 — Character Builder 도구

- 프로필과 계약이 안정된 뒤에만 생성·검증용 Editor UI를 만든다.
- 편의 기능이 런타임 구조나 캐릭터 스키마를 결정하지 않게 한다.

## 주요 위험과 통제

- `Personality`와 `SpeechStyle`은 요청에 포함되지만 현재 Mock 규칙에서 직접 사용되지 않는다. Phase 2에서 ID 하드코딩 없이 의미 있게 반영한다.
- `NpcConversationBehaviour`가 Mock을 직접 생성한다. 실제 클라이언트 전환 시 작은 composition root를 도입하되 지금은 DI 프레임워크를 추가하지 않는다.
- 현재 표현은 정적 색상·회전이다. 대상 3D 캐릭터와 Animator 규격이 정해진 후 별도 `INpcPresentationDriver`로 확장한다.
- 전송 스키마, 인증, 비용 제한은 아직 없다. Phase 4 이전에 네트워크 코드를 넣지 않는다.
- 패키지화를 먼저 하면 잘못된 경계를 고정할 수 있다. 반드시 두 번째 프로젝트 검증 뒤 진행한다.

## 바로 다음 행동

**Phase 1 종료 게이트를 통과시키고 커밋한 다음, Phase 2만 구현한다.** Backend, OpenAI, 기억, 음성 작업은 각 선행 단계의 종료 조건이 충족되기 전에는 시작하지 않는다.
