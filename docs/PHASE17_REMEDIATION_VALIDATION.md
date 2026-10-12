# Phase 17 후속 보완 검증 — 2026-10-12

> 승인된 보완 구현·검증 종료. **API `gpt-5.6-luna` 조합만 채택**, E4B는 미채택이다. SinglePass 기본값과 Phase 12 계산·상태 수명을 유지하며 Phase 17 전체 모델 완료로 묶지 않는다.
> 승인 범위는 [후속 보완 계획](PHASE17_REMEDIATION_PLAN.md)이며 [이전 후보 결과](PHASE17_VALIDATION.md)는 별도 기록으로 보존한다.

## 소스와 범위

- 시작 브랜치 `Phase17`, HEAD `809eef852fb6015f31a52536ccfbb65e317d5063`. 기존 미커밋 Phase 17 소스를 포함한 시작 스냅샷을 보존했다.
- Unity `6000.5.3f1`, 패키지 `0.4.0`, 모델·실행기·의존성을 유지했다. Unity entitlement 확인 후 E 드라이브 격리본을 사용했다.
- E4B는 기존 외부 manifest의 Gemma/llama.cpp Vulkan을 실제 실행했다. API는 `gpt-5.6-luna`, `reasoning.effort=none`, `store=false`, 출력 256 tokens, retry 0, 전체 30초를 사용했다.
- Android·E2B·LFM2.5·WSL·관리자 실행·push·태그·릴리즈는 수행하지 않았다.
- 모델 출력은 허용된 고정 합성 실험에만 보존했다. 키, 실제 사용자 대화, 내부 추론과 원시 upstream 오류는 수집하지 않았다.

## 고정과 사전 검사

기존 96개와 관측된 24개는 기대값·원문을 유지했다. 세 NPC 역할과 다른 대상·불투명 신호 ID를 사용하는 새 36개를 작성하고, 후보 출력 확인 전에 해시와 채점 항목을 고정했다. 새 상태·회상은 12개, 후보 두 회차의 통과선은 신호 69/72, 상태·회상 22/24, 대사 과업 65/72다.

새 fixture의 사전 계약 검사에서 임시 grounding revision 식별자를 발견했다. **새 평가 호출 전에** 기존 계약의 revision 계산으로 36개 식별자만 바로잡았다. 원래 고정 파일, 수정 내역과 새 해시를 모두 보존했으며 문장·이력·사실·기대 신호는 변경하지 않았다. 156개 V4 요청이 전체 본문 및 개별 필드 검사를 통과했다. 인용문 읽기와 번역 요청은 요청된 인용/번역 자체를 정당한 과업으로 채점하고, 무관하게 질문 전체를 복창하는 실패와 구분한다.

## 개발 실험 결과와 후보 선택

모델마다 16개 anchor에 대해 원본·한 요소 변경·원복을 각 3회, **144회 실제 단계 생성**을 수행했다. 분류 108회와 고정 분류 결과를 주입한 대사 36회이며 게임 행동은 실행하지 않았다. 재시도나 실패 행 교체는 없었다. 원본과 원복 payload의 동일성을 확인했다.

| 비교 | E4B | API |
| --- | --- | --- |
| 참조 실행 지침 변경 | 실행 참조·복수 대상 선택의 누락이 모두 유지됨 | 한 참조 사례가 원본 3/3 → 변경 0/3 → 원복 3/3으로 악화 |
| 절별 화자·대상·취소 범위 명시 | 제삼자와 NPC의 태도를 대비한 사례는 여전히 0/3 | 모욕＋취소가 0/3 → 2/3 → 0/3, 제삼자 대비는 3/3 유지 |
| 짧은 키 출력 | 조사한 두 anchor에서 기존 배열 대비 개선 없음 | 조사한 두 anchor에서 개선 없음 |
| 선언 순서 역전 | 칭찬＋요청이 3/3 → 0/3 → 3/3; 입력 순서 영향 재현 | 해당 사례 3/3 유지 |
| 사실 표시·프로토콜 우선순위 | 잘못된 상태 절은 바뀌었지만 “아니요, 출입문은 열려 있습니다”라는 모순이 남음 | 조사한 상태·회상은 원본부터 모두 정답 |

