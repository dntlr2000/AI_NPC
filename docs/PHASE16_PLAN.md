# Phase 16 후보 — 선택형 Offline Local Inference

> 상태: 후속 가안 — 구현 승인 전 요구 조사와 재계획 필요
> 선행 조건: Phase 15 grounding 경계 안정화
> package version: 미정

## 목적

게임이 외부 Backend나 인터넷 없이 완결돼야 하는 consumer를 위해, 다운로드된 언어 모델을 선택형 대화 provider로 연결한다. 기존 `IAiConversationClient`, structured response, presentation, grounding과 action 경계를 재사용하며 기본 UPM package에 특정 모델이나 native runtime을 강제하지 않는다.

## 권장 방향

첫 구현은 별도 로컬 process 또는 consumer-owned adapter를 우선 검토한다. 이 방식은 Unity main process의 native crash와 플랫폼별 plugin 충돌을 격리하고, 모델/runtime 교체를 Core 변경 없이 수행하기 쉽다. 완전한 단일 실행 파일이 필수이고 대상 플랫폼이 고정됐을 때만 in-process native inference를 비교한다.

```text
NpcAIController
    → IAiConversationClient
         ├─ existing Mock
         ├─ existing loopback Backend
         └─ optional LocalInferenceConversationClient
                 → consumer-selected runner/model
```

## 구현 전 결정할 항목

1. 지원 플랫폼과 CPU/GPU/NPU backend
2. 허용 다운로드 크기, RAM/VRAM, 첫 token 및 전체 응답 latency
3. 모델·tokenizer·runtime 라이선스와 재배포 조건
4. 모델 파일을 build에 포함할지 첫 실행 시 내려받을지
5. V4 grounding과 V3 action schema를 안정적으로 따르는 constrained decoding 방식
6. cancellation, process crash, timeout, save 경로와 integrity 검증
7. 저사양 fallback: deterministic Mock, authored dialogue, 또는 기능 비활성화

## 후보 범위

- 선택형 local inference client/runner interface와 capability probe
- model manifest, hash 검증, 명시적 설치/제거 절차
- V4 request grounding을 입력으로 사용하고 기존 `AiNpcResponse`로 반환
- JSON/schema 오류, timeout, cancellation과 out-of-memory의 안전한 mapping
- 플랫폼별 benchmark scene 및 최소 지원 사양 문서
- provider가 없어도 기존 Mock와 online Backend가 그대로 동작하는 optional composition

## 제외 범위

- AI Character Kit 저장소에 모델 weight를 직접 commit
- 라이선스가 불명확한 model/runtime 재배포
- 모든 플랫폼과 모든 GPU를 동시에 지원
- 모델이 임의 C# 메서드나 Unity object를 직접 호출
- local inference와 장기 기억·RAG·Realtime을 한 단계에서 함께 구현

## 재계획 게이트

실제 게임의 대상 플랫폼, 최소 하드웨어, 배포 크기와 품질 평가 문장을 먼저 확정한다. 최소 두 개의 후보 model/runtime을 동일한 V4 fixture로 측정한 뒤 adapter 형태, package 분리, 공개 API와 version을 결정한다. 이 문서는 방향과 책임 경계만 고정하며 구현 승인을 의미하지 않는다.
