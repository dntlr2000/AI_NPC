# AI Character Kit 재사용 가이드

Phase 9부터 정식 재사용 경계는 raw `Assets` 복사가 아니라 UPM package `com.aicharacterkit.framework`다. package version `0.2.0`은 Unity `6000.5`, uGUI `2.5.0`을 기준으로 하며 URP와 Input System을 요구하지 않는다.

## 설치

Unity Package Manager에서 **+ > Install package from disk**를 선택하고 다음 파일을 지정한다.

```text
<AI_NPC 저장소>/Packages/com.aicharacterkit.framework/package.json
```

또는 consumer의 `Packages/manifest.json`에 local file dependency를 추가한다.

```json
"com.aicharacterkit.framework": "file:E:/path/to/AI_NPC/Packages/com.aicharacterkit.framework"
```

기존 `Assets/AiCharacterKit` raw 복사본과 UPM package를 동시에 두지 않는다. 같은 assembly와 GUID가 이중 로드될 수 있으며 Editor resolver도 이를 중복 설치 오류로 처리한다.

## 샘플 가져오기

1. Package Manager에서 **AI Character Kit**을 선택한다.
2. Samples 탭의 **AI NPC Prototypes**를 Import한다.
3. **Tools > AI Character Kit > Repair All Sample Scenes**를 실행한다.
4. `Assets/Samples/AI Character Kit/0.2.0/AI NPC Prototypes` 아래의 원하는 scene을 연다.

**Tools > AI Character Kit > Import or Repair AI NPC Prototypes** 메뉴를 사용하면 import와 repair를 한 번에 수행할 수 있다. Sample repair 도구는 imported sample이 없을 때 `Assets/AI Character Kit/Samples`에 새 sample을 생성한다. package의 `Samples~`나 package cache에는 쓰지 않는다. Input System이 설치돼 있으면 해당 uGUI module을 reflection으로 구성하고, 없으면 Legacy Input Manager module을 사용한다.

## 최소 Mock 구성

1. **Tools > AI Character Kit > Character Builder**를 연다.
2. consumer-owned CharacterProfile을 `Assets` 아래에 만들고 Mock preview를 확인한다.
3. 기존 Scene GameObject 또는 writable Prefab과 `INpcPresentationDriver` 구현체를 선택한다.
4. 선택형 기존 uGUI View를 연결하고 mode를 `Mock`으로 적용한다.
5. Play Mode에서 network 없이 입력·응답·감정·제스처를 확인한다.

Builder는 모델, UI, presentation 구현이나 prefab을 생성하지 않는다. 기존 Inspector 수동 구성도 계속 지원하며, optional TTS에는 Backend가 소유하는 opaque voice preset ID만 사용한다.

게임별 Animator, Sprite, UI Toolkit 또는 3D 표현은 consumer의 `INpcPresentationDriver` 구현에 둔다. 캐릭터 성격과 말투는 MonoBehaviour가 아니라 `CharacterProfile` asset으로 관리한다.

## Backend와 선택형 음성

V1/V2 대화, TTS와 STT는 compatible loopback backend가 필요하다. 이 저장소의 `server/`는 reference implementation이지만 UPM package에 포함되지 않는다. API key와 provider SDK는 server process에만 두며 Unity asset이나 source에 저장하지 않는다.

## 제거와 업그레이드

Package 제거는 package source/assembly만 제거한다. imported sample, consumer profile과 presentation driver는 `Assets` 소유이므로 자동 삭제하지 않는다. version 교체 후 새 sample을 import한다. 구버전과 현재 version sample 폴더가 공존하면 automation은 현재 설치 version을 선택하며, 구버전 consumer-owned copy는 더 필요하지 않을 때만 직접 정리한다. 재설치나 version 교체 후 Unity compile, sample repair와 프로젝트 테스트를 수행한다.

raw Phase 8 설치에서 migration할 때는 Unity를 닫고 raw kit 폴더를 별도 백업한 뒤 제거하고 UPM package를 설치한다. raw 설치는 dependency metadata가 없으므로 전환 전까지 consumer manifest가 uGUI `2.5.0`을 직접 제공해야 한다. consumer가 만든 asset을 raw kit 폴더 내부에 보관했다면 먼저 consumer-owned 폴더로 옮긴다. 컴파일과 참조를 확인하기 전까지 백업을 삭제하지 않는다.
