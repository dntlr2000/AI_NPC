# Phase 17 — PC·API 선택형 2단계 추론과 신뢰성 안정화

> 기준일: 2026-10-12
> 상태: 후속 API 후보의 의미·환경 검증과 NPC/Builder 통합 완료, `gpt-5.6-luna` 조합만 채택. E4B는 미채택이므로 Phase 17 전체 완료로 묶지 않는다. [이전 후보](PHASE17_VALIDATION.md)와 [후속 검증](PHASE17_REMEDIATION_VALIDATION.md)을 구분하며 아래 승인 계획·기준은 유지한다.
> main 기준: `809eef852fb6015f31a52536ccfbb65e317d5063` (로드맵 갱신). 런타임 기준: `0ea2e4d3916e3da5f42076719d7560421021ca9b`
> 계획 점검 시 작업 브랜치: `Phase17` — 같은 기준 커밋이며 이번 작업은 브랜치를 변경하지 않았다.
> Unity `6000.5.3f1`, package `0.4.0`, 기존 의존성·모델·실행기·공개 태그 유지
> 확정 채택 기준: 행동 신호 오탐 0건, 신호 일치 95% 이상, 상태·기억 90% 이상, 응답 p95 5초 이내. 구조·호환성 검사는 모두 통과.

후보 탈락 이후의 원인 재검토와 다음 제안은 [Phase 17 후속 보완 계획](PHASE17_REMEDIATION_PLAN.md)에 분리했다. 아래 당시 승인 계획과 동결 평가 기록은 그대로 보존한다. 이후 승인된 후속 후보 구현·평가의 진행 상태는 [후속 검증 기록](PHASE17_REMEDIATION_VALIDATION.md)에 분리한다.

## 목표와 선택 이유

여러 Unity 프로젝트에서 재사용하는 PC·API 기반 기능 NPC의 대화 의도 판정과 현재 상태 답변을 개선한다. 기존 단일 호출 위에 같은 모델을 사용하는 선택형 **의도 분류 → 대사 생성** 경로를 추가한다. Phase 12의 결정적 변수 계산과 행동 선택, Phase 15 grounding, Phase 16 Windows 실행기와 기억을 재사용한다.

main 기반 격리 검증에서도 잘못된 행동 신호와 상태·기억 응답이 재현됐다. 반환 ID에 따른 Basic/Advanced 규칙 처리는 1,152/1,152건 기대값과 일치했다. 규칙 엔진을 다시 확장하기보다 추론과 요청 경계를 보완한다.

AndroidSDK 전체 병합 없이 필요한 변경만 main에 추가한다. 모바일 목표를 보류하면 LiteRT SDK, Java/JNI, 모델 가져오기와 플랫폼별 수명 관리의 유지보수 부담을 현재 PC 목표에서 분리할 수 있다. 기존 브랜치와 실패·성공 증거는 보존한다.

## 이전 평가와 이번 구현의 구분

동일한 작성 데이터·grounding·성공 이력을 가진 96개 고정 사례를 경로별로 두 번 실행한 main 기반 결과다. 통합 통과는 기대 신호, 의도한 규칙 효과, 해당 사례의 상태·기억 의미 검사를 함께 만족한 경우다.

| 구성 | 단일 호출 신호 / 통합 | 2단계 신호 / 통합 | 응답 p95 단일 / 2단계 |
| --- | --- | --- | --- |
| Gemma 4 E4B / llama.cpp b11146 Vulkan | 156/192 · 144/192 | 186/192 · 182/192 | 2.02초 / 3.43초 |
| API 기본 모델 gpt-5.6-luna | 192/192 · 187/192 | 190/192 · 190/192 | 1.95초 / 3.50초 |
| LFM2.5 1.2B Q8_0 / 검증 전용 어댑터 | 48/192 · 48/192 | 36/192 · 28/192 | 0.53초 / 0.72초 |

2단계는 아직 격리 후보에만 존재한다. main 기본 샘플의 작성 문구까지 그대로 시험한 결과가 아니며 일부 fixture의 문장이 반복된다. 일반 대화 정확도나 모델별 제품 지원 완료를 뜻하지 않는다. E4B 2단계의 최대 응답 18.30초도 제외하지 않았고, API 2단계에는 같은 입력에서도 정상 요청 누락이 발생했다.

AndroidSDK 계열 이전 평가의 E4B 2단계 통합 점수도 182/192였다. API는 이전 192/192에서 이번 190/192였고 LFM은 두 경로 모두 채택하기 어려웠다. 이번 수치만으로 브랜치 전체의 우열이나 과거와 같은 API 내부 모델 빌드를 단정하지 않는다.

S10·E2B에서는 실제 오프라인 대화·행동·설치·수명 관리를 확인했지만 기본 GPU p95 12.87초, 연속 검사 p95 17.45초로 목표를 충족하지 못했다. Windows LiteRT GPU도 실제 NPC 워밍업 실패가 남았다. 모바일·LiteRT 제품화는 보류하며 기존 Windows E4B Phase 16 완료 기록과 구분한다.

