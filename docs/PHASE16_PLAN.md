# Phase 16 — Windows 로컬 NPC 대화

> 확정 계획: 2026-09-30 사용자 구현 승인. 승인 범위의 구현·실행 검증 완료, Unreleased.
> Unity 6000.5.3f1 / package 0.4.0 유지. 공개 v0.4.0에는 이 변경이 포함되지 않는다.

## 목표와 경계

Unity Editor와 Windows x64 Mono 게임에서 인터넷, OpenAI 또는 Node 서버 없이 실제 NPC 대화를 제공한다. 첫 검증 대상은 RAM 32GB, RTX 5070 Laptop 8GB다. 다른 플랫폼, IL2CPP, 저사양 보장과 두 후보 모델 비교는 후속 작업이다. Phase 12~14, 장기 기억, RAG, 로컬 음성, 스트리밍은 구현하지 않는다.

모델 원본은 외부 설치 폴더에서 읽는다. 게임의 자동 다운로드와 모델 복사는 없다. UPM에 모델·tokenizer·실행기·native DLL 또는 새 의존성을 포함하지 않는다.

## 고정 구성

| 항목 | 값 |
| --- | --- |
| 모델 | Gemma 4 E4B IT Q4_K_M GGUF, 5,335,289,664 bytes |
| 모델 SHA-256 | `3f72a20a06f626c78e6c475ae07a64c88b2663149c0f6197b56bf7cf1f37585c` |
| 실행기 | llama.cpp b11146 Vulkan Windows x64 |
| llama-server.exe SHA-256 | `7b886298b688509ced3e92b420edd57dd3d665da72c1fc207d7a537be5870352` |
| 템플릿 SHA-256 | `37d87b94f5d7ce4a3ba69fd625d63b2062502efd256c993b76a9764ae4b4ce53` |
| context / 출력 예약 | 4,096 / 256 tokens |
| temperature / seed | 0.2 / 42 |
| 준비 / 생성 제한 | 180초 / 35초 |
| 동시 생성 / FIFO 대기 | 1건 / 최대 8건, 각 30초 |
| NPC별 성공 대화 기억 | 최대 8턴, UTF-8 16KiB, 유휴 30분 |

GGUF 내장 템플릿 대신 사전 검사에서 확인한 LM Studio model.yaml의 Jinja 템플릿을 사용한다. thinking을 비활성화하고 reasoning 응답을 거부한다. 실행기 DLL도 manifest에 크기·해시를 기록하여 준비마다 검사한다.

## 구현 구성

- `NpcConversationMode.LocalInference = 5`. 기존 값과 Core/V4 계약은 유지한다.
- 순수 `AiCharacterKit.LocalInference`에 `ILocalInferenceRunner`, `LocalInferenceConversationClient`, NPC별 기억과 FIFO 대기열을 둔다.
- Unity `LocalInferenceProfile`은 외부 manifest를 지정하고 `LocalInferenceRuntime`은 Scene 수명으로 프로세스를 공유한다. NPC는 지정한 host, 없으면 부모 host를 찾는다. 개별 reset/취소는 공유 실행기를 종료하지 않는다.
- 설치 도구는 기존 파일을 검사하고 manifest와 파생 템플릿만 생성한다. 상대 경로는 manifest 폴더 기준이며 player의 `-aiNpcLocalManifest`가 Profile 경로를 덮어쓴다.
- 파일 검증 → 숨김 프로세스 → health → 합성 JSON 워밍업 → Ready. Windows CNG 해시와 대기는 메인 스레드를 막지 않는다. 준비 전 제출은 해당 요청만 실패한다. 실패 후 `PrepareAsync`로 명시적으로 다시 준비한다.
- loopback 임시 포트, 프로세스별 환경변수 인증값, Windows Job Object 종료 연동. stdout/stderr는 기록 없이 소비한다.
- V4 검증과 직렬화 후 512KiB 상한을 재사용한다. 캐릭터·grounding·trigger·사용자 입력을 구분한다. JSON Schema와 엄격한 출력 검사를 모두 거친다.
- 실제 템플릿 적용 후 토큰을 계산한다. 입력+출력 예약이 4,096을 넘으면 오래된 기억부터 제외한다. 현재 입력만으로 넘으면 거부하며 현재 데이터를 자르지 않는다.
- 성공한 사용자 입력과 대사만 기억한다. grounding·revision·실패·취소는 기억에 저장하지 않는다. 행동은 기존 router와 실행 직전 `CanExecute`를 거친다.
- Builder Scene/Prefab 적용·재적용·미리보기와 Awake는 정적 검사만 수행한다. 부모 host가 필요한 Prefab은 경고하고 외부 Scene 참조를 저장하지 않는다.
- Editor API로 host 하나와 NPC 둘의 Local Grounded NPC 샘플을 생성한다. 준비/재시도, 대화/reset, gate/alarm 변경을 제공한다.

## 검증과 마무리

기준선은 Phase 15 안정화 Server 106개, Unity EditMode 203개다. fake 회귀, Builder, Windows 프로세스 소유권·설치 실패, 실제 모델 8개 시나리오 두 번, 기억·reset·격리·grounding·행동 권한 변경, 반복 Play Mode와 Windows Mono player를 검증한다. 짧은 기본 응답의 워밍업 후 p95 5초를 목표로 하며 준비 시간·RAM/VRAM·프레임 간격을 기록한다. OpenAI 호출은 하지 않는다.

수정 소스의 root 복제본과 Built-in/Legacy consumer를 사용한다. 공개 패키지 캐시 결과를 수정본 검증으로 간주하지 않는다. 실행 검증이 막히면 전체 완료로 기록하지 않는다. 결과는 [검증 기록](PHASE16_VALIDATION.md), 설정은 [사용 안내](LOCAL_INFERENCE_QUICKSTART.md)에 기록한다.

작업 경로: `E:\CodexValidation\AI_NPC-phase16-implementation-20260930-a1`의 delivery/evidence. TEMP/TMP와 GI 캐시는 대응하는 `E:\CodexTemp` 작업 폴더를 사용한다. 최종 증거와 재실행 파일을 보존하고 불필요한 작업 소유 복제본·캐시는 정리한다. 새/수정 메서드 주석, 기존 GUID, Phase 15 변경을 보존하며 push·태그·릴리즈 발행은 하지 않는다.

## 근거

- [고정 llama.cpp API](https://github.com/ggml-org/llama.cpp/blob/b11146/tools/server/README.md)
- [Windows Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)
- 사전 검사: `E:\CodexValidation\AI_NPC-phase16-model-audit-20260930-031830\evidence\MODEL_ASSESSMENT.md` (이번 통합 검증과 별도)
