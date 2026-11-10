# 보안

## 비밀값·설정

- 비밀값(DB·Redis 비밀번호, 암호화·해시 키 등)을 코드나 `appsettings.json`에 쓰지 않는다. `appsettings.json`에는 빈 값이나 자리표시자만 둔다.
- 실제 값은 환경변수로 주입한다. 설정 계층 구분자 `:`는 `__`로 쓴다(예: `ConnectionStrings__DefaultConnection`).
- 로컬 개발 값은 `appsettings.Development.json`에 둔다. 이 파일은 git에서 제외되어 있으니 커밋하지 않는다.
- 명령줄에 비밀번호를 쓰지 않는다. 스캐폴드도 `Name=ConnectionStrings:DefaultConnection`으로 설정에서 읽게 한다.
- 필수 설정(연결 문자열·서명 키)이 비어 있으면 기동할 때 `InvalidOperationException`으로 실패시킨다. 빈 값으로 조용히 넘어가지 않는다.
- 설정 묶음은 `IOptions<T>`로 받는다. 서비스에 `IConfiguration`을 직접 주입하지 않는다.
- Issuer·Audience·프록시 IP처럼 환경마다 다른 값도 코드 상수로 박지 않고 설정에서 읽는다.

## 로그

- 비밀번호·토큰·개인정보(아이 이름, 연락처, 위치 등)를 로그에 남기지 않는다.
- `EnableSensitiveDataLogging()`은 Development에서만 켠다.

## 입력·쿼리

- Dapper 값은 반드시 `DynamicParameters`로 넘긴다. 사용자 입력을 SQL 문자열에 보간하지 않는다.
- `LIKE` 검색어는 `\`·`%`·`_`를 이스케이프하고 `ESCAPE '\\'`를 붙인다.

## 인증·인가

- 부모만 계정을 가진다. 비밀번호는 없다. 휴대폰 인증번호나 QR로 로그인하고 차량에 로그인 토큰을 발급한다. [ADR-0008]
- 로그인 토큰, 인증번호, 픽업 링크 토큰은 원문을 저장하지 않는다. 서버 비밀키로 계산한 해시만 `_HASH` 컬럼에 둔다.
- 전화번호는 암호화해 `_ENC` 컬럼에 두고, 계정을 찾는 용도로는 해시를 따로 둔다.
- 차량 로그인 토큰은 access와 refresh 둘이다. 발급은 `POST /api/v1/Auth/M/Token`(`api.md`)이다. [ADR-0013]

| | access 토큰 | refresh 토큰 |
|---|---|---|
| 형태 | JWT, HS256 서명 | 256비트 난수의 base64url |
| 수명 | 1시간(`expires_in` 3600) | 90일. 쓸 때마다 다시 90일로 늘어난다 |
| 보관 | 서버에 두지 않는다 | `LOGIN_SESSION.TOKEN_HASH`에 HMAC-SHA256 해시(소문자 hex 64자)만 둔다. 원문은 응답으로 한 번만 준다 |
| 싣는 방법 | `Authorization: Bearer <access 토큰>` | `Token` 요청의 form 값 `refresh_token` |
| 클레임 | `sub`(USER_ID), `sid`(LOGIN_SESSION_ID), `did`(DEVICE_ID), `iss`, `aud`, `exp` | |

- refresh 토큰은 쓸 때마다 새 값으로 바꾼다. 같은 `LOGIN_SESSION` 행의 `TOKEN_HASH`를 바꾸므로 `session_id`는 그대로다. 바꾸기 직전 값 하나는 10분 동안 받아 준다(응답을 놓친 차량의 재시도). 이때 허용 시간은 늘어나지 않는다. 그 행을 `FOR UPDATE`로 잠그고 바꾼다.
- `sign` 구간은 JWT의 서명·만료·발급자·수신자만 검사하고 DB를 조회하지 않는다. 그래서 로그아웃(`DeleteLoginSession`) 뒤에도 그 access 토큰은 만료(최대 1시간)까지 통과한다. 로그아웃은 `LOGIN_SESSION`을 끝내 refresh만 거절시킨다.
- 인증에 실패한 `sign` 요청은 401이고 본문은 `{ "message": "잘못된 요청입니다.", "data": null, "code": 401 }`이다. 토큰이 없거나 위조·만료여도 같다.
- Service는 `IHttpContextAccessor`의 클레임 `sub`에서 `USER_ID`를 꺼낸다. JwtBearer가 클레임 이름을 바꾸지 않도록 `MapInboundClaims = false`로 둔다.
- 경로에 `sign` 구간이 있는 API는 `SignRouteConvention`(`app.MapControllers().RequireAuthForSignRoutes()`)이 인증을 요구한다. 최종 경로(클래스와 액션의 `[Route]`를 합친 것)로 판단하므로 액션에 `[Authorize]`를 따로 붙이지 않아도 닫힌다. `sign` 구간 API에 `[AllowAnonymous]`를 붙이면 서버가 기동할 때 `InvalidOperationException`으로 실패한다. Swagger의 Bearer 표시도 같은 규칙으로 `sign` 구간 API에만 붙는다(`SignRouteOperationFilter`).
- 키와 발급자 설정은 다섯 개다. 하나라도 비어 있거나 base64가 아니거나 길이가 맞지 않으면 기동할 때 `InvalidOperationException`으로 실패한다.

| 설정 이름 | 값 |
|---|---|
| `Jwt:Issuer` | 발급자(`iss`). 비어 있으면 안 된다 |
| `Jwt:Audience` | 수신자(`aud`). 비어 있으면 안 된다 |
| `Jwt:SigningKey` | JWT 서명 키. base64, 32바이트 이상 |
| `Crypto:PhoneEncKey` | 전화번호 AES-256-GCM 암호화 키. base64, 정확히 32바이트 |
| `Crypto:HashKey` | 토큰·인증번호 등의 HMAC-SHA256 해시 키. base64, 32바이트 이상. 암호화 키와 다른 값을 쓴다 |

- 전화번호 암호문은 키 버전 1바이트(`0x01`) + nonce 12바이트 + 암호문 + tag 16바이트를 이어 `PHONE_ENC`에 둔다. [ADR-0013]