## 범위와 유지할 경계

- **주 대상:** Windows Editor·Windows x64 Mono의 E4B/llama.cpp, 기존 loopback Backend V4/API. API 모델 식별정보와 설정은 실행 때 기록한다.
- **선택:** NPC별 `SinglePass = 0` / `ClassifyThenRespond = 1`. 기존 자산과 샘플은 단일 호출을 유지한다. Basic/Advanced 행동 구성과 추론 전략은 서로 독립된 선택이다.
- **호환성:** 기존 공개 생성자, `IAiConversationClient`, `ILocalInferenceRunner`, `AiNpcResponse`, V1~V4 본문과 schema version을 유지한다. 미지원 모드·사용자 정의 실행기는 명시적인 설정 오류를 내고 자동 전환하지 않는다.
- **Backend:** 같은 V4 본문을 사용하는 별도 `/v4/npc/respond-two-stage` 설정을 제공한다. 기존 endpoint 값을 덮어쓰지 않고 기존 reset을 공유하며 새 경로에도 512 KiB 상한을 적용한다.
- **유지:** NPC별 기억과 행동 상태 수명, reset 분리, 변경 전/후 규칙 평가, 최대 한 행동, cooldown·once 성공 확정, 실행 직전 `CanExecute`와 로컬 행동 결과 최종화.
- **제외:** Android·iOS·LiteRT/E2B, LFM2.5 신규 제품 지원, 모델/실행기 교체, Phase 12 계산 알고리즘 확장, 장기 기억·RAG·스트리밍·음성 확장, Phase 13·14 구현, 자동 재시도·온라인 전환·확인 버튼 UI·공개 릴리즈.

LFM2.5 비교 자료는 유지하되 이번 구현의 지원 조건이나 추가 모델 설치 요구로 만들지 않는다. 기존 Gemma 설치 허용 범위도 임의로 넓히지 않는다.

## 공개 API와 파일별 설계

UPM 아래의 경로는 `Packages/com.aicharacterkit.framework/`를 기준으로 한다. 아래 새 이름은 구현할 경계를 지정하며 현재 제품 API로 존재한다는 뜻은 아니다.

| 영역 | 변경 파일과 책임 |
| --- | --- |
| 순수 전략·검증 | 새 `Runtime/Core/Models/NpcInferenceStrategy.cs`; `Runtime/LocalInference/LocalInferenceContracts.cs`와 새 `LocalInferenceIntentValidation.cs`. enum, capability, 순서별 분류 검증과 ID 변환을 담당한다. JSON은 포함하지 않는다. |
| NPC별 요청·기억 | `Runtime/LocalInference/LocalInferenceConversationClient.cs`. 전략을 요청에 전달하고 두 단계 전체가 성공한 응답만 기존 최종화·기억 확정 경로로 넘긴다. |
| 로컬 직렬화·실행 | `Runtime/Unity/Networking/LocalInferenceJsonCodec.cs`, 새 `.TwoStage.cs`·`.StageReader.cs`; `UnityWebRequestLocalInferenceRunner.cs`와 새 `.TwoStage.cs`; `Runtime/Transport/LocalInference/LocalInferenceDtos.cs`. strict JSON, 실제 토큰 계산과 하나의 큐 예약을 담당한다. |
| Unity 연결 | `Runtime/Unity/LocalInference/LocalInferenceRuntime.cs`, `Runtime/Unity/Controllers/NpcConversationBehaviour.cs`. NPC별 전략과 별도 V4 endpoint를 선택하고 공유 실행기를 유지한다. |
| 현재 턴 진단 | 새 `Runtime/LocalInference/LocalInferenceDiagnostics.cs`, `Runtime/Behavior/NpcBehaviorRuntime.cs`, `Runtime/Unity/Actions/NpcActionCoordinator*.cs`와 controller. 평가 알고리즘은 유지하고 이번 제출의 진단을 연결한다. |
| 작성·Builder | `Runtime/Behavior/NpcBehaviorModels.cs`, `Editor/CharacterBuilder/CharacterBuilderModels.cs`, `CharacterBuilderService.cs`, `CharacterBuilderWindow.cs`, `BehaviorAuthoringGUI.cs` 및 관련 partial. 전략·예시 편집, 검증·저장·불러오기를 연결한다. |
| Backend | 새 `server/src/two-stage-generator.ts`; `generator.ts`의 선택형 진단 타입·공통 오류 매핑, `app.ts`, `index.ts`. 기존 `sessions.ts`의 잠금·확정 계약을 재사용한다. |
| 검사·샘플 | 관련 EditMode/server 테스트, 공통 분류 golden fixture, 새 `Samples~/AI NPC Prototypes/InferenceComparisonNpc`의 Editor 생성기·비교 UI. 기존 샘플 자산은 자동 변환하지 않는다. |
| 문서 | 이 계획·로드맵·지침, 새 `docs/PHASE17_VALIDATION.md`, 저장소/패키지 사용 안내·V4 안내·Unreleased. 검증 문서는 실제 결과가 나온 뒤 생성한다. |

