# Phase 17 구현 검증

> 2026-10-12 · E4B·API 고정 비교 완료, 두 2단계 후보 모두 미채택. 기반 구현을 보존하고 Phase 17 전체 완료로 표시하지 않는다.
> 작업: `AI_NPC-phase17-implementation-20261012-a1`
> 기준: `Phase17`, `809eef852fb6015f31a52536ccfbb65e317d5063`

## 검증 원칙

[확정 계획](PHASE17_PLAN.md)의 모델별·평가 집합별 기준을 적용한다. 기존 96개와 신규 24개를 별도로 평가하며 개발 실행의 성공 결과로 최종 실행의 실패를 교체하지 않는다. 단일 호출은 비교 기준이며 기존 기본값을 유지한다.

사용자가 허용한 고정 합성 fixture만 원문 증거로 보존한다. 일반 Runtime·Backend 로그에는 대화, grounding, 내부 추론과 upstream 오류 원문을 기록하지 않는다. API는 새 장부로 실제 시도 최대 1,000회를 제한하며 이전 작업 장부는 변경하지 않는다.

## 완료한 구조 검사

| 검사 | 현재 결과 | 증거 |
| --- | --- | --- |
| Unity 라이선스 | entitlement 확인, 일반 사용자 실행 | `license-preflight.json` |
| 서버 TypeScript | `tsc --noEmit` 통과 | `source-audit.json` 및 서버 검증 실행 기록 |
| 서버 전체 회귀 | 143/143 | `server-tests-gated.json` |
| root Unity EditMode | 350/350, 실패·skip 0 | `root-edit-gated.xml` |
| Built-in/Legacy consumer EditMode | 350/350, 실패·skip 0 | `consumer-edit-gated.xml` |
| 실제 Unity fixture 검사 | 2,163개 조건 통과 | `selftest-final-results.json` |
| 단일 호출 입력 보존 | 120개 wire 및 SinglePass 직렬화 입력 불변 | `exported-inputs.json`, `exported-inputs-final.json` |
| API 격리 하네스 | fake 14개, 외부 호출 0 | `api-harness-selftest.json` |

현재 수정 소스의 복제본과 별도 `file:` package snapshot을 사용했다. 공개 package cache를 수정본 검증으로 계산하지 않았다. Unity는 라이선스 IPC 연결을 위해 샌드박스 밖의 일반 사용자 권한으로 실행했다. 관리자 실행, WSL 활성화나 라이선스 설정 변경은 하지 않았다.

## 개발 사례 평가와 후보 고정

각 행은 기존 96개를 2단계로 한 번 실행한 별도 개발 결과다. 신규 24개 출력은 이 과정에서 보지 않았다.

| 입력 후보 | 신호 완전 일치 | 행동 신호 오탐 | 상태·기억 원문 검토 | 전체 p95 |
| --- | ---: | ---: | ---: | ---: |
| 이전 후보 복원 | 93/96 | 0 | 17/19 | 2.368초 |
| 최신 관찰 블록을 마지막에 배치 | 93/96 | 0 | 19/19 | 2.517초 |
| 현재 참조 요청과 과거 발화 구분 명확화 | 94/96 | 0 | 17/19 | 2.400초 |
| 각 절의 신호와 취소 범위를 독립 판정 | 95/96 | 0 | 17/19 | 2.431초 |
| 위 보완의 조합 — 최종 후보 | 95/96 | 0 | 19/19 | 2.346초 |

기존 자동 판정은 올바른 영어 상태 답변을 실패로 표시하고, 상태 질문을 반복한 대사를 정답으로 표시하기도 했다. 자동 판정과 원문 검토를 분리했다. 조합 후보에도 복수 대상 중 문 열기를 선택하는 참조 요청 한 건의 신호 누락은 남았다.

기준 후보와 최신 블록 배치 후보의 실제 생성 384회에서 사전 tokenizer 수와 `prompt_tokens`가 모두 일치했다. 최신 블록 후보의 실제 렌더된 192개 상태 블록은 모두 최신 사용자 발화 뒤에 있었다. 특수 토큰을 임의 상수로 보정하지 않았다.

