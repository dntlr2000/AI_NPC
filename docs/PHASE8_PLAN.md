# Phase 8 — Second Unity Project Reuse Validation

> 상태: 완료 — 전체 자동 검증 및 Built-in/Legacy consumer 수동 Play Mode 통과
> 기준 커밋: `ea4187b` (Phase 7 체크포인트)
> 목표: 새 AI 기능 없이 현재 프레임워크를 다른 Unity 프로젝트와 다른 Assets 경로에서 재사용할 수 있음을 증명한다.

## 범위와 비범위

Phase 8은 기능 마일스톤이 아니라 portability hardening 단계다. 같은 Unity 버전의 별도 consumer 프로젝트에 kit를 `Assets/ThirdParty/AiCharacterKit`으로 옮기고, Built-in Render Pipeline과 Legacy Input Manager에서 컴파일·샘플 생성·테스트·Play Mode를 검증한다.

포함 범위:

- 설치 root와 테스트 fixture의 `Assets/AiCharacterKit` 고정 경로 제거
- Input System을 Editor/sample의 선택형 통합으로 전환
- 이동된 `.meta`와 GUID를 보존한 raw Assets import 검증
- consumer 전용 `INpcPresentationDriver` 구현으로 교체 가능성 확인
- Mock, V2 memory/reset, 선택형 TTS/STT의 두 번째 프로젝트 smoke test
- 최소 의존성과 재사용 절차 문서화

UPM 구조 이동, Unity 버전 매트릭스, Realtime, streaming, VAD, 장기 기억, 원격 Backend 배포, client authentication, Animator와 lip sync는 제외한다.

## 검증 환경

| 항목 | 원본 프로젝트 | Phase 8 consumer |
| --- | --- | --- |
| 위치 | `E:\Unity\AI_NPC` | `E:\CodexValidation\AICharacterKitPhase8Consumer` |
| Unity | `6000.5.3f1` | `6000.5.3f1` |
| Kit 위치 | `Assets/AiCharacterKit` | `Assets/ThirdParty/AiCharacterKit` |
| 렌더링 | URP 17.5.0 | Built-in Render Pipeline |
| UI 입력 | Input System 1.19.0 | Legacy Input Manager |
| 필수 package | uGUI 2.5.0, Test Framework 1.7.0 | uGUI 2.5.0, Test Framework 1.7.0 |

consumer 프로젝트는 검증 산출물이며 Git에 커밋하지 않는다. 원본 `.meta`를 보존하고 kit를 한 번만 복사한다. Backend 기능은 원본 저장소의 loopback server를 재사용한다.

## 구현 경계

```text
consumer CharacterProfile + text/voice input
        → existing NpcConversationBehaviour
        → existing NpcAIController / IAiConversationClient
        → Mock 또는 loopback Backend
        → consumer INpcPresentationDriver
```

- Core, Transport, Speech, Transcription 공개 API와 wire contract는 변경하지 않는다.
- `AiCharacterKitAssetPaths`가 Core asmdef를 기준으로 고유한 Assets 설치 root를 찾는다.
- `UiEventSystemFactory`가 활성 input backend에 맞는 uGUI module을 생성하거나 복구한다.
- Input System type은 reflection 경계에만 남기고 asmdef 필수 참조에서 제거한다.
- 중복 설치는 모호하게 선택하지 않고 명확한 오류로 거부한다.

## 구현 순서

1. Phase 7 체크포인트와 Phase 8 범위를 문서에 반영한다.
2. movable asset path resolver와 input-backend-neutral EventSystem factory를 추가한다.
3. sample builder와 EditMode fixture/scene 테스트의 고정 경로를 제거한다.
4. 원본 server build/test와 Unity compile/builder/EditMode 회귀를 실행한다.
5. E:에 최소 consumer 프로젝트를 만들고 tracked kit assets와 `.meta`를 alternate path로 복사한다.
6. consumer 전용 presentation driver와 Editor API scene builder를 추가한다.
7. consumer compile, sample repair, EditMode, Windows player build를 실행한다.
8. 실제로 발견된 이식성 문제만 수정하고 양쪽 프로젝트 검증을 반복한다.

모든 신규·수정 메서드에는 기능을 설명하는 간략한 주석을 둔다. `.unity`, `.prefab`, `.asset` YAML은 직접 작성하지 않는다.

## 자동 검증과 완료 기준