### 전략 선택과 호환 생성자

- `NpcInferenceStrategy.SinglePass = 0`, `ClassifyThenRespond = 1`을 추가한다. 새 enum은 Core에 두고 기존 `NpcConversationMode` 값은 바꾸지 않는다.
- `NpcConversationBehaviour.inferenceStrategy`와 `contextTwoStageBackendEndpoint`를 추가한다. 새 endpoint 기본값은 `http://127.0.0.1:8787/v4/npc/respond-two-stage`다. 기존 `contextBackendEndpoint`와 `contextResetEndpoint` 값은 보존한다.
- `LocalInferenceRequest`는 전략이 포함된 오버로드를 추가하고 기존 2인자 생성자는 SinglePass로 위임한다. mutable V4 DTO와 이력은 요청 생성 시 깊은 복사하여 제출 이후 값 변경이 두 단계에 영향을 주지 않게 한다.
- `LocalInferenceConversationClient`와 `LocalInferenceRuntime.CreateClient`는 전략을 받는 오버로드를 추가한다. 기존 생성자·메서드 서명은 그대로 남긴다. 전략은 클라이언트 생성 시 고정하며 실행 중 hot switching은 추가하지 않는다.
- 선택형 `ILocalInferenceStrategyCapabilities.SupportsStrategy(...)`를 제공한다. 기존 사용자 정의 runner는 SinglePass에서 계속 작동한다. capability가 없거나 2단계를 지원하지 않는 runner의 2단계 요청은 네트워크·행동·기억 변경 전에 `unsupported_inference_strategy`, `retryable=false`로 거부한다.
- `IAiConversationClient`, `ILocalInferenceRunner`, `AiNpcResponse`, V1~V4 본문은 확장하지 않는다. 새로운 분류 정보·진단 원문을 wire DTO에 넣지 않는다.

### Backend 세션과 오류 경계

`index.ts`가 `InMemoryConversationSessionStore` 하나를 만들고 단일/2단계 generator에 각각 연결한 두 `SessionConversationService`가 이를 공유한다. 새 route만 새 service를 사용한다. 기존 reset과 두 route가 같은 sessionId의 잠금을 공유하여 동시 생성·reset을 거부하고 완성된 대화 쌍을 한 번만 저장한다.

`AppDependencies`에 선택형 `twoStageSessionService`를 추가하여 기존 fake/custom app 구성을 보존한다. 새 service를 주입하지 않은 구성의 새 route는 고정된 설정 오류를 반환하고 단일 generator로 대체하지 않는다. 기존 단일 `buildNpcInstructions`·schema·출력 옵션은 변경하지 않는다.

새 generator는 기존 모델 설정, `reasoning.effort=none`, `store=false`, 단계별 출력 256 tokens, `maxRetries=0`을 사용한다. 요청 취소와 전체 30초 deadline을 두 호출이 공유한다. 상태가 completed가 아니거나 잘림·거절·tool output·추론 채널·잘못된 필드가 있으면 기존 V4 안전 오류로 변환한다. 새 route의 HTTP 413도 generator를 호출하지 않는 V4 오류 형식으로 반환한다.

현재 Backend는 Unity의 최종 행동 결과를 수신하지 않는다. 같은 프로토콜을 유지하고 대사에서 실행 성공을 미리 단정하지 않도록 한다. 로컬의 실제 행동 결과 대사 최종화와 Backend의 보장 범위를 문서·샘플에서 구분한다.

## 한 턴의 처리

1. 프로필·트리거·최신 grounding과 성공 대화 이력을 불변 snapshot으로 잡고 기존 요청 검증을 수행한다.
2. NPC 식별정보·트리거·같은 성공 이력·현재 발화로 의도를 분류한다. 캐릭터 연기 지침과 현재 행동 권한은 분류 입력에서 분리한다. 이력은 현재 참조 지시의 대상을 찾는 데 사용하며 과거 발화를 다시 실행하지 않는다.
3. 선언 순서에 대응하는 `{"decisions":["match","no_match","uncertain"]}` 형태를 제한된 JSON으로 받는다. 실제 배열 길이는 등록 트리거 수와 같아야 한다. 개수·값·추가/중복 키·잘림을 검사하고 `match`만 등록 ID로 변환한다. 이유나 내부 추론은 요구하지 않는다.
4. 같은 보존 이력, 캐릭터, 최신 grounding과 검증된 분류 결과로 대사·감정·제스처만 생성한다. 신호 필드가 섞이면 거부한다. `uncertain`인 신호만 효과를 보류하고 명확히 match된 다른 신호는 유지한다. 해석하지 못한 참조는 구체적인 요청을 묻도록 한다.
5. 두 단계가 모두 성공한 뒤 기존 Basic/Advanced 처리를 한 번 실행한다. 로컬 최종화가 적용된 대사만 기억과 화면에 반영한다. Backend에 새로운 게임 효과 동기화 프로토콜은 추가하지 않는다.

