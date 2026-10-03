# AI Character Kit 0.4.0 공개 릴리즈 기록

> 상태: 공개 완료 — 2026-09-04
> 대상: `com.aicharacterkit.framework` `0.4.0`
> 공개 exact commit: `027b61f053c620d4e5202830b18692a6d45d3458`
> 구현 기준선: `570dcb84a7e0126b3e5ca0de67e94a567b291e4d` (Phase 15 구현 및 사용자 수동 검증)

[GitHub Release](https://github.com/dntlr2000/AI_NPC/releases/tag/v0.4.0)의 발행 시각은 2026-09-04 02:09:56 UTC이며 draft/prerelease가 아니다. 이 기록은 확인한 공개 상태를 반영하며 별도의 승인 이력을 추정하지 않는다.

## 배포 결정

- 저장소와 UPM package 라이선스: MIT, copyright holder `dntlr2000`
- 배포 방식: 공개 GitHub Release와 immutable annotated tag `v0.4.0`
- UPM 설치 경계: `Packages/com.aicharacterkit.framework`
- package ID: `com.aicharacterkit.framework` 유지
- Registry, Asset Store, npm과 별도 package-only 저장소 배포는 제외
- `server/`는 같은 tag의 loopback reference source이며 package artifact로 배포하지 않음

공개 설치 URL:

```text
https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.4.0
```

## 공개 전 검증 기록

- [x] package version, author, MIT license와 문서 URL 검증
- [x] Runtime/Transport 경계, package dependency와 root manifest·lock 불변 감사
- [x] tracked source의 credential 및 generated output 정적 감사
- [x] Markdown 상대 링크, V4 JSON 예제·revision과 `v0.4.0` 설치 안내 검사
- [x] Server TypeScript build와 Vitest 95/95 회귀
- [x] Unity 6000.5.3f1 compile, sample import/repair/action/grounding 생성과 EditMode 186/186 회귀
- [x] Built-in/Legacy consumer에서 후보 Git commit의 package subfolder 설치와 lock hash 확인
- [x] consumer sample import/repair/action/grounding 생성, compile, EditMode 186/186 회귀와 Windows Development Player build
- [x] 사용자가 live V4 Grounded Guard 상태 반영, revision 갱신과 reset 시나리오 정상 동작 확인

검증 로그와 결과는 `E:\CodexValidation`, TEMP/TMP는 `E:\CodexTemp`에만 뒀다. 별도 consumer는 Built-in Render Pipeline, Legacy Input Manager이며 package lock의 Git hash가 기준선 전체 SHA와 일치했다. 실제 OpenAI 호출은 자동화하지 않으며, Backend 배포와 package registry 발행은 이 릴리즈에 포함하지 않는다.

## GitHub Release 기능 기록

AI Character Kit `0.4.0` adds bounded request-time character and world grounding to the reusable Unity 6 AI NPC framework.

Highlights:

- Character canon fields for background, goals, behavioral rules, and dialogue examples
- Reusable `NpcLoreProfile` assets for world lore and character beliefs
- Consumer-owned `INpcContextProvider` adapters for live game-state observations
- Immutable, bounded snapshots with deterministic priority trimming and content-derived revisions
- Context-grounded V4 session contract and matching loopback reference Backend
- Character Builder support for canon, lore, provider wiring, and grounding preview
- Importable Grounded Guard sample with observable gate/alarm state and revision diagnostics
- Compatibility with existing Mock, V1–V3, conversation actions, bounded sessions, TTS, and STT

Install with:

```text
https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.4.0
```

The UPM package works offline in Mock mode. The optional `server/` reference source is not installed or published as a package and must run separately for V4 semantic grounding and other Backend modes. Version `0.4.0` does not include persistent memory, RAG, remote Backend deployment, client authentication, Realtime voice, generic behavior variables or scores, or a bundled local model runtime.

## 공개 이후 안정화와 발행 경계

`v0.4.0` tag와 위 공개 기록은 보존한다. 2026-09-08에 발견한 V4 요청 크기 제한, context provider 호출 시점과 문서 정합성 보완은 `Unreleased`이며 이 공개 tag에 소급 적용되지 않는다. 구현·검증 상태는 [Phase 15 계획](PHASE15_PLAN.md)의 안정화 기록을 따른다. 현재 보완은 package/Unity version과 의존성을 변경하지 않으며 push, tag 생성·이동 또는 Release 발행을 포함하지 않는다. 후속 릴리즈는 검증된 exact commit에 대한 별도 승인이 필요하다.