정적 대상 별칭 추가 실험은 두 anchor에서 API가 원본/원복 10/12, 변경 6/6이었다. 그러나 변경된 설명은 **563 bytes로 기존 512-byte 한도를 넘었으므로 제품 구성 후보에서 제외**했다. 이를 위해 한도를 늘리거나 작성 정보를 자동 삽입하지 않는다. 작은 반복 표본으로 일반적 인과 효과를 확정하지도 않는다.

**E4B는 유효한 완성 응답 개선 후보를 선택하지 못해 계획의 단계 B에서 종료했다.** 사실 절이 맞더라도 같은 답의 부정과 모순되면 성공으로 계산하지 않았다. 새 보류 평가와 제품 통합·player 호출을 하지 않는다. 모델의 선언 순서 민감성과 ID를 배열 위치대로 변환하는 코드의 정확성은 별개다.

**API는 `phase17-r1-api-clause-subject-scope`를 격리본에 구현했다.** 분류 지침의 절별 판단 문장 하나만 교체했고 persona/게임 권한 제외, 이력, schema, ID 변환, 두 번째 생성과 시간 예산은 그대로다. 실제 후보 입력이 개발 실험 payload와 일치함을 확인했다. 상태 입력, 키 schema, 별칭과 참조 지침은 채택하지 않았다. 후보 선택 파일은 두 모델의 결정을 모두 확정하고 동결 시점에 새 평가 출력을 아직 보지 않았음을 기록한다. 최종 평가 이후 후보를 다시 조정하지 않았다.

개발 실험은 어려운 사례와 의도적인 변형을 모은 것이므로 그 합계를 일반 정확도로 발표하지 않는다. 단계별 p95는 E4B 1.464초, API 2.050초이며 **전체 NPC 제출 p95가 아니다**. 두 모델 모두 개발 생성 오류는 0/144였다. E4B 초기 준비는 캐시 생성이 포함된 격리 환경에서 84.980초였으며 소유 llama 프로세스 종료를 확인했다.

## API 최종 평가와 채택

기존 120개 × 후보 2회와 새 36개 × 단일/후보 × 2회, 총 **384턴·696회 생성**을 수행했다. 출력 원문을 보고 판정한 상태·회상/과업 항목 206개를 `api-semantic-review.json`에 남겼다. 검토자는 Codex이며 독립적인 사람 평가를 수행했다고 주장하지 않는다. 오류·timeout·재시도는 0회다. 지침·fixture·기대값은 동결 이후 바꾸지 않았다.

| 집합 / 경로 | 정확한 신호 집합 | 현재 상태·회상 | 대사 과업 | 행동 신호 오탐 | 요청 p50 / p95 / 최대 | 생성 횟수 |
| --- | --- | --- | --- | ---: | --- | ---: |
| 기존 96 / 2단계 | **184/192** (기준 183) | **38/38** | 별도 기준 없음 | 0 | 2.791 / 4.055 / 7.773초 | 384 |
| 관측 24 / 2단계 | **47/48** (기준 46) | **24/24** | 별도 기준 없음 | 0 | 2.646 / 3.541 / 3.829초 | 96 |
| 새 36 / 2단계 | **70/72** (기준 69) | **24/24** | **66/72** (기준 65) | 0 | 2.993 / 3.662 / 3.957초 | 144 |
| 새 36 / 단일 | **72/72** | **24/24** | **70/72** | 0 | 1.738 / 2.296 / 4.080초 | 72 |

이 표의 시간은 생성 요청 경계다. 전체 NPC 제출 시간은 다음 실제 환경 표에서 따로 확인한다. 최대 지연을 제외하지 않았으며 p95는 nearest-rank 방식이다. API가 보고한 모델 식별자는 `gpt-5.6-luna`였고 고정 revision은 제공되지 않았다. 과거 별칭 호출과 내부 모델이 완전히 같다고 가정하지 않는다.