트리거가 없으면 분류를 생략한다. 로컬 두 단계는 FIFO 슬롯 하나와 생성 제한 35초를 공유하고 Backend도 전체 upstream 30초 예산을 공유한다. 단계별 출력 상한은 256 tokens다. 실패·취소된 생성은 기억·변수·행동에 반영하지 않으며 행동 처리에 들어간 뒤에는 기존 확정 규칙을 따른다. 최대 두 번 생성하고 재요청하지 않는다.

로컬의 각 단계는 실제 템플릿과 tokenizer로 입력을 계산한다. 오래된 성공 이력부터 제외하되 두 단계의 참조 이력은 같게 유지한다. 현재 입력 자체가 한도를 넘거나 분류 후 실제 대사 입력이 넘치면 실패시킨다. 성공한 분류 이후 근거 이력을 추가 삭제하거나 현재 데이터를 잘라내지 않는다. Backend는 기존 요청·기억 제한과 두 단계의 동일 이력을 유지하며 API가 보고한 실제 사용량을 별도로 기록한다.

### 로컬 입력 한도와 취소

단일/2단계가 공통 `CountPromptTokensAsync`를 사용하도록 토큰 계산을 추출한다. `/apply-template` 출력과 실제 생성 usage를 고정 모델에서 대조하고 필요한 특수 토큰 옵션을 명시한다. 현재 고정 E4B 템플릿은 BOS가 없는 출력에 `add_special:true`가 필요했으며, 기본값에 의존하거나 `+1`을 덧붙이지 않는다. 실제 입력 3,840 + 출력 예약 256 = 4,096은 허용하고 입력 3,841은 생성 전에 거부한다. 한글·따옴표·역슬래시·줄바꿈·emoji와 이력 축소에서도 검증한다.

2단계 preflight는 공통 이력 suffix를 골라 분류 입력과 가정 결과의 대사 입력을 모두 검사한다. 분류 후 실제 decisions로 대사 입력을 다시 계산하며 초과 시 두 번째 생성을 호출하지 않는다. 입력을 맞추기 위해 현재 grounding·트리거를 삭제하거나 분류 근거 이력을 바꾸지 않는다.

큐 대기 최대 8건·30초, 실행 슬롯 하나, 슬롯 획득 후 두 단계 전체 35초, 4,096 context·출력 256·temperature 0.2·seed 42를 유지한다. 취소된 응답과 늦은 완료는 무시하고 진행 중 HTTP 종료를 확인한 뒤 슬롯을 반납한다. 한 NPC의 취소로 공유 프로세스를 종료하지 않는다. 실제 생성 중 취소 후 같은 runner에서 다음 요청 복구와 중복 행동·기억 반영 0회를 검증한다.

### 후보 입력 보완의 한계와 순서

보존 후보의 구조를 먼저 옮기고 단일 호출의 직렬화 결과를 기준 소스와 대조한다. 2단계에는 조사에서 근거가 있었던 다음 후보를 각각 하나씩 시험한다.

1. 최신 관찰 블록이 실제 렌더된 템플릿에서도 과거 이력 뒤에 오도록 배치를 조정한다. 메시지 배열에서 마지막 system이라는 사실만으로 위치가 유지된다고 가정하지 않는다.
2. 과거 발화를 새 지시로 취급하지 않는 규칙과 현재의 명시적 재실행 요청을 구분하는 문장을 명확히 한다. 과거 발화 배제 지침을 무조건 제거하지 않는다.
3. 한 발화의 칭찬·모욕·행동 취소를 트리거별로 독립 판정하도록 안내한다. 취소 한 개가 다른 명확한 신호까지 억제하지 않게 한다.

개발용 96개에서 단일 요소별 효과를 확인한 뒤 조합 후보를 하나 고정한다. 새 24개 보류 평가 사례를 후보별 튜닝에 반복 사용하거나 평가 문장을 프롬프트 예시로 옮기지 않는다. API는 별도의 표현 튜닝 반복 없이 고정한 후보를 평가한다. 미통과 시 실패를 기록하고 이번 예산 안에서 새 후보로 결과를 교체하지 않는다.

## 함께 보완할 항목