`final-candidate-freeze.json`과 `pre-evaluation-source.zip`에 후보와 핵심 연속 시나리오를 고정했다. 보류 평가 출력을 본 뒤에는 프롬프트를 수정하거나 실패를 재실행으로 교체하지 않았다. 평가용 격리본에서는 후보를 실행하고, 현재 제품 소스는 별도 capability/모델 채택 목록으로 미통과 후보를 차단한다.

## E4B 최종 비교 — 미채택

480턴을 모두 실행했고 생성 오류·timeout은 0건이었다. 아래 의미 점수는 고정 합성 응답의 원문 검토 결과이며 문자열 판정기의 점수와 구분한다. p95는 성공 표본만 골라 계산하지 않았다.

| 평가 집합 / 경로 | 신호 완전 일치 | 행동 신호 오탐 | 상태·기억 | 추론 요청 p95 / 최대 | 실제 생성 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 기존 96 × 2 / 단일 | 156/192 | 28 | 26/38 | 1.508 / 2.305초 | 192 |
| 기존 96 × 2 / 2단계 | 190/192 | 0 | 38/38 | 2.339 / 13.168초 | 384 |
| 신규 24 × 2 / 단일 | 40/48 | 4 | 20/24 | 1.613 / 1.647초 | 48 |
| 신규 24 × 2 / 2단계 | **44/48** | 0 | 22/24 | 2.315 / 2.330초 | 96 |

2단계는 기존 회귀 집합의 의미 기준과 추론 요청 p95 목표를 만족했으나, 신규 집합 신호 **46/48 이상** 기준에 미달했다. 합산 점수나 이전 결과로 이를 상쇄하지 않았다. `WindowsE4BTwoStage=false`로 유지하고 기존 단일 호출과 샘플 기본값을 보존한다. API도 아직 채택하지 않아 현재 제품에서 새 경로를 선택할 수 없다.

위 시간은 격리 하네스의 `runner.GenerateAsync` 호출부터 검증된 반환/오류까지이며 큐·템플릿·tokenizer·두 생성·응답 검증을 포함한다. 이후 Basic/Advanced 재생, 실제 게임 핸들러·화면 표시·기억 최종화는 포함하지 않는다. 따라서 전체 NPC 제출의 지연 통과나 Windows player에서 새 전략의 성능 통과로 확대하지 않는다. 채택 후보의 최종 환경 검사에서는 `LastSubmissionSeconds`로 전체 제출 시간을 별도 확인해야 한다. E4B는 의미 기준에 먼저 미달해 그 단계에 진입하지 않았다.

남은 오류는 다음과 같다. 각 사례는 두 회차 모두 같은 유형으로 실패했다.

- `heldout24_ref_two_targets` — 기존 96개에 포함된 참조 요청에서 문 열기 신호 누락.
- `phase17_reference_execute` — 새 참조 실행 요청의 문 열기 신호 누락.
- `phase17_compound_third_person` — 다른 사람을 함께 언급한 발화에서 NPC에 대한 모욕 신호 누락. 잘못된 변수 신호는 신규 2단계 2/48턴이다.
- `phase17_state_other_target` — 최신 문 상태가 열림인데 닫힘으로 답변. 신규 현재 상태 10/12, 개인 회상 12/12다. 기존 2단계는 각각 30/30, 8/8이다.

Basic/Advanced에 반환 ID를 재생한 규칙 계산 검사는 **480/480** 통과했다. 사용자 의도에 따른 실제 효과 일치는 신호 오류를 그대로 반영하므로 이것을 의미 정확도 100%로 해석하지 않는다. 이번 행동 오탐은 미리 지정한 인사 행동도 포함한다. 기존 문 열기만 센 기준으로 단일 호출 오탐은 기존 24턴·신규 0턴이므로 두 수치를 혼동하지 않는다.

개인 정보를 정확히 답하면서 확인형 문장을 쓰는 응답은 구체 값이 없는 질문 반복과 구별해 양쪽 경로에 같은 기준을 적용했다. 예를 들어 질문에 없는 목적지를 정확히 제시하는 회상과 단순 상태 질문 반복은 다르게 판정했다. 자세한 행별 판정은 `e4b-meaning-review.json`, 집계는 `e4b-final-summary.json`에 있다.

