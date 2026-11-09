# ADR-0004 — 메서드별 try/catch와 오류 변환

- 상태: **채택** (2026-11-09)

## 결정

모든 Controller 액션과 Service 메서드에 try/catch를 둔다. 전역 예외 필터로 바꾸지 않는다. Repository는 예외를 기록한 뒤 다시 던지고, Service는 예외를 `ResponseModel`의 `code = 500`으로 바꿔 돌려준다. 요청이 취소된 경우에만 예외를 기록하지 않고 그대로 올려보내며, Controller는 499를 돌려준다.

## 근거

- 메서드별 try/catch와 Repository·Service 양쪽의 로그는 STSP 표준이다([ADR-0001]). 같은 예외가 두 번 기록되지만 이 규모(하루 100건 미만)에서는 영향이 작아 그대로 둔다.
- 취소 토큰 전달과 취소 예외 분리는 STSP에 없던 것을 더했다. 앱이 연결을 끊은 요청이 500 오류로 기록되지 않게 하기 위해서다.

## 결과

- Service는 요청 취소를 빼고는 예외를 던지지 않는다.
- 모든 Service·Repository 메서드는 마지막 파라미터로 `CancellationToken`을 받는다.
