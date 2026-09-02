# AI Character Kit 0.3.0 릴리즈 준비

> 상태: 공개 릴리즈 후보 준비·검증 완료 — 최종 승인 및 발행 전
> 대상: `com.aicharacterkit.framework` `0.3.0`
> 기준선: `c38b5a0` (Phase 11 구현), `7cf0a63` (사용자 문서)

## 배포 결정

- 저장소와 UPM package 라이선스: MIT, copyright holder `dntlr2000`
- 배포 방식: 공개 GitHub Release와 annotated tag `v0.3.0`
- UPM 설치 경계: `Packages/com.aicharacterkit.framework`
- package ID: `com.aicharacterkit.framework` 유지
- Registry, Asset Store, npm과 별도 package-only 저장소 배포는 제외
- `server/`는 같은 tag의 loopback reference source이며 package artifact로 배포하지 않음

예정 설치 URL:

```text
https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.3.0
```

## 후보 검증

- [x] package version, author, MIT license와 문서 URL 검증
- [x] Runtime/Transport 경계, package dependency와 root manifest·lock 불변 감사
- [x] tracked source의 credential 및 generated output 정적 감사
- [x] Markdown 상대 링크와 `v0.3.0` 설치 안내 검사
- [x] Server TypeScript build와 Vitest 85/85 회귀
- [x] Unity 6000.5.3f1 compile, sample import/repair/action 생성과 EditMode 167/167 회귀
- [x] Built-in/Legacy consumer에서 후보 Git commit의 package subfolder 설치와 lock hash 확인
- [x] consumer sample import/repair/action 생성, compile, EditMode 167/167, PlayMode 2/2와 Windows Development Player build

검증 로그와 결과는 `E:\CodexValidation`, TEMP/TMP는 `E:\CodexTemp`에만 둔다. 실제 OpenAI 호출은 자동화하지 않으며, Phase 11의 Character Builder/Mock action, `CanExecute` 거부·허용과 live V3 semantic trigger는 별도 수동 Play Mode에서 이미 통과했다.

## GitHub Release 노트 초안

AI Character Kit `0.3.0` adds a reusable, data-driven conversation-action pipeline to the Unity 6 AI NPC framework.

Highlights:

- Natural-language trigger-to-action bindings authored in Character Builder
- Deterministic, network-free Mock matching for repeatable development and tests
- Consumer-owned `INpcActionHandler` boundary with final `CanExecute` authorization
- Deterministic selection of at most one action per successful conversation turn
- Action-aware V3 session contract and matching loopback reference Backend
- Importable action sample, end-to-end quick start, and expanded regression coverage
- Compatibility with existing action-free Mock, V1/V2 conversation, TTS, and STT paths

Install with:

```text
https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.3.0
```

The UPM package works offline in Mock mode. The optional `server/` reference source is not installed or published as a package and must be run separately for semantic V3 matching and other Backend modes. Version `0.3.0` does not include model-generated action parameters, Reflection calls, persistent behavior variables, generic rule trees, Backend deployment, remote authentication, or Realtime voice.

## 공개 승인 게이트

검증된 release-preparation commit을 만든 뒤 exact commit SHA, 테스트 결과와 이 초안을 검토한다. 사용자의 별도 최종 승인 전에는 `v0.3.0` tag를 만들거나 push하지 않고 GitHub Release를 발행하지 않는다. 공개 후 tag는 이동하지 않으며 수정은 `0.3.1` 이상의 새 version으로 배포한다.
