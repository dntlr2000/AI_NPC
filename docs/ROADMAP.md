# AI NPC Framework 진행 점검 및 로드맵

> 기준일: 2026-09-01
> 비교 기준: ChatGPT 대화 **“Unity Ai NPC 만들기”**의 초기 구상과 이후 합의된 Phase 1~8 범위

## 결론

저장소는 수정된 로드맵의 순서와 제약을 따르고 있다. **Phase 1~8의 구현과 검증이 완료됐으며, Phase 8은 별도 Built-in/Legacy 프로젝트에서 실제 입력·Mock 응답·consumer-owned presentation까지 확인했다.** Mock 재사용성, stateless V1, session V2와 선택형 TTS/STT 계약은 그대로 유지된다.

Phase 7 체크포인트는 `ea4187b`다. Phase 8 범위와 검증 환경은 [`PHASE8_PLAN.md`](PHASE8_PLAN.md), raw Assets 재사용 절차는 [`REUSE_GUIDE.md`](REUSE_GUIDE.md)를 따른다. 기존 대화·Speech·Transcription wire 계약은 변경 없이 유효하다.

## 우리가 만드는 것

**AI Character Kit은 특정 게임의 NPC 한 명이 아니라, 여러 Unity 3D 프로젝트에서 재사용할 수 있는 AI NPC 런타임 프레임워크다.** 게임이나 UI에 종속되지 않은 대화 제어 계약을 중심에 두고, 캐릭터 설정·대화 공급자·화면 표현을 교체 가능한 경계로 분리한다. 개발 중에는 결정적 Mock으로 비용과 네트워크 없이 동작하고, 온라인 단계에서는 같은 Core 계약에 Backend 어댑터를 연결한다.

목표 사용 흐름은 다음과 같다.

```text
사용자 입력 + CharacterProfile
        ↓
    AiNpcRequest
        ↓
 NpcAIController ── IAiConversationClient
                         ├─ MockConversationClient
                         ├─ BackendConversationClient
                         │            ↕ JSON Contract V1 (stateless)
                         └─ [Phase 5] SessionBackendConversationClient
                                      ↕ JSON Contract V2 + sessionId/reset
                                  Backend bounded memory → OpenAI
        ↓
    AiNpcResponse
        ↓
 INpcPresentationDriver
        ├─ 대사 UI + 감정 표현 + 제스처/애니메이션
        └─ [Phase 6] Speech decorator
                    → ISpeechSynthesisClient → Speech Backend
                    → fixed PCM → ISpeechPlaybackDriver

[Phase 7 optional input adapter]
Push-to-Talk → IAudioCaptureDriver → bounded WAV
             → ITranscriptionClient → local Backend → OpenAI transcription
             → existing text field → 사용자 검토·수정 → Send
```

프레임워크가 제공할 핵심은 다음과 같다.

- 디자이너가 에셋으로 관리하는 캐릭터 성격·말투·예시 대사
- 중복 요청, 취소, 성공과 실패를 일관되게 처리하는 순수 C# 대화 제어
- Mock과 실제 Backend를 교체해도 유지되는 `IAiConversationClient` 계약
- 대사와 감정·제스처 명령을 전달하는 버전 고정 JSON 계약
- 최근 성공 turn만 제한적으로 보관하고 NPC별로 reset 가능한 process-local session
- uGUI, 3D 캐릭터 또는 다른 표현 방식을 교체할 수 있는 presentation 경계
- 캐릭터별 provider 설정을 숨기는 opaque voice preset과 선택형 TTS 경계
- 캐릭터와 무관하게 재사용하고 자동 전송하지 않는 선택형 Push-to-Talk STT 입력 경계
- 샘플, 자동 테스트, 두 번째 프로젝트 검증을 거친 최종 UPM 패키지

API 키와 OpenAI 호출은 Unity 클라이언트가 아니라 Backend가 소유한다. TTS와 STT는 대화 Core를 변경하지 않는 선택형 adapter이며 전사 결과는 기존 텍스트 입력에서 검토한다. Realtime은 지연·끼어들기 요구가 실제로 확인된 뒤에만 검토한다. 퀘스트, 관계도, 범용 자율 에이전트와 게임별 행동 트리는 현재 목표가 아니다.

## 현재 기준선

