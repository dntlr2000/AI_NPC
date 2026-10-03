# Phase 16 구현 검증 기록

검증일: 2026-09-30. 승인 범위인 **Windows Editor / Windows x64 Mono, Gemma 4 E4B IT Q4_K_M + llama.cpp b11146 Vulkan**의 구현과 실행 검증을 완료했다. 이 변경은 **Unreleased**이며 공개 `v0.4.0`에 포함되지 않는다.

> **2026-09-30 추가 샘플 검토 — 당시 미완료, 2026-10-02 보완 결과는 다음 절.** Unity Local Grounded NPC에서 문이 열리면서 거절 대사가 나오고, 열린 문을 닫혔다고 답하는 현상을 재현했다. 42회 모두 현재 상태가 정확히 전달됐지만, 문 열기 6건 중 5건에서 거절 표현이 동반됐고 행동 직후 상태 질문 6건 모두 닫힘 표현이 나왔다. 기억 초기화 후 상태 질문은 6/6 맞았다. 기존 행동 검사는 실제 상태/실행 횟수만 확인했고, 대사 키워드 검사는 부정문을 구별하지 못했다. 아래 최초 통과 기록은 당시 검사 범위의 결과이며 샘플의 의미적 정합성까지 보장하지 않는다. 해당 검토에서는 동작 코드를 수정하지 않았다. 재현·원인·보완 방향: `E:\CodexValidation\AI_NPC-phase16-gate-review-20260930-b1\evidence\REVIEW.md`.

## 2026-10-02 — 로컬 샘플 대사·행동 정합성 보완

작업: `AI_NPC-phase16-gate-fix-20261002-c1`. 증거는 `E:\CodexValidation\AI_NPC-phase16-gate-fix-20261002-c1\evidence`, 재실행용 Windows Mono 빌드와 manifest는 같은 작업의 `delivery`에 보존한다. 모델과 실행기는 기존 외부 설치를 재사용했다. 패키지 버전·의존성·Core/V4 인터페이스·공개 태그와 Phase 12~14 후보 계획은 변경하지 않았다.

### 변경 내용과 적용

- 로컬 NPC에 `INpcActionResponseFormatter`가 있으면 검증된 모델 응답을 기존 action router에 한 번 전달하고, 실제 성공·거절 결과로 대사를 확정한 뒤 기억과 화면에 반영한다. `CanExecute`는 실행 직전에 유지한다. 샘플의 문 행동은 Inspector에서 편집 가능한 한국어/영어 결과 문구를 사용하며 감정·제스처도 확정한다. 추가 모델 호출·자동 재요청은 없다. 기존 formatter 없는 NPC와 Mock/API/음성 경로의 처리 순서는 유지한다.
- 현재 관찰을 과거 대화 뒤에 배치한다. 보존된 사용자 발언은 별도의 인용 데이터로 함께 제공해 개인 정보 회상과 현재 세계 상태를 구분한다. 이 인용은 현재 요청에서만 구성하고, 실제 토큰 계산과 오래된 대화 제외를 함께 적용한다. grounding과 revision은 여전히 기억에 저장하지 않는다.
- Mira/Toma가 `LocalGroundedGate`의 문·경보를 공유하고 기억만 분리한다. UI는 공유 상태와 실제 action status를 보여 준다. 기존 imported 샘플 스크립트도 함께 갱신했다. 이전 씬은 실행 시 첫 NPC의 기존 초기값으로 공통 상태를 연결하므로 재생성할 필요가 없다. 원본 Scene/Prefab/Profile YAML과 기존 GUID는 모두 유지했다.
- 수정 파일: `Runtime/LocalInference/LocalInferenceConversationClient.cs`, `Runtime/Unity/Actions/INpcActionResponseFormatter.cs`(추가), `Runtime/Unity/Controllers/NpcConversationBehaviour.cs`, `Runtime/Unity/LocalInference/LocalInferenceRuntime.cs`, `Runtime/Unity/Networking/LocalInferenceJsonCodec.cs`, `Tests/Editor/LocalInferenceTests.cs`; package/imported `LocalNpc`의 `LocalGroundedNpc.cs`, `LocalGroundedNpcPanel.cs`, `LocalGroundedGate.cs`(추가), `Editor/LocalGroundedNpcSampleBuilder.cs`; 중복 설치 안내·변경 이력·로드맵·저장소 지침. 새 스크립트 meta는 Unity가 생성한 값을 사용했다. 전체 경로와 SHA-256은 `source-audit.json`에 있다.

