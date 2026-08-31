# AI Character Kit Reuse Guide

Phase 8에서는 UPM 패키지화 전의 source portability를 검증하기 위해 raw Unity assets로 kit를 옮긴다. 정식 `package.json` 설치·제거·업그레이드는 Phase 9 범위다.

## 요구 사항

- Unity `6000.5.3f1`
- `com.unity.ugui` `2.5.0`
- 테스트를 가져올 때 `com.unity.test-framework` `1.7.0`
- Backend 기능을 사용할 때 원본 저장소의 Node.js 24 loopback server

URP와 Input System은 필수가 아니다. Core, Transport, Speech와 Transcription domain assembly는 `noEngineReferences: true`이며, 전체 Unity adapter를 가져오면 uGUI와 Unity 기본 audio, microphone, JSON, networking module을 사용한다.

## Assets로 가져오기

1. 대상 프로젝트를 닫는다.
2. `Assets/AiCharacterKit` 폴더 전체와 인접한 `Assets/AiCharacterKit.meta`를 함께 복사한다.
3. 대상에서는 원하는 Assets 하위 경로에 한 번만 배치한다. 예: `Assets/ThirdParty/AiCharacterKit`과 `Assets/ThirdParty/AiCharacterKit.meta`.
4. Unity를 열고 compile이 끝날 때까지 기다린다.
5. `Tools/AI Character Kit` 메뉴에서 필요한 sample builder를 실행한다. Builder는 현재 프로젝트의 Input System 또는 Legacy Input Manager에 맞춰 EventSystem을 복구하고, 끊긴 Input System action에는 self-contained 기본 action을 지정한다.

`.meta`를 누락하거나 같은 kit를 두 위치에 복사하지 않는다. GUID를 보존해야 profile·scene 참조가 유지되며 중복 설치는 editor automation이 거부한다.

## 최소 사용 흐름

1. `Create > AI Character Kit > Character Profile`로 profile을 만든다.
2. NPC GameObject에 `NpcConversationBehaviour`를 추가한다.
3. `INpcPresentationDriver`를 구현한 MonoBehaviour를 연결한다.
4. 오프라인 개발은 `NpcConversationMode.Mock`으로 시작한다.
5. Backend가 필요한 경우 loopback endpoint와 timeout을 Inspector에서 설정한다.
6. TTS와 STT는 각각 speech/voice input component를 선택적으로 조합한다.

게임별 Animator, Sprite, UI Toolkit 또는 3D presentation은 `INpcPresentationDriver` 구현에 둔다. 캐릭터 성격이나 말투를 MonoBehaviour에 하드코딩하지 않고 `CharacterProfile`에 저장한다.

## Backend와 보안

Mock에는 server가 필요 없다. V1/V2 대화, TTS와 STT는 `127.0.0.1`에 실행 중인 `server/`를 사용한다. API key는 server process environment에만 두고 Unity 프로젝트나 profile asset에 저장하지 않는다.

Phase 8 raw import는 원격 배포 계약이 아니다. loopback 밖의 사용에는 별도 authentication, transport security, rate limit 설계가 필요하다.
