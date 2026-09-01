# Phase 10 — Character Builder Editor 도구

> 상태: 완료 — 구현·자동 검증·consumer 수동 검증 통과
> 기준선: `071fbc3` (Phase 9 문서 체크포인트)
> package: `com.aicharacterkit.framework` `0.2.0`

## 목표와 경계

기존 Runtime 계약을 바꾸지 않고 Unity 사용자가 캐릭터 profile을 작성하고 기존 Scene 또는 Prefab NPC를 구성할 수 있는 Editor 도구를 제공한다. Character Builder는 consumer가 만든 모델, UI와 `INpcPresentationDriver`를 연결할 뿐 이를 생성하거나 소유하지 않는다.

포함 범위:

- CharacterProfile 및 opaque NpcVoiceProfile 생성·편집
- zero-latency 결정적 Mock 미리보기
- loaded Scene GameObject와 writable Regular/Variant Prefab 구성
- Mock, Backend V1, BackendSession V2와 loopback endpoint 검증
- 선택형 기존 uGUI View 및 TTS 구성
- Undo, prefab contents 격리 저장, 재실행 멱등성

STT 구성, UI·모델·presentation 구현·prefab 생성, Animator, Realtime, 장기 기억, remote endpoint와 registry publishing은 제외한다.

## 사용자 흐름

1. **Tools > AI Character Kit > Character Builder**를 연다.
2. profile draft를 작성해 `Assets/` 아래에 저장하거나 기존 consumer profile을 불러온다.
3. 사용자 텍스트로 network-free Mock 응답의 대사·감정·제스처를 확인한다.
4. Scene GameObject 또는 Prefab root와 기존 visual presentation driver를 선택한다.
5. mode, loopback endpoint, timeout과 선택형 View/TTS를 지정한다.
6. preflight 결과를 확인하고 **Apply to Target**을 실행한다.

Builder는 둘 이상의 동일 Kit component, incomplete View, prefab 외부 참조, Model/package Prefab, invalid profile/endpoint를 적용 전에 거부한다. 동일 characterId는 import version이나 의도적 identity reuse가 가능하므로 warning으로만 표시한다.

## 비파괴 적용 정책

- Scene은 하나의 Undo group으로 처리하고 실패 시 전체 적용을 되돌린다.
- Prefab은 `PrefabUtility.LoadPrefabContents`에서 구성·검증한 뒤 성공한 경우에만 저장한다.
- 재실행은 기존 단일 component를 재사용하며 사용자 component나 asset을 삭제하지 않는다.
- TTS는 전용 AudioSource를 사용한다. TTS를 끄면 visual driver를 다시 연결하지만 기존 speech component는 보존한다.
- Scene prefab instance 변경은 override로 남기고 원본에 자동 apply하지 않는다.
- package root에는 아무것도 쓰지 않으며 profile과 voice asset은 user-selected `Assets/` 폴더에만 생성한다.

## 검증 계획

- Profile/voice create·update·validation, safe unique path, duplicate ID warning
- deterministic Mock preview와 invalid input 실패
- Scene apply/reapply/Undo와 invalid preflight 원자성
- V1/V2 endpoint 및 timeout serialization
- optional text/session/speech View wiring
- dedicated AudioSource, TTS decorator/output/playback와 non-destructive disable
- Regular/Variant Prefab save/reload, external reference 및 read-only target 거부
- 기존 EditMode 112개, Server 75개, sample scene 6개 전체 회귀
- 별도 Built-in/Legacy consumer의 `0.1.0 → 0.2.0` upgrade, compile, Mock Play Mode와 Windows player build

검증 결과·로그는 `E:\CodexValidation`, TEMP/TMP는 `E:\CodexTemp`에만 둔다. 자동 검증과 consumer 수동 Mock/TTS 확인을 모두 통과한 뒤에만 Phase 10을 완료로 전환한다.

## 자동 검증 결과

- Server TypeScript build 및 Vitest **75/75** 통과
- Unity 6000.5.3f1 package import·compile 및 root EditMode **131/131** 통과
- package `0.2.0` sample import/repair와 여섯 scene 회귀 통과
- 별도 Built-in/Legacy consumer에서 `0.1.0 → 0.2.0` 해석, compile 및 EditMode **131/131** 통과
- 같은 consumer에서 consumer-owned Mock presentation PlayMode **1/1** 및 Windows Development player build 통과
- Runtime/Transport 경계, package dependency 불변, package root 비기록, secret/provider 정보 정적 감사 통과

## 수동 검증 결과

사용자가 Built-in/Legacy consumer에서 Builder UI, profile 생성, Scene/Prefab 적용과 재적용, Mock Play Mode 및 선택형 TTS를 포함한 수동 체크리스트의 정상 동작을 확인했다. 자동·수동 완료 조건이 모두 충족됐으며 Phase 10은 완료 상태다.