### 최종 검증

| 검사 | 결과 | 증거 |
| --- | --- | --- |
| Server fake 회귀 / TypeScript 컴파일 | **106/106**, 컴파일 성공; 실제 OpenAI 호출 없음 | `server-final.json`, `server-compile.log` |
| root / Built-in·Legacy 전체 Unity EditMode | 각각 **248/248**, 실패·skip 0; 최종화 관련 8개 추가 | `root-editmode-complete.xml`, `consumer-editmode-complete.xml` |
| 기존 root 씬 실제 모델 Play Mode | **54/54**, 개인 기억·격리·reset·자식 종료 통과 | `root-model-final.json` |
| Built-in·Legacy 실제 모델 Play Mode | **54/54**, 개인 기억·격리·reset·자식 종료 통과 | `consumer-model-final.json` |
| Windows x64 Mono 빌드와 실제 모델 | 빌드 성공, **54/54**, 개인 기억·격리·reset·자식 종료 통과 | `windows-build-final.log`, `player-model-final.json` |
| 수정 소스 일치 / 보호 대상 | C#/asmdef **213개**가 두 검증 프로젝트와 일치; 기존 직렬화 에셋·GUID·버전 유지 | `source-audit.json`, `player-assemblies.json` |

각 환경에서 실제 모델 요청 **56회**와 의도적으로 모순을 넣은 fake 응답 **2회**를 사용했다. 54개 상태/행동 검사는 원래 42회 연속 질문, 경보 거절·이미 열린 문·생성 중 경보 변경, 강제 거절 대사와 실제 문 열기 및 다음 턴의 최종 기억, 개인 정보를 유지한 상태 변경을 포함한다. 추가 네 요청으로 개인 정보 회상·NPC 격리·reset을 검사했다. 따라서 최종 세 환경에서 실제 모델 요청은 총 **168회**다. `GateFixProbe.cs`는 모델의 원문·추론·grounding을 저장하지 않고 판정 값만 기록한다.

검사는 부정문을 구분하고 긍정적인 상태 근거도 요구한다. 행동 ID가 없는 올바른 경보 거절이나 이미 열린 문 안내도 정상으로 판단하되 실제 효과가 없어야 한다. 행동이 실행된 경우에는 결과 문구와 다음 요청에 저장된 대사까지 검사한다. 현재 provider fact가 정확히 전달됐는지도 계속 확인했다.

### 한계와 검증 중 발견 사항

- 중간 구현에서는 문 상태 검사에 통과해도 개인 정보 회상이 실패했다. 보존된 사용자 발언을 현재 관찰과 구분하여 인용하는 보완 후 같은 회상·격리·reset 검사가 최종 세 환경에서 통과했다. `memory-diagnostic.json`, `memory-retained.json`과 최종 보고서에 구분한다.
- 다른 Unity 작업 및 빌드가 함께 실행되던 중 한 검증 실행에서 준비 157.63초와 첫 생성 timeout을 관측했다. 최종 통과 실행의 준비 시간은 root **71.10초**, consumer **54.41초**, player **58.33초**다. 자원 부하에 따른 지연 가능성은 남는다. 이번에는 응답 p95를 재측정하지 않았으며, 아래 최초 p95 값은 이번 프롬프트의 성능 보증이 아니다.
- 일반 대화와 상태 질문은 여전히 모델이 생성하며 **요청 시점 snapshot**을 기준으로 한다. 이 합성 시나리오의 통과가 모든 자연어 표현이나 응답 중 외부 상태 변경까지 보장하지는 않는다. 결과 문구 보장은 샘플의 문 행동과 명시적으로 연결한 local formatter에 적용된다. 이미 완료된 효과는 이후 취소나 formatter 실패로 롤백하지 않는다.
- 소비자 최초 회귀 실패는 URP/Input System 프로젝트에서 복사한 기존 UI 샘플 구성이 원인이었다. 해당 복제본에서 기존 Editor 생성기를 사용해 모든 Legacy UI 샘플을 재구성한 뒤 전체 회귀가 통과했다. 원본 씬은 변경하지 않았다. Unity Search 초기화 예외는 이전 검증에서도 보인 Editor 로그 항목이며 실제 컴파일·검사·플레이는 완료됐다.
- IL2CPP·다른 플랫폼·저사양 보증·새 릴리즈는 이번 작업에 포함하지 않는다. 정리 결과와 용량, 필수 증거·최종 빌드 보존 이유는 같은 evidence의 `REPORT.md`, `cleanup-result.json`에 기록한다.