최종 720회 실제 생성에서 사전 tokenizer 수와 `prompt_tokens` 불일치는 **0건**이고, 2단계의 실제 렌더된 현재 상태 블록 480개는 모두 최신 사용자 발화 뒤에 있었다(`baseline-input-audit.json`). 별도의 실제 실행기 경계 검사에서는 생성 도중 취소, 같은 실행기의 다음 요청 복구, native `length` 종료 감지·거부, 소유 프로세스 종료를 통과했다(`e4b-boundary.json`, `e4b-boundary-results.json`). 경계 검사 복구 응답의 15.146초는 의도적 취소 직후 별도 측정이며 최종 비교 지연 표본과 섞지 않았다.

## API 최종 비교 — 미채택

사용자가 키를 보유한 터미널에서 시작한 8791 검증 서버로 480턴을 완료했다. 실제 upstream 시도는 **720/1,000회**, 자동 재시도·실패 결과 교체는 0회다. 기존 8787 서버와 과거 장부는 변경하지 않았다. 요청 모델과 응답 보고 모델은 모두 `gpt-5.6-luna`다. 같은 별칭이라는 사실만으로 이전 평가와 동일한 내부 모델 빌드라고 단정하지 않는다.

| 평가 집합 / 경로 | 신호 완전 일치 | 행동 신호 오탐 | 상태·기억 | HTTP 요청 p95 / 최대 | 실제 생성 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 기존 96 × 2 / 단일 | 192/192 | 0 | 33/38 | 2.497 / 8.942초 | 192 |
| 기존 96 × 2 / 2단계 | 178/192 | 0 | 38/38 | 3.452 / 10.424초 | 384 |
| 신규 24 × 2 / 단일 | 45/48 | 1 | 24/24 | 2.375 / 8.558초 | 48 |
| 신규 24 × 2 / 2단계 | 45/48 | 0 | 24/24 | 3.903 / 9.661초 | 96 |

2단계의 신호 일치는 기존 **178/192**로 기준 183/192, 신규 **45/48**로 기준 46/48에 미달했다. 상태·기억 개선이나 빠른 응답으로 신호 실패를 상쇄하지 않았다. `DefaultApiTwoStage=false`와 빈 서버 채택 목록을 유지한다. 기존 단일 호출도 이번 평가에서 오류가 있었으며, 비교 결과를 일반 대화 의미 정확도 보장으로 해석하지 않는다.

전체 오류·timeout은 0건이다. 상태·기억 124개 응답은 frozen fixture의 대상·현재 상태·사용자 정보·정정 이력과 원문으로 대조했다. 문자열 점수와 별도로 기록했고, 행별 근거와 응답 해시는 `api-meaning-decisions.json`·`api-meaning-review.json`에 있다. Basic/Advanced 반환 신호 재생은 480/480 통과했다. 이는 반환 신호에 따른 계산의 일치이며 사용자 의도 효과의 완전 일치를 뜻하지 않는다.

주요 2단계 오류는 정중한 문 열기와 참조 실행 요청을 `uncertain`으로 분류해 놓치는 경우, 인사에 칭찬을 추가하는 경우, 모욕·취소 복합 발화에서 모욕 대신 칭찬을 반환하는 경우였다. 실패 행의 첫 잘못된 값은 보존된 분류 배열에서 확인됐고 최종 ID 변환·규칙 계산은 그 결과를 따랐다. 실제 전송된 이력·현재 발화·트리거·최신 grounding과 분류→대사 연결을 별도 점검했다(`api-failure-traces.json`, `api-input-audit.json`). 이 비교만으로 특정 문구가 언어 해석 실패의 근본 원인이라고 확정하지는 않는다.

입력 감사 6,963개 조건을 통과했다. 두 단계의 이력이 일치하고, 현재 관찰은 대사 입력의 최신 사용자 메시지 뒤에 있으며, 모델 전송 전에 기록한 request hash가 실제 전송 payload와 일치했다. fixture 전용 세션 reset 480/480, 누적 장부 720회가 결과와 일치한다. 원시 usage 합계는 입력 606,327, 출력 26,528, 총 632,855 tokens다.