- Unity: `6000.5.3f1`
- 주요 설치 패키지: URP `17.5.0`, Input System `1.19.0`, uGUI `2.5.0`, Test Framework `1.7.0`
- 구현 위치: `Assets/AiCharacterKit/`
- 샘플: `MockNpcPrototype.unity`, `MultiCharacterMock.unity`, `BackendNpcPrototype.unity`, `MemoryNpcPrototype.unity`, `SpeechNpcPrototype.unity`, `VoiceInputNpcPrototype.unity`
- Git 기준선: `ea4187b` (Phase 7 체크포인트)
- Backend: Node.js 24 + TypeScript + Fastify + OpenAI SDK, 대화 V1/V2와 선택형 Speech/Transcription V1, loopback 전용
- 제외 범위: 영구·장기·Vector 기억, Realtime, VAD, 자동 전송, 원격 배포, client auth, streaming

## 초기 로드맵과의 비교

초기 구상의 순서는 **텍스트 NPC → 실제 GPT/구조화 JSON → 감정·애니메이션 명령 → CharacterProfile → 기억 → 서버 정리 → 패키지 → 음성 → Character Builder → Realtime**이었다. 이후 대화에서 위험을 줄이기 위해 **오프라인 Mock → 다중 프로필 → 전송 계약 → 실제 모델 → 단기 기억 → TTS → STT/Realtime → 두 번째 프로젝트 → UPM** 순서로 정제됐다.

| 계획 축 | 현재 상태 | 판단 |
| --- | --- | --- |
| 텍스트 NPC vertical slice | 입력, 결정적 Mock 응답, 출력 UI와 샘플 NPC 구현 | 완료 |
| 구조화 응답 | `AiNpcResponse`와 분리된 V1 JSON DTO·validator·codec | Phase 3 완료 |
| 캐릭터 데이터 | Mina·Luna·Guard 프로필과 다중 NPC 재사용 검증 | Phase 2 완료 |
| 표현 명령 | 색상으로 감정, 회전으로 제스처를 확인 | vertical slice 충족; Animator 연동은 의도적으로 미구현 |
| 실제 모델·백엔드 | loopback Backend와 Structured Output 경로 구현 및 라이브 1회 검증 | Phase 4 완료 |
| 제한된 단기 기억 | V2 session/reset, bounded process memory, 두 NPC 샘플 구현 | Phase 5 완료; 실제 모델 수동 검증 완료 |
| 선택형 TTS | pure speech 경계, preset 기반 Backend, PCM Unity playback 구현 | Phase 6 자동·수동 검증 완료 |
| Push-to-Talk STT | pure input 경계, bounded WAV, Backend transcription, reviewed text 입력 | Phase 7 자동·수동 검증 완료 |
| 장기 기억·Realtime·패키지화 | 구현하지 않음 | 선행 구현을 피한 올바른 상태 |

초기 대화에서는 실제 GPT/JSON 응답이 비교적 앞에 있었으나, 이후 계획은 Mock → 프로필 재사용 → 전송 계약 → 백엔드 순서로 정리됐다. 현재 저장소는 이 수정된 순서를 따른다.

## 구현 및 검증 현황

- Core: 요청/응답 모델, `IAiConversationClient`, 결정적 `MockConversationClient`, 중복·취소·오류를 처리하는 `NpcAIController`
- Transport: Unity 비의존 V1 DTO, validator, mapper와 Unity 경계의 `JsonUtility` codec
- Unity 경계: `CharacterProfile`, `NpcConversationBehaviour`, uGUI 입력, `INpcPresentationDriver` 구현
- Backend: V1 stateless 경로, V2 검증과 bounded session store, OpenAI Structured Output, 취소·timeout·오류 매핑과 안전한 telemetry log
- Unity networking: V1 client/gateway와 V2 session client/gateway; 기존 Mock mode 유지
- Speech: provider-neutral controller/interface, 별도 Speech V1 계약, Backend voice preset, Unity PCM playback과 presentation decorator 구현
- Transcription: provider-neutral controller/interface, canonical WAV encoder, 별도 V1 계약, Backend file transcription과 Unity microphone/input adapter 구현
- 자동 설정: `PrototypeSceneBuilder`가 Editor API로 프로필과 Mock/Backend/Memory/Speech/VoiceInput 샘플 씬을 생성·복구
- 의존성: Core asmdef는 `noEngineReferences: true`; Runtime에는 `UnityEditor` 참조가 없음
- 자동 검증: Server build 및 Vitest **75/75**, Unity 6000.5.3f1 컴파일, Voice Input scene 생성·복구와 전체 EditMode **105/105** 통과, 실패·건너뜀 0
- 수동 검증: Phase 1·2 Play Mode, Phase 3 계약, Phase 4 실제 모델 smoke test, Phase 5 live memory/reset, Phase 6 live TTS·교체·중지·fallback, Phase 7 live microphone/STT·검토·취소 검증 완료