## 2026-09-30 최초 검증의 소스와 환경

- 작업 ID: `AI_NPC-phase16-implementation-20260930-a1`
- 원본 소스: `E:\Unity\AI_NPC`, HEAD `027b61f053c620d4e5202830b18692a6d45d3458`와 작업 중 변경. 커밋·push·태그·릴리즈 발행은 하지 않았다.
- 증거: `E:\CodexValidation\AI_NPC-phase16-implementation-20260930-a1\evidence`
- 납품물: 같은 작업 폴더의 `delivery`. 모델 원본은 `E:\Models\gemma-4-E4B-it-GGUF\gemma-4-E4B-it-Q4_K_M.gguf`를 참조하며 복사하지 않았다.
- Unity `6000.5.3f1 (c2eb47b3a2a9)`, Windows 11 Home `10.0.26200`, RAM 32GB, RTX 5070 Laptop 8GB, NVIDIA driver `592.82`.
- Node `24.18.0`, TypeScript `7.0.2`, .NET SDK `10.0.400`. Unity 라이선스 활성화 후 실제 Editor 실행·컴파일·player 빌드를 수행했다.
- root 복제본은 현재 embedded package를, Built-in/Legacy consumer는 `file:E:/CodexValidation/AI_NPC-phase16-implementation-20260930-a1/package-snapshot`을 사용했다. Runtime/Editor/Tests/Samples의 C#/asmdef **211개 파일이 두 복제본 모두 원본과 일치**한다. `validation-source-identity.json`에 SHA-256과 player assembly 식별정보를 보존한다.
- `baseline.patch`, `baseline-files.json`으로 기존 Phase 15 변경과 이번 변경을 구분한다. 기존 에셋·GUID·Unity/패키지 버전·의존성·Phase 12~14 후보 문서는 보호 검사 대상이다. 기존 샘플 5개 소스의 미제공 meta는 기준선 그대로이며 이번에 추가한 소스에는 Unity가 생성한 meta를 보존했다.

## 자동 회귀와 실제 실행

| 검사 | 결과 | 원본 증거 |
| --- | --- | --- |
| Server 전체 fake 회귀 | **106/106**, 실제 OpenAI 호출 없음 | `server-final.json` |
| TypeScript 컴파일 | 성공, 출력은 작업 소유 `server-dist` | `verification-commands.md` |
| root 전체 Unity EditMode | **240/240**, 실패·skip 0 | `root-editmode-240.xml`, `.log` |
| Built-in/Legacy consumer 전체 EditMode | **240/240**, 실패·skip 0 | `consumer-editmode-240.xml`, `.log` |
| root 실제 모델 전체 반복 Play Mode | 같은 Editor에서 **35/35 × 2**, Domain Reload 비활성화 | `root-repeat-final-cycle1.json`, `root-repeat-final.json` |
| consumer 실제 모델 전체 Play Mode | **34/34**, 기본 8개 시나리오 × 2 포함 | `consumer-playmode-final.json`, `.log` |
| 마지막 수명 수정 후 root 반복 Play Mode | **5/5 × 2**, 같은 Editor, Domain Reload 비활성화 | `root-lifecycle-delivery-cycle1.json`, `root-lifecycle-delivery.json` |
| 마지막 수명 수정 후 consumer Play Mode | **5/5**, 준비 95.608초, 자식 정리 확인 | `consumer-lifecycle-delivery.json` |
| 최종 Windows x64 Mono 빌드 | 성공, D3D12 player | `windows-build-delivery.log`, `build-result.txt` |
| 최종 Windows 실제 모델 전체 | **35/35**, 기본 16/16, 한글·공백 manifest CLI override | `player-delivery.json`, `.log` |
| 한글·공백 실행기 경로, 포트 점유, 실행기 조기 종료 | 모두 통과, 자식 정리 확인 | `native-failures.json`, `.log` |
| 최종 게임 프로세스 강제 종료 | Job Object 자식 종료, 무관한 프로세스 생존 | `abnormal-exit.json`, `abnormal-player.log` |

Phase 15 안정화 기준선은 Server 106 / Unity 203이었다. Unity 회귀 **37개를 추가**했으며 기존 Mock·Backend·V1~V4·TTS·STT 회귀도 함께 실행했다. 기존 공개 시점의 Server 95 / Unity 186 기록은 변경하지 않는다.