| 항목 | 반영할 내용 |
| --- | --- |
| 사전 토큰 계산 | main E4B에서 실제 생성보다 1 token 적었던 특수 토큰 처리를 실제 chat 경로와 맞춘다. 임의 상수 보정이나 문자 수 추정으로 대체하지 않는다. |
| 현재 턴 진단 | 반환 신호, 평가 여부, 고정 실패 코드, 행동 결과와 전후 변수 값을 이번 제출에 연결한다. 생성 실패·Busy·취소를 이전 trace와 혼동하지 않는다. |
| 작성 데이터 검증 | 필요한 일치/제외 예시 편집과 중복·범위 검사만 선별한다. 현재 main 설명·Mock 예시를 자동 변환하지 않으며 전송되는 문구를 미리 확인할 수 있게 한다. |
| 의미 평가 | 질문 반복, 현재/과거, 대상과 표현 차이를 구분해 판정한다. 기존 문자열 판정과 원문 검토를 따로 남기고 일반 의미 판정기로 완성됐다고 표현하지 않는다. |
| 개인정보 없는 측정 | 단계별 시간·실제 호출 수·토큰 수·실패 단계만 진단한다. 일반 앱 로그에 대화·grounding·모델 내부 추론을 남기지 않는다. |

AndroidSDK의 단일 호출 프롬프트·schema 변경을 묶어서 가져오지 않는다. 현재 턴 진단과 설정 검증은 main 호출부와의 호환성을 확인해 선별한다. 모델별 의미 개선 후보는 고정 비교에서 확인한 뒤 별도로 채택한다.

진단에는 requestId·NPC 제출 번호·전략·단계·실제 호출 수·시간·토큰 수·고정 실패 코드만 담는다. 로컬의 선택형 `ILocalInferenceDiagnostics`는 완료된 불변 snapshot을 이벤트로 전달하고 client가 자신의 요청에만 연결한다. 공유 runner의 전역 `LastTurnDiagnostics`를 나중에 읽어 다른 NPC 결과를 가져오는 방식을 사용하지 않는다. 구독은 client 수명으로 해제하고 진단 구독자 예외가 대화나 슬롯 해제를 방해하지 않게 한다. 기본 UI는 NPC별 최근 제출 한 건만 보유하며 명시적 trace의 최근 32턴 한도는 유지한다.

AndroidSDK의 `LastEvaluationCode`·현재 턴 outcome 처리만 선별 이식한다. 새 제출은 pending으로 시작하고 생성 실패·provider 오류·취소·실제 평가 완료를 구분한다. Busy로 거절된 중복 제출은 진행 중 턴의 결과를 덮어쓰지 않는다. 행동 상태 reset은 관련 outcome/trace를 지우고 대화 기억 reset은 행동 상태를 유지한다. Basic에서도 생성 실패 뒤 이전 행동을 이번 결과로 표시하지 않는지 검증한다.

Backend의 단계별 진단은 requestId로 연결한 서버의 제한된 구조화 telemetry에 기록한다. Unity 비교 UI는 API 왕복 시간·반환 신호·실제 효과를 표시하며 V4 본문에 단계별 측정 필드를 추가하지 않는다. 제공되지 않는 API 단계 측정을 0으로 표시하지 않는다.

작성 기능은 Advanced 신호의 선택형 positive/negative examples만 선별한다. 각 목록 최대 4개, 각 예시 UTF-8 128 bytes, 제어 문자·공백만 있는 값·양쪽 목록의 동일 예시를 거부한다. 비교 시 바깥 공백만 제거하고 원문은 보존한다. 설명과 label·예시를 합친 **최종 설명 전체 512 bytes**를 유지한다. 빈/null 목록은 기존 설명과 byte 동일하며 Mock의 `exampleUserText`는 자동 변환하지 않는다. Basic에는 기존 조건 설명 필드를 사용하고 비교 시 Advanced가 만든 최종 설명을 똑같이 입력하여 분류의 조건을 맞춘다.

## Builder와 샘플

NPC와 Character Builder에서 추론 전략을 선택하고 생성·불러오기·Scene/Prefab 적용·재적용·Undo를 지원한다. LocalInference와 BackendContext/V4 외 조합은 정적 오류로 안내한다. Builder·Awake 검사는 provider·행동 handler·모델 준비를 호출하지 않는다.

Editor API로 별도 PC 비교 샘플을 만든다. 같은 작성 데이터로 단일/2단계, 현재 신호, 변수 변화, 실제 행동 결과와 응답 시간을 확인한다. NPC별 기억과 호감도, 대화/행동 reset은 분리한다. 기존 Basic/Advanced 및 Local Grounded 샘플의 기본 설정은 유지한다. 비교에 쓴 작성 데이터를 재현 가능한 consumer 자산으로 정리하며 기존 YAML/GUID는 직접 변경하지 않는다.

샘플은 두 NPC를 한 쌍으로 생성해 왼쪽 SinglePass, 오른쪽 ClassifyThenRespond로 고정한다. 두 NPC는 실행기 하나를 공유하되 기억·호감도·문/경보 상태는 서로 독립적으로 두고 같은 초기값으로 시작한다. 비교 버튼은 같은 합성 발화를 두 NPC에 **순차 제출**하여 큐 경합을 속도 차이로 오해하지 않게 한다. 공유 문을 첫 NPC가 변경하여 두 번째의 입력 조건이 달라지는 비교는 사용하지 않는다. 공유 실행기 취소·FIFO 검사는 별도 시나리오로 수행한다.