## Phase 1 완료 기록

- Unity EditMode 11/11 통과, 실패·건너뜀 0
- Play Mode에서 대사·감정·제스처와 입력 예외 흐름 정상 확인
- `Packages/`와 `ProjectSettings/` 변경 없음
- OpenAI, HTTP, 서버, 기억, 음성 코드 없음
- `main`에 `e2f2c8d (Phase1)` 커밋 생성, 작업 트리 clean

**판정:** Phase 1 완료. Phase 2 시작 가능.

## 향후 작업 순서

### Phase 2 — 다중 캐릭터와 데이터 주도 재사용

- **상태: 완료 — `8708f4f (Phase2)`**
- 상세 구현 계획: [`PHASE2_PLAN.md`](PHASE2_PLAN.md)
- 성격과 말투가 대비되는 프로필 2개(예: Luna, Guard)를 만든다.
- 기존 Mock 규칙과 서로 다른 프로필 데이터를 사용해 같은 입력은 같은 프로필에서 항상 같고, 다른 프로필에서는 구분되게 한다.
- `characterId` 분기 없이 프로필 데이터만 사용하며 필수 필드 검증을 추가한다.
- 동일한 Core와 Controller를 두 NPC 또는 프로필 선택 UI에서 재사용한다.
- 프로필별 결정성·차별성·유효성 테스트를 추가한다.

**종료 조건:** 두 캐릭터가 코드 복제 없이 구분되고 기존 11개 테스트를 포함한 전체 테스트가 통과한다.

### Phase 3 — Unity ↔ Backend 전송 계약

- **상태: 완료 — `cfc5b04 (Phase3)`**
- 상세 구현 기록: [`PHASE3_PLAN.md`](PHASE3_PLAN.md)
- V1 wire 규격: [`CONTRACT_V1.md`](CONTRACT_V1.md)
- Core 도메인 모델과 직렬화 DTO를 분리하고 버전이 있는 JSON 계약을 정의한다.
- 요청 ID, 캐릭터 스냅샷, 사용자 입력, 대사·감정·제스처, 오류 형식을 고정한다.
- 정상·누락·알 수 없는 enum·잘못된 JSON에 대한 golden fixture 테스트를 만든다.
- 이 단계에서는 실제 OpenAI 호출이 없어도 된다. `server/` 생성은 별도 구현 승인을 받은 뒤 진행한다.

### Phase 4 — Backend와 실제 OpenAI Structured Output

- **상태: 완료 — `ab53815 (Phase4_1)`**
- 상세 구현 기록: [`PHASE4_PLAN.md`](PHASE4_PLAN.md)
- API 키를 서버에만 두고 `IAiConversationClient`의 네트워크 어댑터를 추가한다.
- 스키마 검증, 취소, timeout, 재시도 제한, 오류 매핑, 민감정보 없는 로그를 구현한다.
- Mock 경로를 유지해 오프라인 개발과 회귀 테스트가 계속 가능하게 한다.

### Phase 5 — 세션과 단기 기억

- **상태: 완료 — `d8ae5f7 (Phase5)`**
- 상세 구현 기록: [`PHASE5_PLAN.md`](PHASE5_PLAN.md)
- V2 wire 규격: [`CONTRACT_V2.md`](CONTRACT_V2.md)
- 최근 8개 성공 turn과 16 KiB의 process-local buffer, TTL·LRU·capacity 제한을 구현했다.
- NPC component별 안정적인 opaque session ID, 캐릭터 결합, 명시적 reset과 shared busy gate를 추가했다.
- V1·Mock을 유지하고 장기 기억이나 Vector DB는 실제 요구와 평가 기준이 생길 때까지 보류한다.

### Phase 6 — TTS

- **상태: 완료 — 자동 검증 및 실제 OpenAI TTS Play Mode 수동 검증 통과**
- 상세 구현 계획: [`PHASE6_PLAN.md`](PHASE6_PLAN.md)
- Speech wire 규격: [`SPEECH_CONTRACT_V1.md`](SPEECH_CONTRACT_V1.md)
- 대화 Core와 캐릭터 코드를 변경하지 않는 선택형 synthesis/playback 경계를 추가한다.
- Unity에는 opaque `voicePresetId`만 두고 실제 OpenAI voice 설정은 Backend JSON preset이 소유한다.
- 재생 취소·교체, on/off, text fallback과 AI-generated disclosure를 검증한다.

