# ADR-0001 — STSP 아키텍처 표준 채택

- 상태: **채택** (2026-11-09)

## 결정

백엔드는 사내 STSP 아키텍처 표준을 따른다. ASP.NET Core Web API(.NET 10), Controller → Service → Repository 3계층, Repository의 partial 3분할(`.cs`·`.EF.cs`·`.SQL.cs`), `{레이어}/v1/{도메인}/` 폴더 미러링, 네이밍(DTO camelCase, `{도메인}Seq`, `Yn` 접미, 레이어별 메서드 접미사), 대문자 스네이크 테이블과 감사 컬럼·소프트 삭제를 그대로 쓴다. STSP가 "권장"으로 적은 항목(환경변수 비밀값, 필수 설정 누락 시 기동 실패)도 채택한다.

## 근거

백엔드 개발자가 1명이고 이미 익숙한 구조다. 코드는 대부분 LLM 에이전트가 쓰므로, 판단 여지가 적고 모든 도메인이 같은 모양인 구조가 유리하다.

## 결과

- 홈의 `~/AGENTS.md`(.NET 9, Pomelo, `AppException`, 단위 테스트)와 여러 곳에서 다르다. `backend/AGENTS.md`가 우선한다.
- STSP 원문은 비공개 레포에 있고 이 레포에 복사하지 않는다. 필요한 규칙은 `backend/docs/`에 옮겨 적었다.
- STSP와 다르게 한 곳은 각 ADR에 적는다([ADR-0003], [ADR-0004], [ADR-0005]).