전체 root/consumer 모델 검사는 마지막 `StopAsync`와 재준비 정리 대기의 순서 수정 전에 수행했다. 그 수정 뒤 전체 EditMode 240개를 양쪽에서 다시 실행하고, root 반복·consumer 수명 검사와 **최종 Windows 전체 35개 검사**를 통과했다. 과거 공개 패키지나 사전 모델 검사 결과를 현재 수정 소스 결과로 대체하지 않는다.

검사한 동작:

- fake 실행기로 준비 전 실패와 복구, NPC별 기억·reset·TTL·바이트/턴 한도, 취소·중복 제출, FIFO 순서·대기 한도·timeout·종료를 확인했다.
- 기존 V4 검증과 직렬화된 UTF-8 512KiB 상한, JSON 이스케이프, 실제 template/tokenize 결과에 따른 오래된 기억 제외와 현재 입력 초과 거부를 검사했다. 현재 프로필·grounding·trigger·사용자 입력은 자동으로 잘라내지 않는다.
- JSON 누락·중복·추가 필드, reasoning, 잘린 출력, 잘못된 감정/제스처와 중복·미등록 trigger를 거부했다. 예외·메모리 부족은 고정 오류로 변환한다. 실제 GPU 메모리 고갈을 강제로 발생시키지는 않았고 오류 매핑을 fake로 검사했다.
- Builder Scene/Prefab 적용·재적용, 명시/부모 host, 외부 Scene 참조 거부, authored 미리보기는 실행기 시작과 live provider 호출 없이 처리된다. 실제 sample provider는 Start에서 준비되며 같은 NPC의 준비 전 실패 후 제출이 성공했다.
- 실제 모델에서 8개 한국어 기본 시나리오를 두 번 실행하고 기억 회상·reset·두 NPC 격리·변경된 grounding·revision을 확인했다. 생성 뒤 상태를 바꾸면 최종 `CanExecute`가 행동을 막고 허용된 경우에는 행동이 실행됐다.
- 한 NPC 취소 후 다른 대화가 성공했다. 생성 중 자식 종료는 안전하게 실패했고 명시적 재준비 후 같은 client가 성공했다. 요청 중 host 종료와 반복 Play Mode 후 자식이 남지 않았다.
- 정리 대기 중 host가 종료되면 재준비가 새 프로세스를 시작하지 않는 회귀를 추가했다. 프로세스 종료를 기다린 뒤 작업 폴더를 정리하여 늦은 native cache 쓰기와 삭제가 겹치지 않도록 했다.

## 측정값과 제한

최종 Windows player, 기본 **16회**의 워밍업 후 응답 p95는 **3.199초**로 목표 5초 이내였다. 첫 준비는 **67.734초**로 180초 제한 이내였다. 준비에는 파일 해시·모델 로드·합성 워밍업이 포함된다. 더 긴 실제 게임 입력이나 다른 PC의 성능을 보장하는 값은 아니다.

| 측정 | 최종 Windows 값 |
| --- | --- |
| 실행기 최대 working set, 2초 간격 표본 | 3,456,573,440 bytes (약 3.22 GiB) |
| 실행기 OS peak working set | 3,747,405,824 bytes (약 3.49 GiB) |
| 게임 최대 working set, 표본 | 357,953,536 bytes (약 341 MiB) |
| 시스템 전체 GPU 사용 최대, 표본 | 4,653 MiB |
| Unity 프레임 간격 p95 / 최대 | 16.785 / **513.630 ms** |

GPU 수치는 데스크톱과 다른 앱을 포함한 시스템 전체이며 모델 단독 VRAM 수치가 아니다. `player-delivery-resources.json`과 `player-delivery-metrics.json`에 표본·집계 방법을 남긴다. 마지막 준비 순서 수정 전 player 반복에서는 최대 프레임 간격 663.594ms도 관측했다. 평균적인 프레임 간격과 별개로 준비/프로세스 수명 전환을 포함한 측정 구간에서 일시 정지가 발생했으므로 **무정지 프레임을 보장하지 않는다**. 해시 계산 자체는 별도 스레드에서 수행한다.

전체 consumer 측정은 준비 131.232초, 기본 p95 4.661초였고 root 전체 반복은 준비 110.187/101.480초, p95 4.413/4.383초였다. 수명 smoke에는 기본 시나리오 집합을 실행하지 않으므로 해당 JSON의 `basicP95Ms=0`은 성능 결과로 사용하지 않는다.

