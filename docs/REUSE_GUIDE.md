# AI Character Kit 재사용 가이드

Phase 9부터 정식 재사용 경계는 raw `Assets` 복사가 아니라 UPM package `com.aicharacterkit.framework`다. `v0.3.0`은 Phase 11의 선택형 대화 행동 기능을 포함하며 Unity `6000.5`, uGUI `2.5.0`을 기준으로 한다. URP와 Input System은 요구하지 않는다.

## 설치

공개 version은 Unity Package Manager에서 **+ > Install package from git URL**을 선택하고 tag가 고정된 다음 URL을 입력한다.

```text
https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.3.0
```

또는 consumer의 `Packages/manifest.json`에 같은 Git dependency를 추가한다.

```json
"com.aicharacterkit.framework": "https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.3.0"
```

저장소의 package 하위 경로를 지정하는 `?path=`가 revision `#v0.3.0`보다 앞에 와야 한다. 기본 branch 대신 불변 release tag를 고정하고, upgrade할 때만 새 tag로 바꾼다.

로컬 개발에서는 **Install package from disk**를 선택하고 다음 파일을 지정한다.

```text
<AI_NPC 저장소>/Packages/com.aicharacterkit.framework/package.json
```

또는 local file dependency를 추가한다.

```json
"com.aicharacterkit.framework": "file:E:/path/to/AI_NPC/Packages/com.aicharacterkit.framework"
```

기존 `Assets/AiCharacterKit` raw 복사본과 UPM package를 동시에 두지 않는다. 같은 assembly와 GUID가 이중 로드될 수 있으며 Editor resolver도 이를 중복 설치 오류로 처리한다.

## 샘플 가져오기

1. Package Manager에서 **AI Character Kit**을 선택한다.
2. Samples 탭의 **AI NPC Prototypes**를 Import한다.
3. **Tools > AI Character Kit > Repair All Sample Scenes**를 실행한다.
4. `Assets/Samples/AI Character Kit/<설치 버전>/AI NPC Prototypes` 아래의 원하는 scene을 연다.

Version `0.3.0`에서는 import 뒤 **Tools > AI Character Kit > Samples > Create Conversation Action Prototype**을 실행하면 Editor API가 network-free action sample Scene을 생성한다. Package의 `.unity` YAML을 직접 수정하지 않는다.

**Tools > AI Character Kit > Import or Repair AI NPC Prototypes** 메뉴를 사용하면 import와 repair를 한 번에 수행할 수 있다. Sample repair 도구는 imported sample이 없을 때 `Assets/AI Character Kit/Samples`에 새 sample을 생성한다. package의 `Samples~`나 package cache에는 쓰지 않는다. Input System이 설치돼 있으면 해당 uGUI module을 reflection으로 구성하고, 없으면 Legacy Input Manager module을 사용한다.

## 최소 Mock 구성

1. **Tools > AI Character Kit > Character Builder**를 연다.
2. consumer-owned CharacterProfile을 `Assets` 아래에 만들고 Mock preview를 확인한다.
3. 기존 Scene GameObject 또는 writable Prefab과 `INpcPresentationDriver` 구현체를 선택한다.
4. 선택형 기존 uGUI View를 연결하고 mode를 `Mock`으로 적용한다.
5. Play Mode에서 network 없이 입력·응답·감정·제스처를 확인한다.

Builder는 모델, UI, presentation 구현, action handler나 prefab을 생성하지 않는다. 기존 Inspector 수동 구성도 계속 지원하며, optional TTS에는 Backend가 소유하는 opaque voice preset ID만 사용한다.

게임별 Animator, Sprite, UI Toolkit 또는 3D 표현은 consumer의 `INpcPresentationDriver` 구현에 둔다. 캐릭터 성격과 말투는 MonoBehaviour가 아니라 `CharacterProfile` asset으로 관리한다.

## 선택형 대화 행동 구성