샘플 생성 자체와 Builder 검사는 네트워크·모델 준비를 수행하지 않는다. 실행 시 사용자가 E4B 또는 API를 명시적으로 선택하며 로컬 모델 준비도 명시적으로 시작한다. 기존 앱의 endpoint나 프로필을 덮어쓰지 않는다. 새 샘플은 구현 이후 Unity API로 생성한다.

## 작업 순서와 검증

| 순서 | 구현 단위 | 해당 단계의 확인 조건 |
| --- | --- | --- |
| 17-1 | 기준·fixture·판정기 고정 | 현재 main 소스, 단일 호출 입력, 기존 96개, 새 24개·기대 ID·효과·의미 채점 규칙과 해시를 보존한다. |
| 17-2 | 토큰 계산·현재 턴 진단·예시 검증 | 실제 토큰 경계, 빈 예시의 입력 불변성, Busy/실패/취소·NPC 진단 격리가 통과한다. |
| 17-3 | 순수 분류 검증·로컬 2단계 | 두 단계 전체 1슬롯·1예산, strict 결과 검사, 실패 시 무변경, 기존 생성자 호환을 fake로 통과한다. |
| 17-4 | Backend 2단계 route와 shared store | 512KiB 경계, route 간 Busy/reset, 실패 시 기억 미확정, 최대 2회 호출을 fake로 통과한다. |
| 17-5 | 입력 후보 확정·실제 비교 | E4B에서 개발 후보를 정한 뒤 보류 24개를 공개 평가한다. 같은 최종 후보를 API에서 검사하고 모델별 채택 여부를 기록한다. |
| 17-6 | NPC·Builder·비교 샘플 | 통과한 조합의 설정·불러오기·적용·재적용·Undo와 정적 호출 0회를 확인한다. 미통과 조합을 지원 완료로 표시하지 않는다. |
| 17-7 | 현재 소스 배포 환경 검증 | root·Built-in/Legacy consumer·Windows Mono의 핵심 흐름을 각각 2회, 기존 단일 호출·Basic·Advanced 회귀와 종료 정리를 통과한다. |
| 17-8 | 결과·안내·보존·정리 | 코드·문서·소스 해시를 맞추고 필수 결과와 재실행 자료를 보존한 뒤 소유 캐시를 정리한다. |

자동 회귀의 제품 기준은 main의 Server 106개·Unity EditMode 303개다. 이전 격리본의 Server 137/137·Unity 318/318은 후보와 관찰기 검사를 포함한 별도 증거이며 main의 현재 기능 수로 세지 않는다. 구현 시 전체 회귀에 분류 형식·대사 신호 주입 거절, 실패/취소 후 무변경, 최대 호출 수, 시간 예산·FIFO·Busy, 토큰 경계·동일 이력, 기억 격리/reset, 새 V4 경로 상한·세션 잠금, Builder 정적 호출 0회 검사를 추가한다.

추가 자동 검사는 다음 경계를 분리한다.

- 분류 배열 누락·잘못된 enum·길이·추가/중복 키·잘림, 대사 단계의 신호 주입, 알 수 없는 감정/제스처.
- 분류 성공 뒤 대사 실패/취소, 실제 대사 토큰 초과, 0개 트리거의 1회 생성, 재시도 0회와 행동 처리 1회.
- 35초 공유 예산, FIFO 8건·대기 30초, 한 NPC 취소와 다른 NPC 생존, 새 요청을 덮어쓰는 늦은 진단·결과 방지.
- 524,288-byte V4 요청 허용/1-byte 초과 거절, Unity 전송 전 거절과 server 413 시 generator 0회. V1~V3·reset·TTS/STT 한도 보존.
- V4 단일/2단계 동시 요청과 reset이 같은 sessionId에서 Busy, 실패 후 다음 제출 복구, 중간 분류·grounding의 기억 미저장.
- 기존 공개 생성자, SinglePass 직렬화 입력/schema, custom runner의 기본 호환과 미지원 2단계 오류.
- Builder Scene/Prefab 재적용·Undo·저장 실패 시 기존 자산/GUID 보존, 모델 준비·provider·handler 호출 모두 0회.

실행 검증은 라이선스를 먼저 확인하고 E:의 수정 소스 복제본에서 수행한다. root·consumer·Windows Mono에서 모델별 핵심 연속 시나리오를 각각 두 번 확인한다. 회차당 실제 모델 제출을 12턴으로 고정하며 기억을 만드는 선행 발화도 이 수에 포함한다. 인사·칭찬 누적·거절/허용·금지·참조 지시·현재 상태·개인 회상·두 NPC 격리·대화/행동 reset 분리·실행 직전 상태 변경·종료를 포함하며 실제 실행 목록은 17-1에서 고정한다. Domain Reload 비활성화·반복 Play, Scene 전환·준비/중지·취소 후 복구, 로컬 행동 결과 대사와 기억도 확인한다. 추가 실제 API 생성이 필요한 수명·취소 검사는 아래 예비 136회 안에서 수행한다. 기존 Local Grounded·Advanced 샘플과 fake V4 HTTP 회귀를 함께 실행한다.

