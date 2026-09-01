# Phase 9 — UPM 패키지화

**상태: 완료 — 자동 검증과 별도 Built-in/Legacy consumer 검증을 통과하고 `main` 체크포인트 `cd5825b`로 종료했다.**

## 목표와 기준선

Phase 8 체크포인트 `707123b`에서 검증한 동일 소스를 Unity Package Manager가 설치·제거·업그레이드할 수 있는 재사용 단위로 만든다. 패키지 ID는 `com.aicharacterkit.framework`, 첫 버전은 `0.1.0`, 지원 기준은 Unity `6000.5`다. 공개 C# API, asmdef 이름, namespace, wire contract와 기존 `.meta` GUID는 변경하지 않는다.

이 단계는 로컬 disk/embedded UPM 배포까지만 다룬다. 외부 registry 공개, Realtime, 장기 기억, Animator presentation, Character Builder와 backend 원격 배포는 제외한다.

## 패키지 구조

```text
Packages/com.aicharacterkit.framework/
├─ package.json
├─ README.md
├─ CHANGELOG.md
├─ Runtime/
├─ Editor/
├─ Tests/Editor/
├─ Samples~/AI NPC Prototypes/
└─ Documentation~/
```

- Runtime과 Editor assembly는 Phase 8과 같은 이름과 참조를 유지한다.
- Test Framework는 production dependency가 아니다. 테스트 실행 프로젝트가 `testables`와 Test Framework를 제공한다.
- production dependency는 실제 사용 근거가 있는 uGUI, audio, JsonUtility, UnityWebRequest뿐이다.
- URP와 Input System은 선택 사항이며 패키지 의존성에 넣지 않는다.
- 여섯 prototype scene과 공유 profile은 참조 보존을 위해 하나의 sample로 import한다.
- Node/OpenAI reference backend는 Unity package 밖의 저장소 `server/`에 유지한다.

## 경로와 샘플 정책

`AiCharacterKitAssetPaths`는 `PackageInfo.FindForAssembly`로 설치된 package root를 찾고, raw Assets 복사본이 동시에 있으면 중복 설치 오류를 반환한다. package root는 읽기 전용으로 취급한다.

`AiCharacterKitSamplePaths`는 `Assets` 아래의 imported/generated sample 하나만 선택한다. Package Manager import가 없으면 builder는 `Assets/AI Character Kit/Samples`에 생성한다. **Tools > AI Character Kit > Repair All Sample Scenes**는 현재 입력 backend에 맞춰 profile, scene, EventSystem 참조를 복구한다.

## 구현 순서

1. package metadata, 사용 문서와 dependency 목록을 추가한다.
2. Runtime, Editor와 Tests를 GUID 보존 상태로 package root로 이동한다.
3. 기존 sample 전체를 `Samples~/AI NPC Prototypes`로 이동한다.
4. fixture는 package-relative, scene/profile은 writable sample-relative 경로로 분리한다.
5. 원본 embedded package와 별도 Built-in/Legacy consumer에서 컴파일과 전체 회귀를 실행한다.
6. consumer에서 sample import/repair, Mock Play Mode와 Windows player build를 확인한다.
7. install/remove/reinstall, `0.0.0` validation copy에서 `0.1.0`으로 upgrade, raw Assets에서 UPM으로 migration을 검증한다.
8. GUID, dependency, Runtime/Editor, 고정 경로, credential 경계를 정적 감사하고 문서를 완료 상태로 갱신한다.

## 자동 검증

- [x] `package.json` parse와 package identity/dependency/sample metadata 확인
- [x] 기존 215개 kit asset GUID 보존 및 package 내 중복 GUID 0개
- [x] Runtime `UnityEditor`, Core/Transport UnityEngine, 필수 URP/Input System 참조 0개
- [x] Server `npm.cmd run build`와 Vitest 75/75 회귀
- [x] 원본 Unity 6000.5.3f1 embedded package compile
- [x] sample import/repair와 package EditMode 112/112 통과
- [x] Built-in/Legacy consumer file install, sample import, Mock Play Mode, Windows build 통과
- [x] Input System 활성 원본 프로젝트 compile과 sample 회귀
- [x] remove/reinstall/upgrade/raw migration 후 compile 및 consumer-owned asset 보존