처음 설치한 사용자를 위한 컴파일 가능한 handler 예제와 Character Builder 전체 절차는 package의 [Conversation Actions Quick Start](../Packages/com.aicharacterkit.framework/Documentation~/ACTIONS_QUICKSTART.md)를 따른다.

1. `NpcActionHandlerBase`를 상속하거나 `INpcActionHandler`를 구현한 consumer MonoBehaviour를 작성한다.
2. `ActionId`는 예를 들어 `open_gate` 같은 안정적인 lower `snake_case` 값으로 정의한다.
3. Character Builder의 **Conversation Actions**에서 `NpcActionProfile`을 만들고 trigger ID, 자연어 조건, Mock 예시, action ID와 priority를 입력한다.
4. NPC 대상에서 각 action ID를 제공하는 handler를 선택한다.
5. Mock mode에서 예시 입력과 선택 결과를 확인한 뒤, 의미 기반 판정이 필요할 때만 Backend Actions V3 mode를 사용한다.

한 turn에서는 높은 priority와 profile 선언 순서에 따라 최대 한 행동만 선택된다. Backend는 configured trigger ID만 반환하며 실제 action ID와 Scene 참조는 받지 않는다. `CanExecute`에서 거리, 인벤토리, 퀘스트 상태와 대상 유효성 같은 최종 게임 권한을 다시 검사한다. 행동 실패나 거부는 이미 표시된 정상 대화를 실패로 바꾸지 않는다.

Phase 11 수동 확인은 imported `ActionNpcPrototype.unity` 또는 consumer-owned 구성에서 수행한다.

1. Mock mode에서 `hello`를 보내 `greet_player`가 `wave_to_player`를 한 번만 실행하는지 확인한다.
2. `open the gate`를 보내 대화는 정상 표시되면서 locked `CanExecute`가 행동만 거부하는지 확인한다.
3. **Gate Unlocked**를 켜고 다시 보내 gate indicator가 사라지는지 확인한다.
4. Character Builder로 동일 Scene/Prefab에 재적용하고 coordinator/handler가 중복되지 않는지 확인한다.
5. matching local Backend를 실행하고 mode를 **Backend Actions**로 바꾼 뒤, Mock 예시와 문장이 다르지만 같은 의미인 입력으로 V3 semantic trigger를 확인한다.
6. Backend가 없거나 허용된 OpenAI key/model을 사용할 수 없다면 1~4만 검증했다고 기록하고 live V3 성공을 주장하지 않는다.

## Backend와 선택형 음성

V1/V2/V3 대화, TTS와 STT는 compatible loopback backend가 필요하다. 이 저장소의 `server/`는 같은 Git revision에 포함되는 reference source지만 UPM package나 npm package로 배포되지 않는다. 필요한 사용자는 package와 matching revision을 별도로 checkout하고 `server/README.md`에 따라 실행한다. API key와 provider SDK는 server process에만 두며 Unity asset이나 source에 저장하지 않는다.

## 제거와 업그레이드

Package 제거는 package source/assembly만 제거한다. imported sample, consumer profile과 presentation driver는 `Assets` 소유이므로 자동 삭제하지 않는다. version 교체 후 새 sample을 import한다. 구버전과 현재 version sample 폴더가 공존하면 automation은 현재 설치 version을 선택하며, 구버전 consumer-owned copy는 더 필요하지 않을 때만 직접 정리한다. 재설치나 version 교체 후 Unity compile, sample repair와 프로젝트 테스트를 수행한다.

raw Phase 8 설치에서 migration할 때는 Unity를 닫고 raw kit 폴더를 별도 백업한 뒤 제거하고 UPM package를 설치한다. raw 설치는 dependency metadata가 없으므로 전환 전까지 consumer manifest가 uGUI `2.5.0`을 직접 제공해야 한다. consumer가 만든 asset을 raw kit 폴더 내부에 보관했다면 먼저 consumer-owned 폴더로 옮긴다. 컴파일과 참조를 확인하기 전까지 백업을 삭제하지 않는다.