위 시간은 검증 HTTP 요청부터 V4 응답 수신까지이며 사전 fixture 주입·사후 reset·Unity 행동·화면 반영을 제외한다. 별도의 API 워밍업 호출은 하지 않았고 첫 요청과 느린 표본도 집계에 포함했다. 새 전략 전체 NPC 제출 시간은 측정하지 않았다. 모델 채택 조건 미달로 새 전략의 Builder·샘플과 root/consumer/player 통합 단계에는 진입하지 않았다. API 추가 실제 취소/복구와 예비 호출도 수행하지 않았으며, 쓰지 않은 예산을 채우기 위한 호출은 하지 않았다.

실행·판정 방법은 `API_REVIEW_METHOD.md`, 집계는 `api-final-summary.json`, 모델 전송 및 제한된 응답 원문은 `api-raw/`, 누적 횟수는 `api-attempts.jsonl`에 보존한다. 검증용 Unity 재생기의 `recall:<값>` 문자열 분기를 로컬과 맞춘 변경은 `api-replay-original.cs.txt`로 구분했으며 제품 추론 코드·프롬프트·기대 결과를 바꾸지 않았다.

## 현재 소스의 기존 단일 호출 회귀

| 환경 / 검사 | 결과 | 증거 |
| --- | --- | --- |
| root Advanced, Domain Reload 없이 두 Play 세션 | 각 50/50, 실제 모델 각 19턴, 소유 프로세스 종료 | `root-single-advanced-first.json`, `root-single-advanced.json` |
| root Local Grounded Play | 상태·행동 54/54, 개인 기억·격리·reset·프로세스 종료 통과 | `root-single-local.json` |
| root V4 fake Backend Play | 31/31, 실제 OpenAI 0회 | `root-fake-v4-user.json` |
| Built-in/Legacy Advanced Play, 렌더링 사용 | 각 50/50, 모델 각 19턴, 소유 프로세스 종료 | `consumer-graphics-advanced-first.json`, `consumer-graphics-advanced.json` |
| Built-in/Legacy Local Grounded Play, 렌더링 사용 | 54/54, 기억 유지·격리·reset·종료 통과 | `consumer-graphics-local.json` |
| 최종 Windows x64 Mono 빌드 | 성공, 105,760,448 bytes | `windows-mono-final-build-exit.json`, `build-result.txt` |
| 최종 Windows Advanced | 50/50, 모델 19턴·정리 통과 | `player-final-advanced.json` |
| 최종 Windows Local Grounded | 54/54, 씬 전환·기억 유지·격리·reset·종료 통과 | `player-final-local.json` |

최초 `-nographics` consumer 세션은 대화 제출 **전 모델 준비 실패**로 종료했고 실제 대화 생성은 0회였다. 별도 관찰에서 `local_timeout`을 확인했으며, 첫 프레임에 머무는 원인을 조사했다. `Application.runInBackground=true`만으로는 변화가 없었다. `EditorApplication.isPaused=true`를 직접 확인하고 재개하자 그래픽 장치가 없는 창 초기화 오류로 다시 일시정지했다. 관련 Unity Search 시작 예외·그래픽 오류와 각 실패는 그대로 보존했다.

렌더링을 켠 consumer 실행에서는 검증용 Editor 코드가 **확인된 Unity Search 시작 예외 한 종류만** 기록하고 그 일시정지를 한 번 재개했다. 다른 오류·사용자 전역 Error Pause 설정은 건드리지 않았다. 두 세션이 각 50/50을 통과했고, 시작 예외 1건·재개 1회·기타 오류 0건이다(`consumer-graphics-advanced-editor-pause.json`). 제품 소스나 모델 입력 수정 없이 검증 환경의 멈춤을 분리한 결과다. 최초 실패는 `consumer-single-advanced-first.json`, `consumer-preparation-diagnostic.json`, `consumer-background-diagnostic.json`, `consumer-pause-diagnostic.json`에 남는다.

fake Backend 첫 실행은 샌드박스 안의 검증 서버와 일반 사용자 Unity 사이 연결이 되지 않아 첫 HTTP 검사가 실패했다. 실제 API 시도는 없었고, 일반 사용자 환경에서 같은 fake 서버 연결을 확인한 뒤 31개 검사를 통과했다. 최초 실패는 `root-fake-v4-infrastructure-failure.json`·로그에 보존했다. 제품 코드·프롬프트를 바꿔 결과를 교체한 것이 아니다.