### 고정 비교와 채점

- **기존 회귀:** 96개 × 단일/2단계 × 2회 = 모델당 384턴. 작성 데이터·이력·최신 상태를 같게 유지한다.
- **새 보류 평가:** 참조 지시·취소 6개, 복합 칭찬/모욕/행동 6개, 최신 상태 6개, 개인 회상 6개로 24개를 먼저 고정한다. 단일/2단계 각각 2회여서 모델당 96턴이다. 후보를 고르는 동안 보류 사례의 출력을 보지 않는다.
- 후보를 고정한 최종 비교는 E4B와 API 각각 480턴이다. 개발용 후보 실행은 별도 표로 기록하며 최종 비교의 실패를 성공한 개발 표본으로 대체하지 않는다.
- 기대 ID의 **집합 완전 일치**를 한 턴 성공으로 센다. 부분 일치, 빈 응답, 형식 실패·timeout을 성공으로 계산하지 않는다. 게임 조건이 막았더라도 잘못 반환한 행동 신호는 오탐이다. `CanExecute`의 차단을 분류 정답으로 세지 않는다.
- 행동 신호 여부는 fixture에 미리 지정한 의도 종류로 판정한다. 상태 변경 전용 신호의 오류는 별도 호감도/변수 오변경으로 보고하며 신호 완전 일치에도 반영한다. Basic/Advanced의 반환 ID 기준 효과와 사용자의 기대 의도 기준 효과를 따로 재생한다.
- 상태·기억은 해당 질문이 있는 부분집합에서만 채점한다. 질문 반복·답변 회피를 정답으로 처리하지 않고 현재/과거·대상·개인 정보 주체를 구분한다. 명시한 허용 표현과 원문 검토를 사용하며 다른 생성 모델을 채점기로 추가하지 않는다.
- 성공률은 기존 96개와 새 24개를 각각 집계해 양쪽 모두 기준을 적용한다. 상태와 회상 하위 항목도 따로 공개한다. 실패한 의도적 취소/한도 테스트는 별도 경계 검사이며 정상 비교 중 발생한 오류는 비교 분모에 남긴다.
- 지연은 같은 클라이언트의 제출부터 성공/실패 정착까지 측정한다. 큐·전처리·각 생성·검증·로컬 최종화 시간을 구분하고 p50/p95/최대값·표본 수·실패율을 남긴다. 단일 요청 순차 평가로 두 전략을 맞추고 E4B와 API 성능 측정도 순차 진행한다. 추론 도중 합성 원문 파일 쓰기는 피하고 완료 뒤 제한된 증거를 보존한다.

### 실제 API 예산과 실행 절차

구현 검증용 **새 누적 upstream 상한은 1,000회**로 계획한다. 실패·취소된 시도와 준비용 실제 호출도 포함하며 재시작으로 초기화하지 않는다.

| 용도 | 최대 생성 시도 |
| --- | ---: |
| 고정 120개 × 2회 × (단일 1회 + 2단계 2회) | 720 |
| 2단계 핵심 12턴 × 2회 × root/consumer/player 3환경 × 최대 2회 생성 | 144 |
| 시작 확인·실제 취소/복구·남은 원인 확인 | 136 |
| 합계 상한 | 1,000 |

검증 전용 fixture 주입으로 동일한 합성 성공 이력을 준비하며 이전 대화를 만들기 위한 숨은 API 호출을 추가하지 않는다. 단계별 호출을 하기 **전에** durable 장부에 기록하고 잔여 예산이 실험 단위에 부족하면 시작하지 않는다. 자동 재시도와 실패 결과 교체는 하지 않는다.

fake와 소스 검사를 끝낸 뒤 기존 서버를 유지한 채 사용하지 않는 loopback 포트의 전용 서버를 준비한다. 키가 있는 사용자 터미널에서 검토 가능한 스크립트로 시작하고 키 전달·추출·저장을 요구하지 않는다. 인증·권한·할당량·rate-limit 오류가 생기면 API 검사를 중단하고 가능한 로컬 검사를 계속한다. 과거 576회/891회 장부와 이전 증거는 재사용하거나 초기화하지 않는다. 이번 계획 작성에서는 실제 호출을 하지 않는다.

### 구현 완료와 모델 채택 기준