원래 후보의 기존 신호 178/192·관측 신호 45/48보다 이번 184/192·47/48이 높다. 그러나 이는 **서로 다른 시점의 비교**이며 지침 한 문장의 인과 효과 크기를 확정하는 실험은 아니다. 새 36개 동시 비교에서는 **단일 호출이 더 정확하고 빨랐다.** 따라서 선택형 조합의 절대 기준 통과를 보편적인 우위나 기본값 변경의 근거로 삼지 않는다.

입력 감사 6,336개가 통과했다. 동일한 전체 분류 payload 121종 중 7종에서 배열 변동이 있었다. 남은 신호 실패는 기존 회귀 8턴의 행동 `uncertain`, 관측 집합 1턴의 모욕→칭찬, 새 집합 2턴의 모욕＋취소 처리 오류다. 새 대사 과업 6건에는 제어판/스위치 절차 발명, 과거 거절과 현재 허가의 혼동, 실행 가능한 행동을 직접 할 수 없다고 말한 답변, 현재 상태만 답한 요청 누락이 포함된다. 각각의 실제 입력·배열·대사는 보존하며 성공으로 대체하지 않았다.

## 자동 회귀와 Basic/Advanced 검증

| 검사 | 결과 |
| --- | --- |
| 최종 저장소 Server / TypeScript 컴파일 | **147/147 / 통과** |
| root Unity EditMode | **362/362** |
| Built-in/Legacy consumer Unity EditMode | **362/362** |
| 156개 fixture × 16개 신호 부분집합의 Basic/Advanced 실행 | **2,496/2,496** |
| 최종 API 384개 응답의 실제 Basic/Advanced 재생 | 반환 신호 기준 **384/384**, 같은 문 효과 **384/384** |
| 위 재생의 사용자 의도 기준 관계·행동 효과 | **379/384** |
| 조사 API / 실제 세션 환경 API의 fake 검사 | **11개 / 6개**, 실제 호출 0회 |

**Basic과 Advanced가 정확히 계산했다는 것과 AI가 신호를 정확히 골랐다는 것은 다르다.** 같은 응답을 재생하면 두 경로는 해당 정책대로 움직였다. Advanced는 칭찬·모욕 신호 오류까지 호감도에 반영하므로 의도 기준 효과가 379/384로 낮아졌다. 이번 보완으로 Phase 12 변수 계산이나 행동 선택 알고리즘을 수정하지 않았다.

추가 Builder 회귀는 전략/개별 endpoint의 Scene·Prefab 적용·재적용, Undo, 기존 설정 불러오기, 새 타깃의 SinglePass 기본값, 미지원 모드의 무변경 거절과 Awake의 provider/handler 호출 0회를 검사했다. 기존 local host의 준비 0회, 취소·메모리·큐·V4 크기·실패 격리도 전체 검사에 포함했다. qualification 복제본에서만 임시로 API 지원 플래그를 켜 실제 환경을 검증했고, 통과 후 제품 플래그를 같은 값으로 확정했다. root 패키지와 consumer `package-snapshot`의 실제 C# 소스 해시는 최종 소스와 대조했다. 공개 패키지 캐시를 수정본 검사로 계산하지 않았다.

초기 격리 서버의 공유 V1/V2 fixture 경로 누락 8건은 하네스 경로를 복구하여 해결했다. 제품 코드를 바꿔 검사를 통과시키지 않았다.

## 실제 Unity·Windows Mono 환경

각 환경에서 고정 핵심 12턴을 두 번 실제 API로 실행했다. 생성된 샘플의 전송 프로필·트리거·정적 grounding이 동결 fixture와 일치함을 사전 확인했다. 세션은 fixture 이력 주입 없이 실제 서버 store와 lock, 성공한 대화의 누적 기억을 사용했다.