검증은 합성 시나리오이며 모든 자연어 대화 품질에 대한 보장은 아니다. Windows Mono와 지정 모델/실행기만 완료 범위다. 다른 플랫폼·IL2CPP·CPU 성능·두 모델 비교·로컬 음성·RAG·장기 기억·스트리밍은 검증하거나 구현하지 않았다.

consumer의 복제된 Library에서 Editor Search 인덱스 초기화 예외가 한 차례 기록되었으나 실제 컴파일·회귀·모델 검사는 통과했다. 엔진 검색 캐시 문제를 프레임워크 성공 기록에서 숨기지 않으며 최종 사용 소스나 패키지 의존성은 이를 위해 변경하지 않았다.

## 변경 파일과 재실행

전체 목록은 증거의 `changed-files.txt`와 `source-audit.json`에 있다. 기준선 이후 이번 Phase 16 변경만 식별한다.

- `Runtime/LocalInference/`: 공개 runner/client 경계, 제한된 기억, FIFO.
- `Runtime/Transport/LocalInference/`: Unity에 의존하지 않는 설치/HTTP DTO.
- `Runtime/Unity/LocalInference/`, `Networking/LocalInferenceJsonCodec.cs`, `Networking/UnityWebRequestLocalInferenceRunner.cs`: profile/공유 host, 검증·해시·Job Object·통신·출력 검사.
- `Runtime/Unity/Controllers/`, `Editor/CharacterBuilder/`, `Editor/LocalInference/` 및 관련 asmdef: 새 mode, 기존 조립 경로 호환, 설치 도구.
- `Samples~/AI NPC Prototypes/LocalNpc/`, `Tests/Editor/LocalInference*`, `CharacterBuilderServiceTests.cs`: Editor 생성 sample와 회귀.
- 로드맵·계획·이 문서·저장소 지침·README·재사용/grounding 안내·중복 local quick start·Unreleased 변경 이력.

`delivery/Start-LocalNpc.ps1`로 NPC 둘의 대화·기억 초기화·gate/alarm 변경·준비 재시도를 사용할 수 있다. `delivery/Windows/LocalNpc.exe`에는 `-phase16Evidence`를 명시할 때만 실행되는 검증 probe가 포함된다. 일반 실행은 자동 검증을 시작하지 않는다. 외부 runtime, manifest, template와 필요한 라이선스 고지를 함께 보존한다.

자동 재실행 도구·입력 시나리오·명령·버전은 `evidence`에 보존한다. 생성 대사·사용자 원문·grounding·내부 추론·인증값은 로그나 결과 JSON에 기록하지 않는다.

## 작업 경로와 정리

복제본·빌드·로그는 이 작업의 `E:\CodexValidation` 폴더를, TEMP/TMP·UPM cache는 `E:\CodexTemp\AI_NPC-phase16-implementation-20260930-a1`을 사용했다. 최종 Unity 명령은 `-giCustomCacheLocation`으로 GI cache도 E:에 둔다. 초기 실행에서 잘못된 `-giCachePath` 인자로 기존 C: GI cache 경로가 로그에 나타나 올바른 인자로 수정했다. 그곳의 확인된 디렉터리는 기존 3월 생성 공유 캐시이므로 삭제하지 않았다.

검증 복제본, 작업별 TEMP/UPM/GI cache, 다운로드 중간본, 프로세스 작업 폴더, 중복 로그·빌드 디버그 정보와 사용하지 않는 실행기 CLI 등 **76개 대상, 2,639,897,887 bytes (약 2.64 GB)**를 정리했다. 삭제 시 E: 여유 공간 증가는 2,722,451,456 bytes였다. 정확한 절대 경로·소유권·사용 중인 프로세스 없음·정션 없음 확인과 삭제 후 확인은 `cleanup-plan.json`, `cleanup-result.json`에 기록했다. 미정리 대상은 없다.

`delivery`의 최종 게임·실행기·manifest·template·실행 스크립트와 `evidence`의 필수 원본 결과·명령·버전·소스 식별정보를 보존했다. 복제본 대신 검증된 package source와 프로젝트 설정/생성 sample 입력을 작은 ZIP 두 개로 보존하여 재현할 수 있게 했다. `artifact-inventory.json`에 각 파일의 보존 이유와 SHA-256을 기록한다. 모델 원본, 실제 저장소, 다른 작업과 공유 캐시는 정리 대상에 포함하지 않았다.