- **구조적 완료:** 필수 자동 회귀와 지원 환경의 실제 실행 검사가 통과하고 기존 단일 호출·Core 계약·상태 수명·한 턴 한 행동을 보존한다. 실행 검증이 막힌 환경을 완료로 표시하지 않는다.
- **사용자가 선택한 의미 기준:** 행동 신호 오탐 **0건**, 신호 완전 일치 **95% 이상**, 상태·기억 정답 **90% 이상**을 모델별·평가 집합별로 적용한다. 정상 요청 누락·호감도 오변경·현재 상태 오류·개인 기억 누락을 따로 보고하며 통합 점수로 오탐을 상쇄하지 않는다.
- **정수 통과선:** 기존 회귀의 신호는 **183/192 이상**, 기존 상태·기억은 **35/38 이상**이다. 새 보류 평가의 신호는 **46/48 이상**, 상태·기억 12개 × 2회의 정답은 **22/24 이상**이다. 최종 fixture에서 분모가 바뀌면 후보 출력 확인 전에 `ceil(기준 비율 × 분모)`로 고정한다. 반올림한 백분율로 통과시키지 않는다.
- **응답 시간:** 워밍업 후 전체 p95 **5초 이내**를 E4B와 API의 채택 조건으로 적용한다. 표본 수·실패율·최대값·단계별 시간·API 호출량을 함께 기록하고 느린 응답·timeout을 제외하지 않는다.
- **판정:** 조합별로 따로 채택한다. 단일 호출은 비교 기준이며 새 기준을 이유로 기존 기능을 제거하지 않는다. 새 전략이 미통과한 조합은 지원 목록에 올리지 않고 원래 단일 호출을 유지한다. 한 조합만 통과하면 ‘그 조합 구현·검증 완료, 다른 조합 미채택’으로 보고하고 전체 완료로 묶지 않는다.

이 기준으로 과거 E4B 2단계는 신호 186/192와 오탐 0건을 충족했지만 상태·기억 34/38은 미달이다. API 과거 비교 점수는 참고값이며 새 보류 평가·현재 소스 실행 검증을 대신하지 않는다. 최종 지원 조합과 남은 제한은 구현 후 `PHASE17_VALIDATION.md`에 기록한다.

## 변경 범위와 보존

구현 대상은 추론 전략·조합, 로컬 요청/실행기와 Backend 생성 경로, NPC/Builder 설정, 현재 턴 진단·필요한 설정 검사, 관련 테스트·별도 샘플·사용 안내다. Phase 12 변수 계산·행동 선택 알고리즘은 유지한다. 새로 작성하거나 수정한 메서드에 기능 주석을 달고 관련 공개 호출부의 호환성을 확인한다.

현재 문서 정리의 변경 대상은 이 계획, [로드맵](ROADMAP.md), [저장소 지침](../AGENTS.md)이다. 구현 단계는 작은 변경 묶음으로 나누고 각 묶음의 입력·소스 해시와 검증을 기록한다. 기능 구현 때 실제 결과와 함께 저장소·패키지 안내, 계약 안내와 Unreleased를 갱신한다. 기존 Phase 12·15·16의 완료 기록, Phase 13·14 후보, AndroidSDK와 조사 자료를 보존한다. 브랜치 전체 병합, 버전/의존성 변경, push·태그·릴리즈는 포함하지 않는다.

검증·빌드·로그는 `E:\CodexValidation\<작업ID>`, TEMP/TMP·캐시는 `E:\CodexTemp\<작업ID>`를 사용한다. delivery에는 최종 빌드·설정·재실행 안내, evidence에는 소스 식별·명령·필수 결과를 보존한다. 허용된 고정 합성 테스트 원문만 승인 범위의 evidence에 남기고 사용자 대화·키·인증값·추론·원시 upstream 오류는 수집하지 않는다. 완료 전에 정확한 경로·소유권·사용 여부·링크를 확인해 불필요한 복제본·캐시를 정리하고 용량과 남은 항목을 보고한다. WSL2 활성화와 관리자 실행은 보류 상태를 유지한다.

## 근거와 이전 완료 기록

- [main 세 모델 비교](E:/CodexValidation/AI_NPC-main-three-model-20261012-a1/evidence/FINAL_REPORT.md), [집계](E:/CodexValidation/AI_NPC-main-three-model-20261012-a1/evidence/comparison-summary.json), [격리 후보 재현 소스 안내](E:/CodexValidation/AI_NPC-main-three-model-20261012-a1/delivery/README.md)
- [이전 실패 원인 조사](E:/CodexValidation/AI_NPC-failure-diagnosis-20261012-a1/evidence/FINAL_REPORT.md)
- [Android 공식 SDK·S10 검증 기록](E:/CodexValidation/AI_NPC-litert-sdk-20261011-a1/evidence/REPORT.md)
- [Phase 12 계획](PHASE12_PLAN.md)·[완료 기록](PHASE12_VALIDATION.md), [Phase 15 계획](PHASE15_PLAN.md), [Phase 16 계획](PHASE16_PLAN.md)·[완료 기록](PHASE16_VALIDATION.md)

로컬 E: 증거 링크는 이 PC의 보존 자료다. 최종 Phase 17 구현 검증은 새로운 소스 식별정보로 따로 기록한다.