소스 범위 검사 41개는 버전·의존성 유지, 기존 YAML/GUID 미변경, 새 Unity 생성 metadata, 중복 안내 정합성, 변경 문서 링크와 Runtime의 Editor 비참조를 확인했다(`source-audit.json`). 작업 전 이미 존재한 계획·로드맵 변경은 보존했다. 현재 소스는 `current-source.zip`·manifest·working diff로 별도 식별하며, 모델 평가 당시 후보는 `pre-evaluation-source.zip`로 그대로 보존한다.

최종 빌드의 패키지 C#/asmdef 245개와 consumer package snapshot의 245개가 현재 저장소와 전부 일치한다(`final-compiled-package-source-check.json`). 조사용 관찰 클래스는 연결이 해제된 상태에서도 검증 복제본 Runtime에 남아 있어, Unity API로 GUID를 보존한 채 Editor 전용 폴더로 옮긴 후 최종 빌드·player 검사를 다시 수행했다. 제품 저장소에는 이 관찰 클래스를 추가하지 않았다. 앞선 중간 빌드의 성공 기록도 별도 이름으로 보존한다.

## 미완료 항목

- 채택 가능한 2단계 조합이 없다. NPC·Builder 전략 설정과 별도 비교 샘플은 적용하지 않았다.
- 새 전략의 root·consumer·Windows player 핵심 흐름, 전체 NPC 제출 지연, 실제 API 취소/복구 검사는 미수행이다. 위 Play/player 완료 기록은 기존 단일 호출 회귀다.
- 관측된 보류 평가로 후보를 다시 튜닝하거나 판정 기준을 낮추지 않았다. 추가 개선은 새 평가 설계를 포함해 별도로 결정한다.

고정 비교·검토는 끝났지만 Phase 17 전체 구현·지원 완료는 아니다. Android·LiteRT·E2B·LFM2.5 지원, 버전 변경, push·태그·릴리즈는 수행하지 않았다.

## 보존 위치

- [필수 증거](E:/CodexValidation/AI_NPC-phase17-implementation-20261012-a1/evidence)
- [납품 폴더](E:/CodexValidation/AI_NPC-phase17-implementation-20261012-a1/delivery)
- 임시 루트: `E:\CodexTemp\AI_NPC-phase17-implementation-20261012-a1`

사용이 끝난 이번 작업 소유 `candidate` 참조 추출본 **4,173,617 bytes(약 3.98 MiB)**를 파일별 해시·무사용 상태·링크 부재·절대경로 확인 후 삭제했다(`partial-cleanup.json`). 원본은 `candidate-reference-source.zip` **1,165,916 bytes**로 보존·해시 검증했으므로 이 항목의 논리적 순감소는 약 **2.87 MiB**다. 검증 프로젝트의 소스·설정·하네스는 `validation-project-sources.zip`으로도 보존했다.

API 고정 비교가 종료됐고 채택된 조합이 없어 새 전략 통합은 실행하지 않았다. 소스·설정·하네스 압축본의 해시와 경로·소유권·무사용 상태·링크 부재를 확인한 뒤, root/consumer 복제본·package snapshot·중복 하네스·검증 서버의 복사된 의존성·작업별 TEMP/UPM 캐시 **2,492,886,384 bytes(약 2.322 GiB, 파일 논리 크기)**를 정리했다. 17개 정리 대상 모두 제거했고 정리하지 못한 대상은 0개다. 작업별 TEMP 루트는 비어 있다. 정확한 내역은 `final-cleanup.json`에 있다.

재현용 API 서버 소스·lockfile과 미적용 연결 초안은 보존한다. `pre-evaluation-source.zip`, `pre-api-product-source.zip`, `current-source.zip`, `validation-project-sources.zip`로 모델 평가 당시와 최종 소스를 구분한다. 검증 프로젝트 복원과 오프라인 결과 재검사는 delivery 안내를 따른다. 8791 검증 서버만 종료했고 이번 호출 장부는 720회로 유지했다. 다른 서버는 종료하지 않았다. 종료 후 확인에서 8787 health는 연결되지 않았지만 해당 서버 설정을 변경하거나 원인을 추정하지 않았다. 이전 Phase 12·15·16 검증, AndroidSDK, 공유 모델·실행기와 다른 작업 파일은 정리하지 않았다. 최종 빌드·필수 검증 증거·미해결 의미 오류의 재현 자료는 보존한다.