| 환경 | 핵심 턴 / 신호 일치 | 구조·효과 검사 | 전체 제출 p50 / p95 / 최대 |
| --- | --- | --- | --- |
| root Play | **24/24** | **103/103** (warm·cancel·recovery 포함) | 2.839 / **3.798** / 4.455초 |
| Built-in/Legacy Play | **24/24** | **92/92** | 2.813 / **3.906** / 5.138초 |
| Windows x64 Mono | **24/24** | **92/92** | 2.518 / **3.351** / 5.744초 |

측정 경계는 `NpcConversationBehaviour.SubmitAsync` 전체로 grounding, 전송, 대사 표시와 행동 처리를 포함한다. 핵심 성공 표본은 환경별 24개이며 실패/timeout 0회다. 의도적인 취소 1건은 성공 지연 표본에서 제외하되 별도 실패·복구 증거와 호출 장부에 남겼다. 일부 root 실행 시간에는 별도 consumer 빌드도 진행 중이었다. 격리된 하드웨어 성능 벤치마크나 실서비스 지연 보장이 아니다.

세 환경 모두 호감도 40→50→60, 인사 once, 허가 거절/성공, 최신 문 상태, 암호 회상, 두 NPC 격리, 최종 CanExecute 직전 허가 철회, 행동 reset과 기억 reset 분리를 통과했다. 상태·회상 핵심 질문 30/30과 72턴 대사를 직접 검토했다. 어느 답도 실제 실행 전에 완료됐다고 단정하지 않았다. 다만 6회의 최종 권한 철회에서는 모델의 “열겠습니다”라는 **미래 약속 뒤에 실제 행동이 거절**됐다. 새 Backend 결과 동기화 프로토콜은 없으므로 이를 완료 대사 정합성 보장으로 발표하지 않는다. 비교 화면의 실제 행동 결과가 권위 있는 값이다.

root에서 취소는 upstream 시작을 확인한 뒤 수행했다. 변수와 기억에 반영되지 않았고 서버 작업 종료 뒤 같은 NPC에서 다음 요청이 성공했다. 중복 제출·대화 reset·행동 상태 변경은 Busy로 거절됐다. root 반복 Play와 Domain Reload 비활성 두 번째 수명을 확인했다. Windows 두 프로세스는 각각 exit 0으로 종료했다.

consumer Editor의 `UnityEditor.Search.SearchDatabase` 예외가 예약된 다음 세션과 자동 종료를 방해했다. 첫 결과를 보존하고 **아직 호출하지 않은 두 번째 회차만** 새 Editor에서 이어갔다. 이후 별도의 API 호출 0회 수명 검사에서 Domain Reload를 끈 두 Play 진입·정상 종료를 검증했다. 강제 종료한 두 검증 Editor의 PID·사유·결과 해시도 보존했다. 이는 모델 실패 재시도나 제품 오류 수정이 아니다. 배치 실행의 `ScreenCapture`는 이미지를 생성하지 못했으므로 스크린샷/수동 시각 QA 통과를 주장하지 않는다.

## 반영한 파일과 사용법

- `server/src/two-stage-generator.ts`: 동결한 절별 화자·대상·취소 범위 분류 지침 한 문장. `two-stage-support.ts`: 검증 모델만 허용. SinglePass generator 내용은 이번 보완에서 변경하지 않았다.
- `Runtime/Unity/Controllers/NpcConversationBehaviour.cs`, `NpcInferenceAvailability.cs`, `Runtime/Unity/LocalInference/LocalInferenceRuntime.cs`: 전략 선택/미지원 진단, 별도 endpoint와 기존 생성자 호환성.
- `Editor/CharacterBuilder/CharacterBuilderModels.cs`, `CharacterBuilderService.cs`, `CharacterBuilderWindow.cs`, 새 `.Inference.cs`: 저장·불러오기·정적 검사와 선택 UI.
- `Samples~/AI NPC Prototypes/InferenceComparison/`: 별도 비교 샘플 생성기·패널·안내. 기존 Scene/YAML/GUID는 직접 편집하지 않았고 새 meta는 Unity가 생성했다.
- `Tests/Editor/Phase17RemediationContractTests.cs`, `CharacterBuilderBehaviorTests.Inference.cs`, `server/test/phase17-remediation.test.ts`, `two-stage-support.test.ts`: 계약·Builder·채택 범위 회귀.
- 로드맵, 저장소 지침, 저장소·패키지 안내, 중복 V4 계약과 Unreleased를 이번 상태로 정합화했다. 이전 Phase 12·15·16 완료 기록과 초기 Phase 17 실패 기록은 보존했다.

