# Phase 13 후보 — Backend 배포 단위와 운영 도구

> 상태: 후속 가안 — 구현 승인 전 재계획
> 선행 판단: Phase 11/12의 실제 consumer 요구와 Backend 사용 방식 확인
> package/version: 미정

## 목적

현재 저장소의 `server/` reference source를 Unity package와 독립적으로 설치·실행·버전 확인할 수 있는 배포 단위로 정리한다. Phase 번호는 후속 방향을 잃지 않기 위해 기록하지만, 배포 형태와 Phase 12와의 실행 순서는 재계획할 수 있다.

## 후보 범위

- Unity package와 Backend 계약 호환 version 표기 및 startup 진단
- lockfile을 보존한 npm artifact, 실행 가능한 archive 또는 container 중 하나의 배포 방식 선정
- 환경 변수와 voice preset을 검증하는 setup/check 명령
- health/readiness 및 계약 version 확인 절차
- install, upgrade, rollback과 local-development 문서
- CI에서 build, test, artifact hash와 clean-machine smoke 검증

## 재계획 게이트

1. 대상 사용자가 Node.js source checkout, archive, container 중 무엇을 실제로 필요로 하는지 확인한다.
2. local-only 유지 여부와 지원 OS/CPU를 정한다.
3. Unity package와 Backend의 호환성 및 release cadence를 정한다.
4. artifact signing, SBOM과 취약점 검사 수준을 결정한다.
5. 원격 배포가 필요하면 인증, TLS, rate limit, abuse 방지와 privacy를 별도 보안 계획으로 승인한다.

## 제외 범위

- 승인 없는 공개 npm/container registry 발행
- 현재 loopback 서버를 그대로 인터넷에 노출
- API key의 Unity 전달 또는 bundled secret
- persistent memory, multi-tenant account와 production billing
- Phase 11 action handler나 Unity gameplay 코드의 Backend 이동

## 후보 완료 기준

- 새 환경에서 저장소 전체 checkout 없이 matching Backend를 재현 가능하게 설치한다.
- Unity package와 incompatible Backend 조합을 실행 전에 명확히 진단한다.
- secret과 대화·음성 내용이 artifact나 log에 포함되지 않는다.
- publish와 remote exposure는 각각 사용자의 별도 명시적 승인을 거친다.
