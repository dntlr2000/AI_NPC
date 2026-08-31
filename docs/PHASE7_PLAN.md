# Phase 7 — Reusable Push-to-Talk STT Input Pipeline

> 상태: 완료 — 구현·자동 검증 및 실제 microphone/OpenAI Play Mode 검증 통과
> 기준 커밋: `0501e6d` (Phase 6 체크포인트)
> 목표: 기존 텍스트 입력·Mock·V1·V2·기억·TTS를 보존하면서 검토 가능한 음성 입력을 추가한다.

## 범위와 비범위

Phase 7은 특정 캐릭터용 음성 기능이 아니라 교체 가능한 입력 adapter다. 사용자가 버튼을 누르는 동안 최대 15초를 녹음하고, 전사 결과를 기존 입력 필드에 채운다. **자동 전송하지 않으며** 사용자가 수정·확인한 뒤 기존 Send를 누른다.

포함 범위:

- 순수 C# `AiCharacterKit.Transcription` controller와 capture/transcription interface
- canonical PCM16 mono WAV encoder와 Transcription V1 계약
- `POST /v1/speech/transcribe` loopback endpoint와 OpenAI file transcription adapter
- Unity `Microphone` capture, 취소·중복 방지·안전한 실패 상태
- Push-to-Talk/취소/상태/privacy disclosure UI
- 녹음 시작 시 선택형 TTS 정지와 Editor 생성 1-NPC V2 sample

Realtime, streaming, VAD, barge-in, 자동 전송, language/prompt hints, 녹음 저장, STT retry, 장기 기억과 원격 배포는 제외한다.

## 경계와 데이터 흐름

```text
Push-to-Talk hold
  → IAudioCaptureDriver → canonical PCM16 mono WAV
  → VoiceInputController → ITranscriptionClient
  → Transcription V1 raw WAV / loopback Backend
  → OpenAI file transcription
  → validated JSON transcript
  → NpcTextInputView.SetInputText
  → 사용자 검토·수정 → 기존 Send/V2 conversation/TTS
```

- `AiCharacterKit.Transcription`은 UnityEngine·HTTP·OpenAI를 참조하지 않는다.
- WAV/response DTO validation은 Unity 비의존 Transport에 둔다.
- `Microphone`, `JsonUtility`, `UnityWebRequest`, uGUI는 Unity 경계에만 둔다.
- OpenAI SDK와 API key는 `server/` 밖으로 나오지 않는다.
- 기존 `IAiConversationClient`, `NpcAIController`, Speech V1과 대화 V1/V2 계약은 변경하지 않는다.

## 고정 제한과 정책

- WAV: canonical 44-byte header, PCM16, mono, 8–48 kHz, 15초 이하, 2 MiB 이하
- transcript: 공백이 아닌 4,096자·UTF-8 8 KiB 이하
- request ID: `transcription-<32 hex GUID>`, wire에서는 opaque 최대 128자
- OpenAI 기본 모델: `gpt-transcribe`; timeout 30초; SDK retry 0
- 오디오·전사문·사용자/NPC 데이터는 로그나 디스크에 저장하지 않는다.
- 전사 취소·실패는 기존 입력 텍스트와 대화 상태를 변경하지 않는다.

완성된 녹음 파일은 OpenAI의 [Speech-to-text file transcription](https://developers.openai.com/api/docs/guides/speech-to-text) 경로로 처리한다. 기본 모델의 기능·접근 조건은 [GPT Transcribe model](https://developers.openai.com/api/docs/models/gpt-transcribe)을 기준으로 하며, provider 한도보다 엄격한 local 15초·2 MiB 제한을 적용한다.

## 구현 순서

1. Transcription domain, WAV encoder, V1 response 계약을 추가한다.
2. Backend WAV parser·validator, OpenAI adapter와 route를 추가한다.
3. Unity microphone driver, loopback gateway, input composition과 UI를 추가한다.
4. `PrototypeSceneBuilder`로 `VoiceInputNpcPrototype.unity`를 생성·복구한다.
5. Server build/test, Unity compile/builder/EditMode, 실제 microphone Play Mode 순서로 검증한다.

## 테스트와 완료 기준

- [x] Server TypeScript build 통과
- [x] Server Vitest 75/75 통과, 실제 OpenAI 호출 없음
- [x] Unity 6000.5.3f1 batchmode compile 통과
- [x] Voice Input scene 생성·복구와 전체 EditMode **105/105** 통과
- [x] 한국어 음성이 입력 필드에만 채워지고 자동 전송되지 않음 확인
- [x] 전사문 수정 후 V2 대화·기억·TTS가 기존처럼 동작함 확인
- [x] 녹음/전사 취소, 마이크·Backend 실패, 녹음 시작 시 TTS 정지 확인
- [x] privacy disclosure와 `Packages/`, `ProjectSettings/`, 기존 sample 무변경 확인

Unity 자동 검증은 유효한 Editor license가 있는 환경에서 완료했다. 실제 OpenAI STT 호출은 자동화하지 않고 2026-08-31 사용자 Play Mode 검증으로 확인했다. 검증 로그와 결과는 `E:\CodexValidation`, TEMP/TMP는 `E:\CodexTemp`에만 둔다.

자동 검증에서는 Server TypeScript build, Vitest **75/75**, Unity compile, scene 생성·복구 멱등성, EditMode **105/105**가 통과했다. 사용자 수동 검증에서는 실제 microphone, OpenAI transcription, 검토 후 전송, 기존 V2 기억·TTS, 취소·실패와 disclosure 동작이 모두 정상임을 확인했다.

## 수동 Play Mode 절차

1. `server/`에서 `OPENAI_API_KEY`를 process environment로 설정하고 `npm.cmd run dev`를 실행한다. 필요할 때만 `OPENAI_TRANSCRIPTION_MODEL`을 계정이 접근 가능한 transcription model로 덮어쓴다.
2. Unity에서 `Assets/AiCharacterKit/Samples/VoiceInputNpc/Scenes/VoiceInputNpcPrototype.unity`를 열고 Play한다. OS microphone 권한 요청이 있으면 허용한다.
3. **누르는 동안 말하기**를 1~3초 누른 채 한국어로 말하고 놓는다. 상태가 녹음 중 → 전사 중 → 준비로 바뀌고, 전사문이 입력 필드에 채워지되 자동 전송되지 않는지 확인한다.
4. 전사문을 수정한 뒤 **전송**한다. 기존 V2 응답·단기 기억·TTS가 정상이며 새 녹음 시작 시 재생 중 TTS가 멈추는지 확인한다.
5. 녹음 중 `Esc`, 전사 중 **취소**를 각각 사용한다. 기존 입력문과 대화 상태가 바뀌지 않고 다시 녹음할 수 있는지 확인한다.
6. Backend를 끄거나 microphone 권한을 거부해 안전한 실패 상태와 기존 텍스트 보존을 확인한 뒤 설정을 복구한다.

## 위험

- OS microphone 권한과 device availability는 platform별로 다르다.
- 완전 녹음 후 업로드하므로 첫 transcript까지 녹음 길이+network 지연이 발생한다.
- 자동 언어 감지는 짧거나 잡음이 큰 발화에서 부정확할 수 있다.
- 단일 sample은 한 microphone owner만 가정한다. 다중 NPC 입력 target routing은 실제 요구가 생길 때 별도 설계한다.