Character Builder에서 `BackendContext` + `ClassifyThenRespond`를 선택한다. 최신 서버를 기존 `npm.cmd run dev`로 실행하면 기본 8787의 `/v4/npc/respond`와 `/v4/npc/respond-two-stage`를 함께 제공한다. reset은 `/v4/npc/sessions/reset`을 공유한다. 실제 게임에 별도 검증 서버를 계속 둘 필요가 없다. 기존 실행 중인 8787 서버는 이번 작업에서 재시작하지 않았다. 사용자는 최신 소스를 쓰도록 해당 서버를 직접 재시작할 수 있다.

샘플을 import한 뒤 `Tools > AI Character Kit > Samples > Create API Inference Comparison`을 실행한다. Mira는 SinglePass, Toma는 2단계이며 기억·호감도는 독립이고 문은 공유한다. 같은 입력을 비교하려면 두 NPC의 기억·행동 상태와 공통 문 상태를 동일하게 복원한다. 추가 API 키를 Unity에 저장하지 않는다.

## 호출량·증거·정리

이번 API 장부는 **개발 144 + 최종 696 + 환경 151 = 991/1,000회**다. 환경 호출에는 핵심 144, warm 4, 취소 1, 복구 2가 포함된다. 남은 9회는 사용하지 않았다. 과거 720회 장부는 그대로다. E4B는 개발 144회로 종료했으며 미선정 후 본 평가·통합 호출을 하지 않았다.

보존 루트: `E:\CodexValidation\AI_NPC-phase17-remediation-20261012-a1`.

- `evidence/starting-source.zip`, `starting-source-manifest.json`: 미커밋 기존 변경을 포함한 시작점.
- `fixture-freeze.json`, `frozen-inputs*.json`, `input-freeze-amendment.json`, `candidate-freeze.json`: 입력·후보 동결과 생성 전 revision 수정.
- `development-*`, `e4b-development/`, `api-raw/`, `api-final-responses/`, `api-attempts.jsonl`: 허용된 고정 합성 원문, 실패와 호출 한도.
- `api-final-analysis.json`, `api-final-input-audit.json`, `api-semantic-review.json`, `api-behavior-replay.json`: 신호·의미·입력·실제 규칙 재생 근거.
- `api-environment/`, `environment-*-r*.json`, `environment-analysis.json`, `environment-semantic-review.json`, `consumer-*-audit.json`, `consumer-lifecycle-*`: 실제 세션·취소·수명·예외와 응답 의미 근거.
- `server-final-tests.json`, `*-integration-editmode.xml`, `*-build-result.json`, 주요 명령·로그: 실제 도구 실행 근거.
- 최종 소스/검증 하네스 식별정보·재현 압축본, `delivery/Comparison`의 일반 8787 비교 빌드와 `delivery/Windows`의 검증 빌드를 구분해 보존한다. 자세한 명령과 복원 조건은 `delivery/README.md`를 따른다.

정리 결과는 같은 evidence의 `cleanup-result.json`과 최종 보고에 기록한다. 필수 합성 원문·실패 재현 자료·최종 빌드는 보존하고 작업 소유의 사용 종료 복제본·캐시만 제거한다. 모델 원본·공유 도구·이전 작업·원래 저장소는 대상이 아니다.
