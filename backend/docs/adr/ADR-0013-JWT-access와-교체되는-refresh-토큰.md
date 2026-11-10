# ADR-0013 — JWT access와 교체되는 refresh 토큰

- 상태: **채택** (2026-11-10)
- ADR-0008의 "차량에 로그인 토큰을 발급한다"를 구체화한다.

## 결정

- 차량 로그인 토큰은 둘이다. access는 JWT(HS256, 수명 1시간, `Authorization: Bearer`)이고 클레임은 `sub`(USER_ID)·`sid`(LOGIN_SESSION_ID)·`did`(DEVICE_ID)다. refresh는 서버가 만든 256비트 난수(base64url)이고 수명은 90일이다.
- refresh는 쓸 때마다 새 값으로 바꾼다(rotation). 같은 `LOGIN_SESSION` 행의 `TOKEN_HASH`를 바꾸므로 `session_id`는 그대로다. 바꾸기 직전 값 하나는 10분 동안 받아 준다.
- 토큰 발급은 OAuth 2.0 형식의 `POST /api/v1/Auth/M/Token` 하나로 한다. 방식은 `grant_type`으로 나눈다(`refresh_token`, 이후 `otp`, `urn:ietf:params:oauth:grant-type:device_code`).
- 휴대폰 웹 세션도 `LOGIN_SESSION`에 두고 종류 칸 `SESSION_TYPE`(`CAR`, `WEB`)으로 가른다. 이 테이블을 읽는 모든 조회에 `SESSION_TYPE` 조건을 붙인다.
- `sign` 구간은 JWT의 서명·만료·발급자·수신자만 검사하고 DB를 조회하지 않는다. 경로에 `/sign/`이 있는 API는 규약(`SignRouteConvention`)이 인증을 요구하며, 빠뜨리면 열리는 것이 아니라 닫힌다.
- 전화번호는 AES-256-GCM으로 암호화해 `PHONE_ENC`에 둔다(키 버전 1바이트 + nonce 12바이트 + 암호문 + tag 16바이트). 계정을 찾는 `PHONE_HASH`와 토큰의 `_HASH`는 HMAC-SHA256이고 암호화 키와 다른 키를 쓴다. 키와 발급자·수신자는 설정(`Jwt`·`Crypto`)에서 읽고, 하나라도 비면 기동 실패다.

## 근거

- 차량은 사람이 없는 화면이라 비밀번호를 받을 수 없고, 로그인은 한 번 하면 오래 유지돼야 한다. 수명이 짧은 access에 오래 가는 refresh를 짝지으면 access가 새도 피해가 1시간으로 줄고 로그인은 유지된다.
- 주행 중 통신이 끊겨 응답을 놓칠 수 있다. 쓰는 즉시 옛 refresh를 버리면 그 차량은 로그아웃된다. 직전 값을 10분 허용하면 재시도가 통과한다.
- `sign` 구간이 요청마다 DB를 조회하지 않으면 모든 차량 API가 인증 때문에 DB 왕복을 하나 더 하지 않는다.
- 토큰 발급 방식(인증번호, QR, refresh)이 여럿이어도 앱이 부르는 주소와 응답 형태가 하나면 앱 쪽 코드가 줄고, 표준 형식이라 설명할 것이 적다.
- 휴대폰 웹 세션은 수명과 폐기 방식이 차량 세션과 같아 테이블을 따로 만들 이유가 없다. 종류 칸 하나로 충분하다.
- 경로로 인증을 거는 규약이 있으면 새 API에 `[Authorize]`를 빠뜨려도 `sign` 구간이면 닫힌다. 인증 없이 열리는 `sign` API가 생기는 사고를 막는다.

## 결과

- 로그아웃해도 이미 발급된 access는 만료(최대 1시간)까지 통과한다. 즉시 막는 장치는 두지 않았다. 로그아웃은 refresh만 거절시킨다.
- `sign` 구간 API에는 `[AllowAnonymous]`를 쓸 수 없다. 붙이면 서버가 기동할 때 `InvalidOperationException`으로 실패한다.
- `Token`은 `{ message, data, code }`가 아니라 OAuth 본문을 돌려주는 예외다. `api.md`·`controller.md`·`dto.md`에 예외로 적는다.
- `LOGIN_SESSION`에 `SESSION_TYPE`·`PREV_TOKEN_HASH`·`ROTATE_DT`가 더해졌다(`sql/20261110_login_session_type.sql`). 조회마다 `SESSION_TYPE` 조건을 빠뜨리면 차량 세션과 웹 세션이 섞인다.
- 키가 셋(JWT 서명, 전화번호 암호화, 해시)이라 배포 환경마다 값을 주입해야 한다. 서명 키를 바꾸면 발급된 access가 모두 무효가 되고, 해시 키를 바꾸면 저장된 토큰·번호 해시를 모두 못 찾는다.
