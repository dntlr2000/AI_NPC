# Phase 3 Implementation Plan

> 상태: 완료 — `cfc5b04 (Phase3)`
> 기준 커밋: `8708f4f (Phase2)`
> 목표: 실제 네트워크 없이 Unity와 미래 Backend 사이의 V1 JSON 계약을 고정한다.

## 구현 결과

- `AiCharacterKit.Transport` 순수 C# 어셈블리를 추가했다.
- V1 요청·캐릭터·응답·결과·오류 DTO를 Core 모델과 분리했다.
- schema version, request ID, status, 필수 값, enum token, 성공/오류 branch를 검증한다.
- 기존 `AiNpcRequest`·`AiNpcResponse`와 DTO의 양방향 mapper를 추가했다.
- Unity 경계에 내장 `JsonUtility` 기반 codec을 추가했으며 새 패키지는 없다.
- golden fixture와 실패 fixture를 포함한 EditMode 테스트를 추가했다.
- Unity 컴파일 성공, 전체 EditMode **46/46 통과**, 실패·건너뜀 0을 확인했다.

## 경계와 데이터 흐름

`Core domain ↔ AiNpcContractMapper ↔ V1 DTO ↔ AiNpcJsonCodec ↔ JSON`

- Core는 Transport, JSON, Unity를 참조하지 않는다.
- Transport는 Core만 참조하고 `noEngineReferences: true`를 유지한다.
- Unity assembly만 Transport와 `JsonUtility`를 함께 참조한다.
- 기존 `NpcConversationBehaviour → MockConversationClient` Play Mode 경로는 그대로다.

## 계약 결정

- `schemaVersion`은 정수 `1`, `requestId`는 호출자가 주입하는 비어 있지 않은 문자열이다.
- JSON 필드명은 camelCase, status와 enum token은 정확한 소문자만 허용한다.
- 성공 응답은 `result`, 오류 응답은 `error`만 활성화한다.
- 오류 code는 확장 가능한 lowercase snake_case 문자열이다.
- 같은 V1의 알 수 없는 추가 필드는 허용하고 필수 필드 누락은 거부한다.
- 상세 wire 예제와 호환성 규칙은 [`CONTRACT_V1.md`](CONTRACT_V1.md)에 고정한다.

## 테스트 범위

- Domain 요청·응답과 DTO의 양방향 값 보존
- 같은 DTO의 결정적 JSON 출력과 round-trip
- 정상 요청, 성공 응답, 오류 응답 golden fixture
- 필수 character 누락, 지원하지 않는 schema version
- 알 수 없는 emotion·gesture·status와 잘못된 error code
- 성공/오류 branch 동시 존재 또는 활성 branch 누락
- malformed·빈 JSON의 예외 없는 실패
- 같은 V1의 추가 필드 허용
- Phase 1·2의 기존 20개 회귀 테스트

## 완료 기준

- [x] Core 공개 모델과 `IAiConversationClient`를 변경하지 않았다.
- [x] Transport와 Core에 UnityEngine 또는 UnityEditor 의존성이 없다.
- [x] HTTP, Backend, OpenAI, API key, 기억, 음성을 추가하지 않았다.
- [x] Packages, ProjectSettings, 프로필, 씬, scene builder를 변경하지 않았다.
- [x] Unity 컴파일과 EditMode 46/46 통과를 확인했다.
- [x] 검증 로그·결과와 TEMP/TMP를 E 드라이브에만 기록했다.
- [x] 변경 리뷰 후 Phase 3 체크포인트 커밋을 만들었다 (`cfc5b04`).

## 남은 위험

- Unity 6 `JsonUtility`는 누락되거나 `null`인 중첩 객체를 빈 객체로 만들 수 있다. Codec은 최상위 필드가 누락 또는 명시적 `null`이고 비활성 branch의 모든 값이 기본값일 때만 정규화하며, 명시된 빈 객체나 값이 있는 잘못된 branch는 거부한다.
- 오류 DTO를 Controller 오류로 바꾸는 정책과 request ID 생성은 Phase 4 책임이다.
- V1은 JSON Schema 패키지 없이 fixture와 validator로 고정된다. 정식 다중 언어 Backend를 만들 때 동일 규격의 서버 테스트가 필요하다.
