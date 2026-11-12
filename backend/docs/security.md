# 보안

## 비밀값·설정

- 비밀값(DB·Redis 비밀번호, 암호화·해시 키 등)을 코드나 `appsettings.json`에 쓰지 않는다. `appsettings.json`에는 빈 값이나 자리표시자만 둔다.
- 실제 값은 환경변수로 주입한다. 설정 계층 구분자 `:`는 `__`로 쓴다(예: `ConnectionStrings__DefaultConnection`).
- 로컬 개발 값은 `appsettings.Development.json`에 둔다. 이 파일은 git에서 제외되어 있으니 커밋하지 않는다.
- 명령줄에 비밀번호를 쓰지 않는다. 스캐폴드도 `Name=ConnectionStrings:DefaultConnection`으로 설정에서 읽게 한다.
- 필수 설정(연결 문자열·서명 키)이 비어 있으면 기동할 때 `InvalidOperationException`으로 실패시킨다. 빈 값으로 조용히 넘어가지 않는다.
- 설정 묶음은 `IOptions<T>`로 받는다. 서비스에 `IConfiguration`을 직접 주입하지 않는다.
- Issuer·Audience·프록시 IP처럼 환경마다 다른 값도 코드 상수로 박지 않고 설정에서 읽는다.
- `Kakao:RestApiKey`(카카오 REST API 키)는 선택 비밀값이다. 환경변수 `Kakao__RestApiKey`로 주입하고, 비어 있어도 기동은 하며 도보 시간은 직선 거리 추정으로, 임의 좌표의 장소 이름은 `부모님이 정한 장소`로 떨어진다(기동 로그 `Kakao walk route: ON/OFF`). 같은 키를 도보 경로와 좌표→주소 변환에 쓴다.
- `PublicData:ServiceKey`(공공데이터포털 키)는 선택 비밀값이다. 환경변수 `PublicData__ServiceKey`로 주입하고(compose는 `.env`의 `DATA_GO_KR_API_KEY`를 넘긴다), 비어 있어도 기동은 하며 대기 장소의 실시간 빈자리는 저장값이나 `UNKNOWN`으로 떨어진다(기동 로그 `Parking realtime: ON/OFF`). 키는 요청 URL에만 넣고 로그·예외 메시지에 남기지 않는다. [ADR-0020]

## 로그

- 비밀번호·토큰·개인정보(아이 이름, 연락처, 위치 등)를 로그에 남기지 않는다.
- `EnableSensitiveDataLogging()`은 Development에서만 켠다.
- 출발 안내 계산에서 부모가 바꾼 픽업 장소 좌표와 학원 좌표는 도보 시간을 구하려고 카카오 도보 경로 API(외부)로 전송된다.
- 오늘 픽업 계획을 만들거나 장소를 바꿀 때 부모가 고른 임의 좌표와 학원 좌표는 도보 시간을 구하려고 카카오 도보 경로 API(외부)로, 임의 좌표는 장소 이름을 만들려고 카카오 좌표→주소 변환 API(외부, `coord2address`)로 전송된다. 결과는 계획에 저장하고 캐시하지 않는다.
- 픽업 계획의 좌표, 주소·장소 이름(임의 좌표의 `CUSTOM_NAME`), 하원 시각, 전화번호는 로그와 `PICKUP_EVENT.DETAIL`에 남기지 않는다. 계획 관련 로그에는 계획·계정 ID 같은 식별자만 남기고, 카카오 호출 실패는 HTTP 상태 코드나 실패 종류만 남긴다(키·URL·응답 본문 제외).
- 정차(`UpdatePickupPlanStop`)의 정차 좌표는 배정 장소와의 거리와 단속 구역 판정에만 쓰고 저장하지 않는다. 좌표와 거리는 DB(`PICKUP_PLAN`·`PICKUP_EVENT.DETAIL`)와 로그에 남기지 않는다. `WRONG_STOP`의 `DETAIL`에는 걸린 구역 유형만 둔다.
- 부모 취소(`DeletePickupPlan`)는 아이가 링크를 받았을 수 있으면 아이 번호(`CHILD.PHONE_ENC`)를 복호화해 취소 문자를 보낸다. 번호와 부모 이름은 익명화 전에 읽어 메모리에만 두고, 커밋한 뒤 잠금 밖에서 보낸다. 발송 실패·예외와 복호화 실패 로그에는 계획 ID와 사업자 결과 코드만 남긴다(번호·본문 제외).

