# Phase 12 — 현재 소스 검증 기록

> 2026-10-03, 승인한 Advanced Behavior 범위 구현·자동 실행 검증 완료 / Unreleased
> 공개 v0.4.0 또는 이전 Phase 15·16 검증 결과와 별개다. 버전·의존성·태그는 변경하지 않았다.

## 검증 소스와 환경

- 작업 ID: `AI_NPC-phase12-20261003-a1`.
- 시작 commit: `c3248f2bf62119c134173201cfaf1169195b6b52`, 시작 작업 트리 clean.
- Unity `6000.5.3f1`, Node `v24.18.0`, Windows x64 Mono, RAM 32 GB / RTX 5070 Laptop 8 GB.
- 모델: 기존 Gemma 4 E4B IT Q4_K_M, llama.cpp b11146 Vulkan 및 기존 검증 템플릿. 모델·실행기 원본은 복사하거나 변경하지 않았다. 정확한 크기·SHA-256은 보존 manifest에 있다.
- 검증·납품 루트: `E:\CodexValidation\AI_NPC-phase12-20261003-a1`.
- TEMP/TMP 및 검증용 UPM/GI cache: `E:\CodexTemp\AI_NPC-phase12-20261003-a1`.
- 실제 Unity 라이선스 검사를 통과했다. 기본 sandbox의 UPM IPC 초기화가 실패하여 승인된 elevated batch 실행으로 검증했다.
- root는 Assets/Packages/ProjectSettings 복제본, Built-in/Legacy consumer는 이번 수정 소스의 별도 `file:` package snapshot을 사용했다. 공개 package cache를 수정본 검증으로 취급하지 않았다.

## 결과

| 검사 | 결과 | 주요 근거 (evidence 아래) |
| --- | --- | --- |
| Server TypeScript 및 전체 회귀 | 106/106 통과, OpenAI 호출 0 | `server-results.json`, `server-tests.log`, `server-compile.log` |
| root Unity 전체 EditMode | 303/303, 실패·skip 0 | `root-final-tests.xml`, `.log` |
| Built-in/Legacy 전체 EditMode | 303/303, 실패·skip 0 | `consumer-final-tests.xml`, `.log` |
| root Advanced 실제 모델, Domain Reload 비활성화 연속 2회 | 각각 50/50, 각 19건 실제 생성, 자식 프로세스 종료 | `root-advanced-final-first.json`, `root-advanced-final.json`, `.log` |
| consumer Advanced 실제 모델 | 50/50, 19건 실제 생성, 종료 확인 | `consumer-advanced-final.json`, `.log` |
| Windows x64 Mono 빌드 및 Advanced 실제 모델 | 빌드 성공, 50/50, 19건 실제 생성, 종료 확인 | `build-result.txt`, `windows-build.log`, `windows-advanced-final.json`, `.log` |
| 동일 샘플의 실제 V4 HTTP·session/reset | 31/31, 실제 서버의 fake generator 13회, OpenAI 0회 | `root-backend-v4.json`, `fake-backend-statistics.json` |
| 기존 Local Grounded NPC root·consumer·Windows | 각각 상태/행동 54/54 + 개인 기억·격리·reset·프로세스 종료 통과 | `root-local-regression.json`, `consumer-local-regression.json`, `windows-local-regression.json` |
| 기존 에셋·GUID 보호 | 기준 480개 파일 변경 0 | `protected-assets-check.json` |

초기 Unity 297개/301개/302개 검사는 중간 결과다. 위 303개가 최종 전체 검사이며, 마지막으로 공유 action 기록과 Advanced→Basic 전환의 grounding 연결 회귀를 포함한다. 실행 검증은 저장된 assembly가 아닌 현재 Runtime/Core/Behavior/Unity 및 sample 소스로 수행했다. 이후 Editor 저장/전환 보강은 최종 root·consumer 전체 검사에서 확인했고 Windows 빌드에도 포함됐다.

새 테스트는 typed 값·숫자 범위/소수·잘못된 정의/참조/깊이, 변경 전 조건과 변경 후 행동, 선언 순서·우선순위·동률, 공유 action cooldown/once, 실패/취소 확정 경계와 Busy, provider 실패 후 복구, NPC 격리·정의 복사·trace, V3/V4 fake client, Scene/Prefab 적용·재적용·Undo·기존 설정 읽기·Local Awake 정적 검사를 다룬다. 기존 Mock·V1~V4·음성·로컬 검사도 함께 실행했다.