- [x] Server TypeScript build와 기존 Vitest 75/75 통과
- [x] 원본 Unity compile과 기존 105개를 포함한 전체 EditMode 110/110 통과
- [x] 원본 sample scene 6개의 생성·복구 회귀 통과
- [x] consumer가 URP와 Input System 없이 alternate path에서 컴파일
- [x] consumer에서 imported sample repair와 전체 EditMode 113/113 통과
- [x] consumer custom presentation Mock Play Mode 1/1 및 Windows player build 통과
- [x] 원본 `Packages/`, `ProjectSettings/`, Core/contract/server 구현 무변경
- [x] 수동 Play Mode에서 Mock, Legacy UI, custom presentation 확인

Loopback V2/TTS/STT live smoke test는 해당 구현이 바뀌지 않았고 기존 마일스톤에서 검증됐으므로 Phase 8의 필수 비용 발생 조건으로 두지 않는다. Imported assembly와 전체 회귀 테스트는 두 프로젝트에서 유지한다.

검증 로그와 결과는 `E:\CodexValidation`, TEMP/TMP는 `E:\CodexTemp`만 사용한다. 다른 OS의 microphone 권한과 다른 Unity 버전은 검증 범위가 아니며 완료 후에도 명시적 위험으로 남긴다.

## 현재 자동 검증 결과

- Server: 현재 소스 TypeScript build 및 Vitest 75/75 통과
- 원본: Unity compile, sample repair 6개, EditMode 110/110 통과
- Built-in/Legacy consumer: alternate path compile, sample repair, EditMode 113/113 통과
- Consumer runtime: 실제 InputField/Send Button → Mock → consumer presentation Play Mode 1/1 통과
- Consumer player: Windows development build 생성 완료
- Input System probe: 별도 action asset 없이 compile, sample repair, 전체 EditMode 110/110 통과
- Portability 회귀: 이동 경로, asmdef 선택 의존성, 생성·기존 EventSystem action 복구, sample 6개 입력 모듈 검증 통과. Resolver는 중복 설치를 명확한 오류로 거부한다.
- Source sync audit: consumer와 Input System probe의 비씬 Kit 파일 376개가 원본과 일치하고 원본 Assets GUID 중복 0건
- Boundary audit: 고정 `Assets/AiCharacterKit` 코드 경로, Runtime `UnityEditor`, 필수 `Unity.InputSystem` asmdef 참조 모두 0건

주요 결과 파일은 `Phase8PrimaryFinalEditMode.xml`, `Phase8ConsumerFinalEditMode.xml`, `Phase8ConsumerFinalPlayMode.xml`, `Phase8InputProbeFinalEditMode.xml`과 각 대응 log다. 모두 `E:\CodexValidation`에 있으며, TEMP/TMP는 `E:\CodexTemp`를 사용했다.

## 수동 검증 결과

2026-09-01에 Built-in/Legacy consumer의 `ConsumerSpriteMock.unity`에서 실제 입력 필드로 `hello`를 전송했다. Mock NPC가 `안녕하세요. 저는 Guide입니다...` 형태로 응답했고 consumer-owned Sprite가 제스처에 맞춰 기울었다. 최신 Editor 로그에는 예외, Missing Script, 컴파일 또는 shader 오류가 없었다.

## 수동 Play Mode 절차

1. 원본 `E:\Unity\AI_NPC` 프로젝트를 닫고 Unity Hub에 `E:\CodexValidation\AICharacterKitPhase8Consumer`를 추가한다.
2. `Assets/Phase8Consumer/Scenes/ConsumerSpriteMock.unity`를 열고 Play한다.
3. 입력 필드에 `hello`를 입력하고 **Send**를 누른다.
4. dialogue에 `Guide`, emotion에 `Happy`, gesture에 `Wave`, status에 `ready`가 표시되는지 확인한다.
5. Sprite 색상과 2D 회전이 바뀌며 버튼이 요청 중 잠시 비활성화됐다가 다시 활성화되는지 확인한다.
6. Console에 missing script, Input System package 오류 또는 URP shader 오류가 없는지 확인한다.

이 절차는 Legacy Input Manager의 실제 pointer/keyboard event와 Built-in 렌더링을 사람이 확인하기 위한 것이다. 대화·기억·TTS·STT의 provider 동작은 기존 단계에서 라이브 검증됐고 Phase 8 consumer에서는 동일 소스의 compile·EditMode 회귀를 확인한다. live voice 경로를 다시 확인할 경우 원본 loopback server를 실행하고 imported `VoiceInputNpcPrototype.unity`를 사용할 수 있지만 Phase 8 완료의 필수 비용 발생 조건으로 두지 않는다.