## 입력·쿼리

- Dapper 값은 반드시 `DynamicParameters`로 넘긴다. 사용자 입력을 SQL 문자열에 보간하지 않는다.
- `LIKE` 검색어는 `\`·`%`·`_`를 이스케이프하고 `ESCAPE '\\'`를 붙인다.

## 인증·인가

- 부모만 계정을 가진다. 비밀번호는 없다. 휴대폰 인증번호나 QR로 로그인하고 차량에 로그인 토큰을 발급한다. [ADR-0008]
- 로그인 토큰, 인증번호, 픽업 링크 토큰은 원문을 저장하지 않는다. 서버 비밀키로 계산한 해시만 `_HASH` 컬럼에 둔다.
- 전화번호는 암호화해 `_ENC` 컬럼에 두고, 계정을 찾는 용도로는 해시를 따로 둔다.
- `PHONE_HASH`는 숫자만 남긴 전화번호를 HMAC 입력으로 쓴다. `USER_INFO`와 `PHONE_VERIFICATION`에서 같은 규칙을 쓴다.
- `CODE_HASH`는 `"otp:" + 숫자만 남긴 전화번호 + ":" + 6자리 인증번호`를 HMAC 입력으로 쓴다.
- `QR_LOGIN.DEVICE_CODE_HASH`는 `"qr-device:" + device_code`, `USER_CODE_HASH`는 `"qr-user:" + user_code`를 HMAC 입력으로 쓴다. 두 코드 원문은 DB나 로그에 남기지 않는다.
- 픽업 링크 토큰 원문은 `"pickup-link:" + PICKUP_PLAN_ID + ":" + TOKEN_NONCE`의 HMAC을 base64url로 쓴 값이고, `PICKUP_SESSION.TOKEN_HASH`는 `"pickup-link-token:" + 토큰 원문`을 HMAC 입력으로 쓴다. 토큰 원문은 저장하지 않고 `TOKEN_NONCE`로 다시 계산한다.
- 아이 휴대폰 웹은 링크 토큰을 `X-Pickup-Token` 헤더로만 보낸다. URL·쿼리에 싣지 않아 접근 로그에 남지 않게 한다. 서버도 토큰 원문을 로그에 남기지 않고 계획 ID만 남긴다.
- 아이 출발 알림 API(`Pickup/W/AddChildLeave`·`DeleteChildLeave`)는 인증 없이 열려 있다. 토큰은 추측할 수 없지만 같은 토큰으로 계속 부르면 이벤트 행이 쌓이므로, 차 계정 목록과 같은 방식으로 클라이언트(IPv4 주소, IPv6는 /64)마다 1분에 30번으로 제한한다(넘으면 429 + `Retry-After`). 배포 환경에는 아직 `UseForwardedHeaders`가 없어 모든 요청이 프록시 주소 하나로 묶이고, 그래서 지금은 모든 아이가 1분 30회를 나눠 쓴다. 이 위험은 받아들이고 T26에서 고친다.
- 아이 픽업 안내 API(`Pickup/W/DetailChildPickup`·`AddPickupLinkOpen`)도 인증 없이 열려 있다. 이 둘은 IP가 아니라 링크 토큰마다 센다. 조회는 1분 30회(웹의 15초 대체 조회는 4회), 열람 기록은 1분 10회다. 위 IP 파티션 문제 때문에 IP로 세면 모든 아이가 한 몫을 나눠 쓰게 되기 때문이다. 토큰을 바꿔 가며 부르는 경우를 막으려고 두 API를 합친 전체 공용 상한(1분 600회)을 둔다. 두 제한은 `GlobalLimiter` 안에서 토큰별 → 공용 순서로 묶어, 토큰별 제한을 통과한 요청만 공용 상한을 쓴다. 토큰별 제한을 엔드포인트 정책으로 두면 미들웨어가 `GlobalLimiter`를 먼저 획득하고 고정 창 허용량은 거절돼도 돌려주지 않아서, 한 토큰의 반복 요청만으로 공용 상한이 바닥나 모든 아이가 막힌다(PR #40 리뷰에서 실측). 파티션 키는 토큰 원문이 아니라 SHA-256 해시이고, 헤더가 없거나 형식이 틀린 토큰은 DB를 부르기 전에 404로 끝나므로 어느 제한에도 세지 않는다. 넘으면 429 + `Retry-After`다(`Comm/ChildPickupRateLimit.cs`).
- 아이 픽업 안내 조회는 장소·차 정보·운전자 이름을 돌려주므로 `Cache-Control: no-store`로 응답하고, 끝난 링크(탑승·만료·취소)에는 그 값을 돌려주지 않는다.
- QR 로그인은 3분 동안 유효하고 차량은 3초 간격으로 승인 상태를 조회한다. 초 단위로 내린 서버 시각이 마지막 조회 시각 + 2초보다 이르면 `slow_down`이며, 승인된 QR은 만료 시각 뒤 60초까지 차량이 토큰으로 바꿀 수 있다. 차와 휴대폰에는 같은 숫자 4자리 `match_code`를 보여준다.
- `device_code`는 QR에 넣지 않고 발급받은 차량만 보관한다. 승인 뒤에도 이 값을 받은 기기만 차량 토큰을 받을 수 있다.
- 발송은 `ISmsSender`. `Sms:Provider`로 Dev/솔라피를 고른다. Dev는 실제로 발송하지 않고 가린 전화번호와 메시지를 콘솔 로그에 남기며, Development 환경에서만 기동한다(그 밖의 환경에서 Dev이거나 비어 있으면 기동 실패). 솔라피 키(`Sms:Solapi:*`)는 환경변수나 git 제외 설정 파일로만 넣고, 번호·메시지 본문·사업자 응답 본문은 로그에 남기지 않는다. [ADR-0019]
- 남용 방어(IP·기기별 발송 한도, 번호당 시간·일 한도)는 ADR-0019에 따라 미뤘다. 지금은 같은 번호 1분에 한 번뿐이라 번호를 바꿔 가며 부르면 발송 비용을 막을 수 없다.
- 차 계정 목록(`GetLoginSessionList`)은 로그인 전에 기기 번호만으로 그 차의 운전자 이름을 보여준다. 그래서 기기 번호(`device_id`·`deviceId`)는 로그인 전 조회의 열쇠로 다룬다.
  - 앱은 처음 실행할 때 CSPRNG로 UUID v4를 만들어 앱 저장소에만 둔다. 하드웨어 ID·차대번호·Pleos 계정 ID를 쓰지 않는다. 로그·분석 도구로 보내지 않는다.
  - 서버는 응답에 전화번호·계정 ID·사용 시각을 넣지 않고, 클라이언트(IPv4 주소, IPv6는 /64)마다 1분에 30번으로 제한한다(넘으면 429 + `Retry-After`).
  - 프록시 뒤에 배포하면 `UseForwardedHeaders`(신뢰할 프록시만, `KnownProxies`/`KnownNetworks`)를 먼저 설정해야 제한이 클라이언트별로 동작한다. 설정하지 않으면 모든 요청이 프록시 주소 하나로 묶인다.
- 차량 로그인 토큰은 access와 refresh 둘이다. 발급은 `POST /api/v1/Auth/M/Token`(`api.md`)이다. [ADR-0013]

### 휴대폰 웹 세션

- 휴대폰 웹 로그인 상태는 `LOGIN_SESSION.SESSION_TYPE = 'WEB'` 행으로 보관한다. 토큰은 32바이트 난수의 base64url 값이고 DB에는 `Hmac("web-session:" + token)`만 둔다.
- 토큰은 `__Host-rm_web_session` 쿠키로만 전달한다. 쿠키는 `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, `Max-Age=90일`이고 `Domain`을 두지 않으며 응답 본문에는 넣지 않는다.
- WEB 세션은 발급 시점부터 90일 동안 유효하다. 사용해도 `EXPIRE_DT`나 쿠키 수명을 늘리지 않으며, 휴대폰 여러 대의 WEB 세션을 함께 유지할 수 있다.
- 같은 브라우저에서 새 WEB 세션을 만들 때 유효한 기존 세션 쿠키가 있으면 그 세션을 같은 트랜잭션에서 끝낸다.
- `WebSession` 인증 스킴이 쿠키 해시로 만료되지 않은 WEB 세션과 살아 있는 계정을 조회한다. `/W/sign/` 경로는 이 스킴을, `/M/sign/` 경로는 기존 차량 JWT를 요구한다. 인증 실패 본문은 둘 다 같은 401 공통 형식이다.
- `SameSite=Lax` 쿠키를 안정적으로 유지하려면 휴대폰 웹을 API와 같은 사이트에 배포해야 한다. 같은 도메인의 하위 주소를 쓰거나 웹 서버가 `/api`를 프록시한다. [ADR-0016]
- 실기기로 확인할 때는 Secure 쿠키라 HTTPS(또는 터널)가 필요하다. `http://localhost`만 예외다.
- 처음 가입할 때 서비스 이용약관(`TERMS`)과 개인정보 수집·이용(`PRIVACY`) 동의를 각각 `CONSENT`에 쌓는다. 두 행은 인증번호 행을 가리키며 `VERIFY_METHOD = 'PHONE_OTP'`로 기록한다.