### Phase 7 — Push-to-Talk STT

- **상태: 완료 — Server 75/75, Unity EditMode 105/105 및 실제 microphone/OpenAI Play Mode 검증 통과**
- 상세 구현 계획: [`PHASE7_PLAN.md`](PHASE7_PLAN.md)
- Transcription wire 규격: [`TRANSCRIPTION_CONTRACT_V1.md`](TRANSCRIPTION_CONTRACT_V1.md)
- 최대 15초의 bounded PCM16 mono WAV를 local Backend에 보내 file transcription한다.
- 전사문은 기존 입력 필드에만 채우며 자동 전송하지 않는다.
- 취소·중복·마이크/Backend 실패를 대화·TTS와 독립적으로 처리한다.
- 지연시간과 끼어들기 요구가 확인되기 전에는 Realtime·VAD·streaming을 추가하지 않는다.

### Phase 8 — 두 번째 Unity 프로젝트 재사용 검증

- **상태: 완료 — 자동 검증 및 Built-in/Legacy consumer 수동 Play Mode 통과, 기준 `ea4187b`**
- 상세 구현 계획: [`PHASE8_PLAN.md`](PHASE8_PLAN.md)
- 재사용 절차: [`REUSE_GUIDE.md`](REUSE_GUIDE.md)
- Built-in Render Pipeline과 Legacy Input Manager를 사용하는 별도 Unity 프로젝트의 alternate Assets 경로로 옮긴다.
- 경로와 Input System 고정 의존성을 제거하고 consumer-owned presentation driver로 Core 무변경 확장을 검증한다.
- UPM 구조 이동은 이 단계의 실제 의존성 증거를 확보한 뒤 Phase 9에서 수행한다.

### Phase 9 — UPM 패키지화

- 두 프로젝트에서 검증된 최소 의존성을 기준으로 Runtime, Editor, Tests, Samples를 패키지 구조로 이동한다.
- 설치·제거·업그레이드와 샘플 import를 검증한다.

### Phase 10 — Character Builder 도구

- 프로필과 계약이 안정된 뒤에만 생성·검증용 Editor UI를 만든다.
- 편의 기능이 런타임 구조나 캐릭터 스키마를 결정하지 않게 한다.

## 주요 위험과 통제

- `Personality`와 `SpeechStyle`은 요청에 포함되지만 Mock이 자연어 설명을 해석하지는 않는다. Phase 2에서는 필수 데이터로 검증하고 전달하되, 결정적 차이는 `DisplayName`, `ExampleDialogue`, `DefaultEmotion`으로 만들며 실제 의미 해석은 Phase 4에 둔다.
- `NpcConversationBehaviour`의 작은 mode 기반 composition은 Mock과 Backend를 구분한다. 구현 수가 늘어나기 전까지 DI 프레임워크나 별도 container는 추가하지 않는다.
- 현재 표현은 정적 색상·회전이다. 대상 3D 캐릭터와 Animator 규격이 정해진 후 별도 `INpcPresentationDriver`로 확장한다.
- 로컬 Backend에는 client auth와 rate limiting이 없다. `127.0.0.1` 밖으로 노출하지 않으며 원격 배포는 별도 보안 마일스톤으로 다룬다.
- 실제 OpenAI smoke test는 비용과 계정 quota를 사용하므로 자동 테스트에서는 SDK를 주입형 fake로 대체한다. Phase 4에서는 사용자가 승인한 수동 1회만 실행해 end-to-end 경로를 확인했다.
- Phase 7은 현재 Windows 장치에서 실제 microphone과 live transcription을 확인했다. 다른 OS의 권한·device 동작은 두 번째 프로젝트 또는 platform 검증에서 다시 확인한다.
- Phase 5 session은 서버 재시작·TTL·LRU eviction 시 의도적으로 사라진다. UTF-8 byte 예산은 token 예산이 아니며, 기억 품질은 수동 시나리오로 별도 확인한다.
- 패키지화를 먼저 하면 잘못된 경계를 고정할 수 있다. 반드시 두 번째 프로젝트 검증 뒤 진행한다.

## 바로 다음 행동

**현재 변경을 검토하고 Phase 8 체크포인트로 커밋한다.** Phase 9 UPM 패키지화나 Realtime 작업은 별도 계획과 승인 후 시작한다.
