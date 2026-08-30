# Phase 6 — Reusable Optional TTS Pipeline

> 상태: 완료 — 자동 검증 및 실제 OpenAI TTS Play Mode 수동 검증 통과
> 기준 커밋: `1206654` (Phase 5 문서 체크포인트)
> 목표: 기존 텍스트·Mock·V1·V2 경로를 보존하면서 캐릭터 데이터로 선택 가능한 음성 출력을 추가한다.

## 범위와 비범위

Phase 6는 특정 캐릭터 음성을 하드코딩하지 않는다. Unity는 `NpcVoiceProfile.voicePresetId`만 보유하고 Backend가 JSON 프리셋을 OpenAI voice·instructions·speed로 해석한다. 새 응답의 정확한 `dialogue`를 합성하며 음성이 꺼졌거나 실패해도 텍스트·감정·제스처는 유지한다.

포함 범위:

- 순수 C# `AiCharacterKit.Speech`와 합성·재생 interface
- Speech V1 DTO·validator·mapper·Unity JSON codec
- `POST /v1/speech/synthesize`와 OpenAI Speech adapter
- 완전 버퍼 방식 PCM16LE 24 kHz mono, 최대 8 MiB
- 새 요청 교체, 취소, 음성 on/off, 정지, 안전한 실패 상태
- presentation decorator와 Editor 생성 2-NPC V2 speech sample

STT, Realtime, streaming, lip sync, audio cache, custom voice, 감정별 prosody, 자동 재시도와 원격 배포는 제외한다.

## 경계와 데이터 흐름

```text
AiNpcResponse.dialogue
  → SpeechAugmentedPresentationDriver
      ├─ 기존 INpcPresentationDriver (텍스트/감정/제스처 즉시 표시)
      └─ NpcSpeechOutput
          → NpcSpeechController
          → ISpeechSynthesisClient
          → Speech V1 JSON / loopback Backend
          → server voicePresetId resolver → OpenAI Speech (`pcm`)
          → fixed PCM bytes
          → ISpeechPlaybackDriver → transient Unity AudioClip
```

- `AiCharacterKit.Speech`는 UnityEngine·HTTP·OpenAI를 참조하지 않는다.
- Transport는 Speech domain만 참조하며 UnityEngine을 참조하지 않는다.
- JSON·HTTP·AudioClip은 각각 Unity transport, networking, optional speech 어셈블리에 둔다.
- OpenAI SDK와 실제 voice 설정은 `server/` 밖으로 나오지 않는다.
- 기존 `IAiConversationClient`, `NpcAIController`, V1/V2 contract는 변경하지 않는다.

## 동작 정책

- 새 대화 요청이 시작되면 이전 합성·재생을 중단한다.
- 실제 생성 응답만 읽는다. 초기 프로필 대사와 reset 후 초기 상태는 읽지 않는다.
- 합성 결과가 늦게 도착해도 최신 작업이 아니면 재생하지 않는다.
- speech busy는 Send/Reset busy와 독립적이며 텍스트 입력을 막지 않는다.
- 오류는 안전한 `NpcSpeechState.Failed`와 UI 상태로만 표시한다.
- 음성 UI에는 “이 음성은 AI로 생성됩니다.”를 항상 노출한다.

## 구현 순서

1. Speech domain과 wire contract를 추가한다.
2. Backend preset resolver, OpenAI adapter, endpoint와 fake 기반 테스트를 추가한다.
3. Unity loopback gateway, PCM playback, output composition과 decorator를 추가한다.
4. `PrototypeSceneBuilder`로 `SpeechNpcPrototype.unity`와 두 voice profile을 생성한다.
5. Server build/test, Unity compile/builder/EditMode, 실제 OpenAI Play Mode 순서로 검증한다.

## 검증과 완료 기준

- [x] Server TypeScript build 통과
- [x] Server Vitest 61/61 통과, 실제 OpenAI 호출 없음
- [x] Unity 6000.5.3f1 batchmode compile 통과
- [x] Speech scene builder 생성·복구 경로와 전체 EditMode 89/89 통과
- [x] Luna·Guard가 각 preset으로 발화하고 새 응답이 이전 음성을 교체함을 확인
- [x] 음성 off/stop, Backend 실패 시 텍스트 fallback, AI disclosure 확인
- [x] `Packages/`, `ProjectSettings/`, 기존 4개 sample scene 무변경 확인

이 문서 갱신과 Phase 6 구현 변경을 함께 담는 다음 커밋을 Phase 6 체크포인트로 사용한다.

자동 검증 로그와 결과는 프로젝트 밖의 `E:\CodexValidation`에 보관한다.

- Server build/test: TypeScript build 및 Vitest 61/61
- Unity compile: `Phase6Compile.log`
- Speech scene 생성: `Phase6SpeechSceneBuilder.log`
- Speech scene 복구 재실행: `Phase6SpeechSceneRepair.log`
- EditMode: `Phase6EditMode.log`, `Phase6TestResults.xml` — 89/89, 실패·건너뜀 0
- 복구 후 씬 참조 재검증: `Phase6SpeechSceneRepairEditMode.log`, `Phase6SpeechSceneRepairTestResults.xml` — 1/1

실제 OpenAI TTS 호출은 자동화하지 않았다. 수동 Play Mode에서 두 preset의 실제 음성 출력, 재생 교체·중지·off, Backend 실패 시 text fallback과 AI disclosure를 확인했다. 최초 확인 중 발생한 HTTP 403은 OpenAI 프로젝트의 모델 접근 권한 설정 문제였으며, 권한을 수정한 뒤 동일 경로가 정상 동작했다. API 키나 프로젝트 식별자는 저장소에 기록하지 않았다.

## 위험

- 완전 버퍼 방식은 첫 소리까지 지연과 메모리 사용이 streaming보다 크다.
- 모델·voice의 실제 품질과 quota는 자동 fake 테스트로 보장할 수 없다.
- PCM 전체를 메모리에 보관하므로 8 MiB 제한을 양쪽에서 강제해야 한다.
- AudioSource 기반 재생만 제공하며 캐릭터 mouth animation과 공간 음향 설계는 후속 범위다.