실제 모델 검사는 40→50→60 호감도, 허용/거절, 반복 요청, provider 오류/복구, bool·once, 모욕 감소, 두 NPC 격리, 대화 reset의 상태 유지, 행동 reset의 기억 유지, 다음 요청 grounding, 생성 중 외부 상태 변경, 모순된 fake 응답의 실제 결과 최종화와 중복 실행 방지를 확인했다. 일반 기억 질문이 칭찬으로 잘못 반영되지 않는 검사도 포함한다.

## 구현 중 발견하고 보완한 사항

- 반복 문 열기 요청에서 모델이 trigger를 누락했다. 초기 `root-recovery-diagnostic.json`은 provider 복구 이후 `no_match`임을 증명했다. 샘플 규칙/프로필 설명에 현재 의도와 게임 권한을 분리하고 반복 요청도 분류하도록 명시했다.
- 일반 기억 질문을 칭찬으로 잘못 판정하는 사례가 있었다. `root-advanced-intent.json`을 보존하고, 샘플 신호를 현재 문장의 직접 칭찬/모욕/인사로 좁히며 제외 사례를 추가했다. 이후 위 네 번의 Advanced 실제 실행은 각각 50개 검사를 통과했다.
- Builder의 기존 Advanced 설정 읽기 위치, Basic 재전환 때의 자동 grounding bridge 연결, profile 저장/Undo 오류 처리를 보강하고 회귀 검사를 추가했다.
- 기존 Basic 생성자와 router의 action binding 요구는 유지한다. signal-only 트리거는 Advanced로만 평가하고 공통 executor의 최종 CanExecute를 재사용한다.

## 보존물과 재실행

`evidence`에는 시작 소스, 최종 소스/식별정보, 변경 파일 목록, Unity가 생성한 metadata 목록, 명령·버전·설정, 최종 XML/JSON/핵심 로그, 검증용 C# probe와 `launch.ps1`, 주요 수정 전 실패 근거를 보존한다. `source-audit.json`과 `source-identity.json`으로 빌드/runtime source 일치와 문서·기존 에셋 보호를 확인한다.

`delivery/Windows/AdvancedNpc.exe`와 `delivery/Run-AdvancedNpc.ps1`은 검증한 로컬 구성이다. `delivery/local-inference.json`이 기존 모델·Phase 16 외부 실행기를 참조한다. 자세한 실행은 `delivery/RUN.md`를 따른다. 기본 실행에서는 테스트 probe를 켜거나 대화를 로그로 저장하지 않는다.

저장소에는 Unity API로 생성한 **Mock 기본** `Assets/Samples/AI Character Kit/0.4.0/AI NPC Prototypes/AdvancedNpc/Scenes/AdvancedRelationshipNpc.unity`를 추가했다. 기존 Local Grounded 씬과 기존 YAML/GUID는 수정하지 않았다. 메뉴에서 새 샘플을 다시 생성할 수도 있다. package와 저장소의 [사용 안내](ADVANCED_BEHAVIOR_QUICKSTART.md)는 동일하다.

## 실제 제한

- 모델의 trigger 의도 판정과 일반 대사는 확률적이다. Unity 규칙은 반환된 유효 ID에 대해 결정적이며, 잘못된 의미 판정을 완전히 방지하는 기능은 아니다. 실제 행동 권한은 항상 consumer CanExecute가 결정한다.
- 이번 턴의 점수 변화는 다음 요청부터 모델 입력에 반영된다. V3에는 변수 grounding이 없고, Backend의 대사/기억은 LocalInference의 선택형 행동 결과 formatter와 같은 방식으로 재작성하지 않는다.
- 상태는 인스턴스 메모리만 사용한다. 저장/장기 관계 서비스, 다른 로컬 플랫폼·IL2CPP·다른 모델의 동작은 이번 검증 범위가 아니다. 기존 Phase 16 성능 측정값을 이번 기능의 성능 보증으로 재사용하지 않는다.
- 일부 batch Editor 실행에서 UnityEditor.Search의 시작 인덱싱 `ArgumentOutOfRangeException`이 기록됐다. stack은 Unity 검색 인덱서 내부이고 프로젝트 compile·assertion·종료 검사는 통과했다. Unity 버전을 변경하지 않았다.
- Windows batch 실행의 `ScreenCapture`는 지원되지 않아 대표 화면을 저장하지 못했다. 자동 Play/player 기능 검사는 통과했으며 수동 UI 조작·시각 검사를 수행했다고 주장하지 않는다.

## 임시 산출물 정리

이번 작업 소유 경로만 정리한다. 최종 산출물과 필수 증거, 기존 공유 모델/실행기 및 이전 작업 증거는 보존한다. 정확한 삭제 경로·확보 용량·남은 항목은 `evidence/cleanup-result.json` 및 `evidence/REPORT.md`에 기록한다.
