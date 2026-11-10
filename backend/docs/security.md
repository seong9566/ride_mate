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
- 토큰의 수명과 갱신 방식, 요청에 토큰을 싣는 방법은 아직 정하지 않았다. 인증 API를 만들 때 이 절에 추가한다.