| | access 토큰 | refresh 토큰 |
|---|---|---|
| 형태 | JWT, HS256 서명 | base64url 43자. 처음 발급할 때는 256비트 난수이고, 교체한 값은 서버 해시 키로 계산한다(아래) |
| 수명 | 1시간(`expires_in` 3600) | 90일. 쓸 때마다 다시 90일로 늘어난다 |
| 보관 | 서버에 두지 않는다 | `LOGIN_SESSION.TOKEN_HASH`에 HMAC-SHA256 해시(소문자 hex 64자)만 둔다. 원문은 응답으로 한 번만 준다 |
| 싣는 방법 | `Authorization: Bearer <access 토큰>` | `Token` 요청의 form 값 `refresh_token` |
| 클레임 | `sub`(USER_ID), `sid`(LOGIN_SESSION_ID), `did`(DEVICE_ID), `iss`, `aud`, `exp` | |

- refresh 토큰은 쓸 때마다 새 값으로 바꾼다. 같은 `LOGIN_SESSION` 행의 `TOKEN_HASH`를 바꾸므로 `session_id`는 그대로다. 그 행을 `FOR UPDATE`로 잠그고 바꾼다.
- 새 값은 난수가 아니라 계산한 값이다: base64url(HMAC-SHA256(`Crypto:HashKey`, `"refresh-next:" + LOGIN_SESSION_ID + ":" + 낸 refresh 값`)). 같은 세션에 같은 값을 내면 늘 같은 새 값이 나온다. 원문은 저장하지 않는다.
- 바꾸기 직전 값 하나는 24시간 동안 받아 준다(응답을 놓친 차량의 재시도). 직전 값을 내면 이미 저장된 지금 값과 같은 새 값을 다시 주고, `TOKEN_HASH`·`PREV_TOKEN_HASH`·`ROTATE_DT`는 바꾸지 않는다. 그래서 허용 시간은 늘어나지 않고, 재시도는 순서가 뒤바뀌어 늦게 처리되든 동시에 오든 안전하다. 같은 값으로 refresh 두 번이 동시에 와도 둘 다 성공하고 같은 `refresh_token`을 받는다.
- 차량이 로그아웃되지 않으려면 앱은 다음을 지킨다.
  1. 세션 하나에서 refresh는 한 번에 하나만 보낸다.
  2. 새 refresh 값을 저장하기 전에는 옛 값을 버리지 않는다. 응답을 받지 못했으면 같은 값으로 다시 보낸다(직전 값은 24시간 동안 받아 준다).
  3. refresh(`grant_type=refresh_token`) 요청의 `invalid_grant`만 "로그아웃됨"으로 처리한다. `otp`의 `invalid_grant`는 "대기 중인 인증번호 없음"이라 인증번호를 다시 받게 한다. 네트워크 오류, 5xx, 429는 다시 시도한다.