검증 결과·로그는 `E:\CodexValidation`, TEMP/TMP는 `E:\CodexTemp`만 사용한다. Unity가 실제 실행되지 않았거나 라이선스 단계에서 중단되면 컴파일 성공으로 기록하지 않는다.

## 완료 기준

- 소비자는 raw source 복사 없이 한 package dependency로 Runtime과 Editor 기능을 사용할 수 있다.
- sample은 명시적으로 import되며 package 제거와 별개인 consumer-owned Assets가 된다.
- 설치 위치가 달라도 Editor automation, fixture와 scene 검사가 고정 경로 없이 동작한다.
- 기존 Mock/V1/V2/TTS/STT 계약과 Backend 회귀가 유지된다.
- 제거·재설치·업그레이드가 사용자 profile, presentation driver와 imported sample을 삭제하지 않는다.
- 문서가 설치, sample, migration, backend 분리와 알려진 제한을 정확히 설명한다.

## 예상 위험과 통제

- raw와 UPM 이중 설치는 동일 assembly/GUID 충돌을 만들 수 있으므로 자동 선택하지 않고 명확히 거부한다.
- `Samples~`는 직접 편집 대상이 아니므로 모든 builder 출력은 `Assets`로 제한한다.
- sample scene끼리 profile을 공유하므로 sample을 임의로 분할하지 않는다.
- file dependency는 source package를 직접 참조한다. 업그레이드 검증은 E:의 별도 package copy를 사용해 원본을 변경하지 않는다.
- reference backend는 UPM 제거 대상이 아니며 package version과 server deployment lifecycle은 독립적이다.

## 검증 결과

- 원본 embedded package: compile, UPM sample import/repair 6개, EditMode **112/112**, 정리 후 final compile 통과
- Built-in/Legacy consumer: 최초 file install compile, imported sample repair, EditMode **112/112**, consumer presentation PlayMode **1/1**, Windows development player build 통과
- 제거/재설치: 제거 후 lock에서 package가 사라졌고 imported sample 37개 파일의 집계 SHA-256이 유지됐다. 재설치 후 EditMode **112/112** 통과
- 업그레이드: Package Manager API가 validation copy `0.0.0`과 repository `0.1.0`을 각각 확인했고 교체 후 EditMode **112/112** 통과
- sample version 선택: `0.0.0`과 `0.1.0` marker가 공존할 때 현재 `0.1.0` root를 선택해 6개 scene repair와 EditMode **112/112** 통과
- raw migration: raw 방식에 필요한 직접 uGUI `2.5.0` dependency를 둔 alternate Assets 설치와 PlayMode **1/1** 통과 후 UPM으로 전환했다. 전환 후 EditMode **112/112**, PlayMode **1/1**이 통과했고 consumer-owned 55개 파일의 집계 SHA-256이 동일했다.
- 경계: package+raw 이중 marker를 명확히 거부했고 Runtime `UnityEditor`, 직접 URP/Input System asmdef 참조, legacy 고정 코드 경로와 package GUID 중복은 모두 0건이다.
- Backend: TypeScript build와 Vitest **75/75** 통과

주요 결과는 `E:\CodexValidation\Phase9EmbeddedFinalEditMode.xml`, `Phase9ConsumerEditMode.xml`, `Phase9ConsumerPlayMode.xml`, `Phase9ConsumerReinstallEditMode.xml`, `Phase9ConsumerUpgrade010EditMode.xml`, `Phase9CurrentSamplePreferenceEditMode.xml`, `Phase9MigrationUpmEditMode.xml`, `Phase9MigrationUpmPlayMode.xml`과 대응 log에 있다. Windows player는 `E:\CodexValidation\AICharacterKitPhase9ConsumerBuild`에 생성됐다.
