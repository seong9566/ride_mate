# ADR-0015 — 토큰 API를 용도별 JSON과 공통 응답으로

- 상태: **채택** (2026-11-11)
- ADR-0013의 "OAuth 2.0 형식의 `POST /api/v1/Auth/M/Token` 하나"를 대체한다. 토큰 종류·수명·교체 규칙은 ADR-0013 그대로다.

## 결정

- 인증번호 로그인은 `POST /api/v1/Auth/M/AddLoginSession`, 토큰 갱신은 `PATCH /api/v1/Auth/M/UpdateLoginSession`으로 나눈다. 둘 다 `sign` 구간이 아니며 Bearer 없이 부른다.
- 요청은 `application/json`이다. `AddLoginSession`은 `phone`·`code`·`deviceId`, `UpdateLoginSession`은 `refreshToken`을 받는다. 요청 DTO에는 필수·형식·길이 검증 특성을 붙인다.
- 성공과 실패는 다른 API처럼 `{ message, data, code }` 공통 응답을 쓴다. 성공한 `data`에는 토큰과 세션 정보를, 실패한 `data`에는 `error`와 필요한 부가 값만 넣는다. 값이 없는 칸은 JSON에서 뺀다.
- 두 응답 모두 토큰을 담을 수 있으므로 `Cache-Control: no-store`를 붙인다.
- 두 grant의 검증 순서, 잠금, 트랜잭션, 교착 재시도, 세션 종료·생성, 토큰 값은 바꾸지 않는다.

## 근거

- 모빌리티 앱은 OAuth 라이브러리를 쓰지 않는다. OAuth 토큰 본문을 별도로 처리할 이유가 없다.
- 모든 API가 공통 응답을 쓰면 앱의 응답 처리를 하나로 맞출 수 있다.
- 용도별 API로 나누면 Swagger에서 각 API에 필요한 요청 칸만 보인다.
- 요청 DTO의 검증 특성을 쓸 수 있어 필수 값과 형식을 다른 API와 같은 방식으로 검사한다.

## 결과

- QR 로그인 T09도 `grant_type`을 늘리지 않고 별도 JSON API로 추가한다.
- 오류 이름은 다음처럼 바뀐다. 앱은 `data.error`로 분기한다.

| 기존 오류 | 새 오류 | API |
|---|---|---|
| `invalid_request` | `invalid_request` | 둘 다 |
| refresh의 `invalid_grant` | `invalid_refresh_token` | `UpdateLoginSession` |
| otp의 `invalid_grant`(대기 중인 인증번호 없음) | `otp_not_requested` | `AddLoginSession` |
| otp의 `invalid_grant`(계정 확인 불가) | `signup_required` | `AddLoginSession` |
| `otp_locked` | `otp_locked` | `AddLoginSession` |
| `otp_expired` | `otp_expired` | `AddLoginSession` |
| `otp_mismatch` | `otp_mismatch` | `AddLoginSession` |
| `signup_required` | `signup_required` | `AddLoginSession` |

- OAuth 본문으로 바꾸던 `ApiControllerBase.ToTokenActionResult`를 제거하고 `ToActionResult`를 쓴다.
