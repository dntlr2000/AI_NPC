# Phase 14 후보 — Realtime 음성 대화

> 상태: 후속 가안 — 구현 승인 전 재계획
> 선행 판단: Push-to-Talk의 실제 지연·끼어들기 한계와 지원 플랫폼 확인
> package/version: 미정

## 목적

Phase 7의 검토 가능한 Push-to-Talk 경로로 해결되지 않는 저지연 양방향 음성 요구가 입증될 때, 기존 text/session/action 계약을 대체하지 않는 선택형 Realtime adapter를 추가한다. Phase 번호는 장기 방향을 기록하기 위한 것이며 API와 구현 순서는 검증 결과에 따라 바뀔 수 있다.

## 후보 범위

- provider-neutral realtime session, audio input/output와 lifecycle 계약
- 명시적 connect/disconnect, 권한, timeout, cancel과 reconnect 정책
- bounded PCM streaming, VAD와 사용자가 말할 때 playback을 중지하는 barge-in
- partial transcript와 final transcript의 구분 및 UI 상태
- 대화 turn과 action trigger를 한 번만 확정하는 commit 경계
- latency, interruption, packet loss와 cost를 측정하는 fake 기반 테스트 harness

## 재계획 게이트

1. Push-to-Talk 대비 목표 latency와 실제 실패 사례를 수집한다.
2. Windows 외 지원 플랫폼, microphone 권한과 audio format을 정한다.
3. provider session 인증을 Unity에 노출하지 않는 Backend relay 방식을 결정한다.
4. partial output 중 presentation, memory와 action을 언제 확정할지 정한다.
5. reconnect 중 중복 turn/action 방지와 비용 상한을 정의한다.

## 제외 범위

- 요구 증거 없는 Push-to-Talk 경로 제거
- Unity에 provider API key 저장
- custom voice training, voice cloning, lip sync 또는 audio caching
- 모델이 직접 Unity method나 action parameter를 호출하는 tool execution
- 원격 production Backend 보안 설계의 암묵적 포함

## 후보 완료 기준

- 기존 Mock, text V1/V2/V3, TTS와 Push-to-Talk 경로가 변경 없이 유지된다.
- interruption과 reconnect에서도 동일 turn/action이 중복 commit되지 않는다.
- raw audio와 transcript가 log나 disk에 남지 않는다.
- 지원 플랫폼에서 측정된 latency가 사전에 정한 목표를 충족한다.
