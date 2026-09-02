# AI Character Kit 0.2.0 공개 릴리즈

> 상태: 공개 완료 — annotated tag와 GitHub Release 발행 완료
> 대상: `com.aicharacterkit.framework` `0.2.0`
> 릴리즈 커밋: `dcad604a510b767b18154eb01408218a2e1e51f2`

## 배포 결정

- 저장소 전체 라이선스: MIT, copyright holder `dntlr2000`
- 배포 방식: 공개 GitHub release와 annotated tag `v0.2.0`
- UPM 설치 경계: `Packages/com.aicharacterkit.framework`
- package ID: `com.aicharacterkit.framework` 유지
- Registry, Asset Store, npm과 별도 package-only 저장소 배포는 제외
- `server/`는 같은 tag의 loopback reference source이며 package artifact로 배포하지 않음

공개 릴리즈: <https://github.com/dntlr2000/AI_NPC/releases/tag/v0.2.0>

설치 URL:

```text
https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.2.0
```

## 공개 전 검증

- [x] package manifest version, author, license와 URL 검증
- [x] MIT license가 저장소와 UPM package에 포함되는지 검증
- [x] Runtime/Transport 경계와 package dependency 불변 감사
- [x] 현재 tree와 14개 Git revision의 secret 및 generated output 정적 감사
- [x] Server TypeScript build와 Vitest 75/75 회귀
- [x] Unity 6000.5.3f1 compile과 sample import/repair 후 EditMode 131/131 회귀
- [x] clean consumer에서 전체 commit SHA 기반 Git subfolder 설치
- [x] imported sample 6개 repair, consumer compile과 EditMode 131/131 회귀

검증 로그와 결과는 `E:\CodexValidation`, TEMP/TMP는 `E:\CodexTemp`에만 둔다. 실제 OpenAI 호출과 Backend package 생성은 이 릴리즈 검증에 포함하지 않는다.

검증 consumer는 Unity `6000.5.3f1`, Built-in Render Pipeline과 Legacy Input Manager 환경에서 package를 `source: git`으로 해석했다. 전체 commit hash는 consumer `packages-lock.json`의 `hash`와 일치했고, resolved package cache에 version `0.2.0`, author `dntlr2000`, MIT license file이 모두 포함됐다.

## GitHub Release 노트

AI Character Kit `0.2.0` is the first public Git-based UPM release of the reusable Unity 6 AI NPC framework.

Highlights:

- Deterministic network-free Mock conversation path
- Data-driven CharacterProfile authoring and Character Builder tooling
- Replaceable dialogue, presentation, TTS, and STT boundaries
- Versioned V1/V2 contracts and bounded process-local sessions
- Six importable prototype scenes and package EditMode coverage
- Built-in/Legacy consumer compatibility without required URP or Input System dependencies

Install with:

```text
https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.2.0
```

The optional `server/` reference implementation is not installed or published as a package. It remains loopback-only and must be run separately for Backend, memory, TTS, or STT modes. Remote deployment, persistent memory, Realtime voice, streaming, and client authentication are not included.

## 공개 결과

사용자가 커밋 `dcad604a510b767b18154eb01408218a2e1e51f2`의 공개를 최종 승인한 뒤 `main`을 push하고 annotated tag `v0.2.0`을 생성·push했다. GitHub Release `AI Character Kit 0.2.0`은 draft와 prerelease가 아닌 Latest 공개 릴리즈로 발행됐으며 로그아웃 상태에서도 저장소, tag, 커밋과 설치 URL을 확인했다.

공개된 tag는 이동하거나 덮어쓰지 않는다. `v0.2.0` 수정은 `0.2.1` 이상의 새 patch release로, 새 공개 기능은 별도 package version으로 배포한다.
