# AI Character Kit 0.4.0 릴리즈 준비

> 상태: 공개 릴리즈 후보 준비·검증 완료 — 최종 승인 및 발행 전
> 대상: `com.aicharacterkit.framework` `0.4.0`
> 기준선: `570dcb84a7e0126b3e5ca0de67e94a567b291e4d` (Phase 15 구현 및 사용자 수동 검증)

## 배포 결정

- 저장소와 UPM package 라이선스: MIT, copyright holder `dntlr2000`
- 배포 방식: 공개 GitHub Release와 immutable annotated tag `v0.4.0`
- UPM 설치 경계: `Packages/com.aicharacterkit.framework`
- package ID: `com.aicharacterkit.framework` 유지
- Registry, Asset Store, npm과 별도 package-only 저장소 배포는 제외
- `server/`는 같은 tag의 loopback reference source이며 package artifact로 배포하지 않음

예정 설치 URL:

```text
https://github.com/dntlr2000/AI_NPC.git?path=/Packages/com.aicharacterkit.framework#v0.4.0
```

## 후보 검증

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

## GitHub Release 노트 초안

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

## 공개 승인 게이트

검증된 release-preparation commit을 만든 뒤 exact commit SHA, 테스트 결과와 이 초안을 검토한다. 사용자의 별도 최종 승인 전에는 `main`을 push하거나 `v0.4.0` tag를 생성·push하지 않고 GitHub Release를 발행하지 않는다. 공개 후 tag는 이동하지 않으며 수정은 `0.4.1` 이상의 새 version으로 배포한다.