- `M/sign` 구간은 JWT의 서명·만료·발급자·수신자만 검사하고 DB를 조회하지 않는다. 그래서 로그아웃(`DeleteLoginSession`) 뒤에도 그 access 토큰은 만료(최대 1시간)까지 통과한다. 로그아웃은 `LOGIN_SESSION`을 끝내 refresh만 거절시킨다.
- 인증에 실패한 `sign` 요청은 401이고 본문은 `{ "message": "잘못된 요청입니다.", "data": null, "code": 401 }`이다. 차량 JWT나 웹 세션 쿠키가 없거나 올바르지 않아도 같다.
- Service는 `IHttpContextAccessor`의 클레임 `sub`에서 `USER_ID`를 꺼낸다. JwtBearer가 클레임 이름을 바꾸지 않도록 `MapInboundClaims = false`로 둔다.
- 경로에 `sign` 구간이 있는 API는 `SignRouteConvention`(`app.MapControllers().RequireAuthForSignRoutes()`)이 인증을 요구한다. 최종 경로(클래스와 액션의 `[Route]`를 합친 것)로 판단하므로 액션에 `[Authorize]`를 따로 붙이지 않아도 닫힌다. `W/sign`에는 `WebSession`, 그 밖의 `sign`에는 기본 Bearer 스킴을 붙인다. `sign` 구간 API에 `[AllowAnonymous]`를 붙이면 서버가 기동할 때 `InvalidOperationException`으로 실패한다. Swagger도 같은 규칙으로 쿠키 또는 Bearer 보안 표시를 붙인다(`SignRouteOperationFilter`).
- 키와 발급자 설정은 다섯 개다. 하나라도 비어 있거나 base64가 아니거나 길이가 맞지 않거나, 세 키 중 둘이 같은 값이면 기동할 때 `InvalidOperationException`으로 실패한다.

| 설정 이름 | 값 |
|---|---|
| `Jwt:Issuer` | 발급자(`iss`). 비어 있으면 안 된다 |
| `Jwt:Audience` | 수신자(`aud`). 비어 있으면 안 된다 |
| `Jwt:SigningKey` | JWT 서명 키. base64, 32바이트 이상 |
| `Crypto:PhoneEncKey` | 전화번호 AES-256-GCM 암호화 키. base64, 정확히 32바이트 |
| `Crypto:HashKey` | 토큰·인증번호 등의 HMAC-SHA256 해시 키. 교체한 refresh 값도 이 키로 계산한다(바꾸면 모든 세션이 끝난다). base64, 32바이트 이상. 암호화 키와 다른 값을 쓴다 |

- 전화번호 암호문은 키 버전 1바이트(`0x01`) + nonce 12바이트 + 암호문 + tag 16바이트를 이어 `PHONE_ENC`에 둔다. [ADR-0013]
