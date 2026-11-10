# 스키마 설계

RideMate 백엔드의 테이블 설계다. 테이블 규칙(이름, 감사 컬럼, 삭제 방식)은 `database.md`가 정본이고, 이 문서는 그 규칙 위에서 어떤 테이블과 컬럼을 두는지를 적는다. 실제 DDL은 `backend/sql/`에 쌓는다.

- 범위: 화면 RM-V00~V11, RM-M01~M04 전부(후순위 화면 포함). 기능 명세서 v0.3 기준이다.
- 결정 이유는 [ADR-0008] 계정·개인정보·익명화, [ADR-0009] 정원 배정, [ADR-0010] 중복 방지 UNIQUE, [ADR-0011] 공간 타입에 있다.
- MySQL 8.4 이상을 전제로 한다. 로컬 Docker 이미지와 같은 버전이다.

## 공통

- 모든 테이블에 감사 컬럼 `CREATE_DT`·`CREATE_USER`·`UPDATE_DT`·`UPDATE_USER`·`DELETE_YN`을 둔다. 아래 표에서는 생략한다.
- PK는 `INT AUTO_INCREMENT`다.
- 코드 값 컬럼은 `VARCHAR(20)`에 영문 대문자 코드를 넣는다. 코드 목록은 각 표에 적는다.
- 점 하나로 충분한 위치(학원, 픽업 지점, 주차장, 픽업 후 목적지)는 `LAT`·`LNG` `DECIMAL(10,7)` 두 컬럼으로 둔다.
- 선·면이 필요한 값(구역 경계, 제외 구역, 도보 경로, 한 바퀴 경로)은 MySQL 공간 타입에 SRID 4326으로 둔다.
- 참조 컬럼에는 FK 제약을 건다. `ON DELETE`·`ON UPDATE` 절은 쓰지 않는다(기본 동작인 거부). 물리 삭제를 하지 않으므로 필요 없고, CHECK에 쓰인 컬럼에는 이 절을 붙일 수 없다.
- FK 컬럼의 인덱스는 InnoDB가 자동으로 만들게 두지 않고 DDL에 이름을 붙여 적는다. FK 컬럼이 맨 앞인 복합 인덱스나 UNIQUE가 있으면 그것으로 충분하므로 단독 인덱스를 또 만들지 않는다. 아래 표의 "인덱스"에는 FK 인덱스 말고 따로 필요한 것만 적는다.
- 제약과 인덱스 이름은 `FK_{테이블}_{nn}`, `UK_{테이블}_{nn}`, `IX_{테이블}_{nn}`, `CK_{테이블}_{nn}`으로 짓는다. `nn`은 테이블 안에서 01부터 붙인다.
- `DEFAULT`는 `DELETE_YN`에만 둔다. 나머지 NOT NULL 컬럼은 Service가 값을 채운다. 처음 넣는 값이 정해진 컬럼은 표의 설명에 "처음 값"으로 적는다.
- 구조 조건은 CHECK 제약으로 건다(범위, 함께 채워져야 하는 컬럼). 표에 `·`로 묶어 한 줄에 적은 컬럼 쌍 가운데 NULL을 허용하는 쌍에는 `(A IS NULL) = (B IS NULL)`을 건다(위도·경도, 시작·종료 시각, 추가 시간·요금, 빈자리 수·조회 시각). 그 밖의 CHECK는 각 표 아래에 적는다.
- 코드 값 목록과 코드 값에 따라 달라지는 조건은 CHECK로 걸지 않고 Service에서 확인한다. 코드가 늘 때마다 DDL을 바꾸지 않기 위해서다. 예외는 하나다. `TIMETABLE_EXCEPTION`의 `TIME_CHANGE` 조건은 CHECK로 건다.
- 시작·종료 시각 쌍은 자정을 넘기지 않는다. 자정을 넘는 구간은 두 행으로 나눠 넣는다.
- 트랜잭션 격리 수준은 MySQL 기본값인 REPEATABLE READ를 쓴다. 이 문서의 잠금 순서 설명은 이 수준을 전제로 한다.
- `LAT`·`LNG`로 공간 값을 만들 때는 `ST_GeomFromText(CONCAT('POINT(', LAT, ' ', LNG, ')'), 4326)` 하나만 쓴다. SRID 4326의 WKT는 위도, 경도 순서다.
- 비밀값(전화번호, 인증번호, 토큰)을 저장하는 방식은 셋이다.
  - `_ENC`: 암호화해서 저장한다. 원래 값을 다시 써야 할 때다(문자 발송).
  - `_HASH`: 서버 비밀키로 계산한 HMAC-SHA256(hex 64자)만 저장한다. 같은 값인지 찾거나 비교만 하면 될 때다.
  - 원문은 어디에도 저장하지 않는다. 키는 환경변수로 주입한다(`../security.md`).

### 살아 있는 행에만 거는 UNIQUE

삭제 표시된 행과 겹칠 수 있는 조합은 그대로 UNIQUE를 걸 수 없다. 대신 생성 컬럼 `ACTIVE_FLAG`를 두고 UNIQUE 키 끝에 넣는다.

```sql
ACTIVE_FLAG TINYINT GENERATED ALWAYS AS (IF(DELETE_YN = 0, 1, NULL)) VIRTUAL
```

삭제된 행은 `ACTIVE_FLAG`가 NULL이라 서로 겹쳐도 통과하고, 살아 있는 행끼리만 중복이 막힌다. 이 방식을 쓰는 테이블은 표에 "살아 있는 행 UNIQUE"로 적는다. [ADR-0010] 중복 키 오류는 Service에서 "이미 있음"으로 처리한다.

### 감사 컬럼에 넣는 값

`CREATE_USER`·`UPDATE_USER`에는 개인을 가리키는 값을 넣지 않는다. 역할만 넣는다.

| 값 | 쓰는 때 |
|---|---|
| `PARENT` | 차량 앱 요청 |
| `CHILD` | 모바일 웹(픽업 링크) 요청 |
| `SYSTEM` | 서버가 스스로 한 처리(배정, 만료, 익명화, 공공데이터 적재) |
| `ADMIN` | 운영팀이 넣은 기준 데이터 |

## 테이블 관계

```text
USER_INFO ──< CHILD ──< TIMETABLE ──< TIMETABLE_DAY ──< TIMETABLE_EXCEPTION
```

- 계정·인증: `USER_INFO`, `LOGIN_SESSION`, `PHONE_VERIFICATION`, `QR_LOGIN`, `CONSENT`
- 아이·시간표: `CHILD`, `ACADEMY`, `TIMETABLE`, `TIMETABLE_DAY`, `TIMETABLE_EXCEPTION`
- 기준 데이터: `AREA`, `PICKUP_SPOT`, `PICKUP_SPOT_BLOCK`, `ACADEMY_PICKUP_SPOT`, `LOOP_ROUTE`, `RESTRICTED_ZONE`, `PARKING_LOT`, `HOLIDAY`
- 픽업 진행: `PICKUP_PLAN`, `PICKUP_SESSION`, `PICKUP_EVENT`

## 계정·인증

운전자(부모)만 계정을 가진다. 아이 계정은 없다. 비밀번호는 없고 휴대폰 인증번호나 QR로 로그인한다. [ADR-0008] [ADR-0012]

### USER_INFO — 운전자 계정

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `USER_ID` | INT | N | PK |
| `PHONE_ENC` | VARBINARY(255) | N | 휴대폰 번호(숫자만). 보호자 인증번호를 보낼 때 쓴다 |
| `PHONE_HASH` | CHAR(64) | N | 휴대폰 번호의 HMAC. 로그인할 때 계정을 찾는다 |
| `USER_NAME` | VARCHAR(50) | N | 운전자 실명. 차량의 계정 목록(RM-V00)에는 성만 남기고 가려서(「김OO」) 보여준다 |
| `CALL_NAME` | VARCHAR(20) | Y | 아이에게 보이는 호칭(엄마, 아빠). 비어 있으면 화면은 "보호자"로 보여준다 |
| `CAR_MODEL` | VARCHAR(50) | Y | 차종 |
| `CAR_COLOR` | VARCHAR(20) | Y | 차 색 |
| `CAR_NO_LAST4` | CHAR(4) | Y | 차량 번호 뒷자리 |
| `DEST_ADDRESS` | VARCHAR(200) | Y | 픽업 후 목적지 주소. 내비의 "집"이나 검색해서 고른 곳 |
| `DEST_LAT`·`DEST_LNG` | DECIMAL(10,7) | Y | 픽업 후 목적지 위치 |
| `AUTO_SEND_YN` | TINYINT(1) | N | 링크 자동 발송 여부. 처음 값 0(매번 음성 확인) |
| `ARRIVAL_LOG_YN` | TINYINT(1) | N | 차량 도착 기록 사용 여부. 처음 값 0. 기록 자체는 차 안에만 있고 서버에 오지 않는다 |
| `WAKE_BEFORE_MIN` | TINYINT | N | 쉬는 모드에서 깨우는 시점(출발 몇 분 전, 0이면 출발 시각). 처음 값 5 |

- 살아 있는 행 UNIQUE: `(PHONE_HASH, ACTIVE_FLAG)`
- 차 정보와 호칭은 아이 화면에 보이는 값인데 명세서에 입력 화면이 없다. 입력처가 정해질 때까지 NULL 허용으로 둔다.
- 호칭은 자유 입력이다. 코드 값으로 묶지 않는다.
- 실명은 가입할 때 필수로 받는다. 입력 화면은 아직 명세서에 없다.
- `PHONE_ENC`는 AES-256-GCM 암호문이다. 키 버전 1바이트(`0x01`) + nonce 12바이트 + 암호문 + tag 16바이트를 이어 저장한다. [ADR-0013]
- 삭제(탈퇴)는 아래 "아이·계정 삭제" 순서를 따른다.

### LOGIN_SESSION — 로그인 세션

로그인 세션이다. 종류는 차량(`CAR`)과 휴대폰 웹(`WEB`) 둘이다. 같은 차를 엄마와 아빠가 번갈아 쓰면 계정마다 `CAR` 행이 한 개씩 생긴다. [ADR-0013]

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `LOGIN_SESSION_ID` | INT | N | PK |
| `USER_ID` | INT | N | FK → USER_INFO |
| `SESSION_TYPE` | VARCHAR(20) | N | `CAR`(차량 refresh 토큰), `WEB`(휴대폰 웹 쿠키). DB 기본값이 없다. 행을 넣을 때마다 Service가 정한다 |
| `TOKEN_HASH` | CHAR(64) | N | 로그인 토큰의 HMAC. UNIQUE |
| `PREV_TOKEN_HASH` | CHAR(64) | Y | 바꾸기 직전 refresh 토큰의 HMAC |
| `ROTATE_DT` | DATETIME | Y | 토큰을 마지막으로 바꾼 시각 |
| `DEVICE_ID` | VARCHAR(100) | N | 앱이 처음 실행할 때 만든 임의의 설치 번호. 차대번호가 아니다 |
| `EXPIRE_DT` | DATETIME | N | 만료 시각 |
| `LAST_USED_DT` | DATETIME | N | 마지막으로 쓰인 시각 |

- 이 테이블을 읽는 모든 조회에 `SESSION_TYPE` 조건을 붙인다. refresh와 계정 목록은 `CAR`만, 쿠키 인증은 `WEB`만 본다. 종류를 가리지 않는 것은 계정 삭제뿐이다.
- 로그아웃하면 `DELETE_YN`을 켠다. refresh 토큰은 `DELETE_YN = FALSE`이고 `EXPIRE_DT`가 지나지 않은 행만 통과시킨다.
- refresh 토큰을 쓰면 같은 행을 고친다. 지금 값이면 `PREV_TOKEN_HASH`에 지금 값을, `TOKEN_HASH`에 새 값을, `ROTATE_DT`에 지금을 넣는다. `PREV_TOKEN_HASH`와 같고 `ROTATE_DT`가 10분 안이면 `TOKEN_HASH`만 새 값으로 바꾸고 `PREV_TOKEN_HASH`·`ROTATE_DT`는 그대로 둔다. 둘 다 아니면 거절한다. 어느 쪽이든 `EXPIRE_DT`는 지금 + 90일, `LAST_USED_DT`는 지금으로 고치고, 이 처리는 그 행을 `FOR UPDATE`로 잠그고 한다.
- `WEB` 행은 `DEVICE_ID`가 빈 문자열이고 `PREV_TOKEN_HASH`·`ROTATE_DT`를 쓰지 않는다.
- 인덱스: `(USER_ID, DEVICE_ID)`, `(PREV_TOKEN_HASH)`

### PHONE_VERIFICATION — 휴대폰 인증번호

로그인과 보호자 확인에 같이 쓴다. 인증번호는 6자리, 3분 동안 유효하고, 5회 틀리면 10분 동안 잠근다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PHONE_VERIFICATION_ID` | INT | N | PK |
| `PURPOSE` | VARCHAR(20) | N | `LOGIN`(로그인), `GUARDIAN`(보호자 확인) |
| `PHONE_HASH` | CHAR(64) | N | 받는 번호의 HMAC |
| `CODE_HASH` | CHAR(64) | N | 인증번호의 HMAC |
| `EXPIRE_DT` | DATETIME | N | 만료 시각(발송 후 3분) |
| `FAIL_CNT` | TINYINT | N | 틀린 횟수. 처음 값 0 |
| `LOCK_UNTIL_DT` | DATETIME | Y | 잠금이 풀리는 시각. 5회 틀리면 채운다 |
| `VERIFY_DT` | DATETIME | Y | 인증에 성공한 시각 |

- 인증번호를 다시 받으면 새 행을 만든다. 확인할 때는 그 번호·용도의 가장 최근 행만 본다. `VERIFY_DT`가 채워진 행은 다시 통과시키지 않는다.
- 틀린 횟수와 잠금은 용도와 관계없이 번호 단위다. 새 행을 만들 때 그 번호의 직전 행이 인증되지 않았고 만든 지 10분이 지나지 않았으면 `FAIL_CNT`를 이어받는다. 다시 받는다고 횟수가 0으로 돌아가지 않는다.
- 잠금 중인지는 그 번호의 가장 최근 행에서 `LOCK_UNTIL_DT`로 판단한다. 잠긴 동안에는 새 인증번호를 보내지 않는다.
- 같은 번호로는 1분에 한 번만 보낸다(가정값).
- 인증에 성공했거나 만료된 지 24시간이 지난 행은 주기 작업이 `PHONE_HASH`·`CODE_HASH`를 빈 문자열로 바꾸고 `DELETE_YN`을 켠다. 동의 증빙에 필요한 시각과 방법은 `CONSENT`에 있다.
- `GUARDIAN`은 로그인한 운전자 계정의 번호로만 보낸다.
- 틀린 횟수를 올릴 때는 그 행을 `FOR UPDATE`로 잠그고 읽는다. 동시에 여러 번 시도해도 횟수가 빠지지 않는다.
- 인덱스: `(PHONE_HASH, PURPOSE, PHONE_VERIFICATION_ID)`

### QR_LOGIN — QR 로그인 요청

차량이 QR을 띄우고, 운전자가 휴대폰으로 찍어 인증하면 차량이 로그인된다. 요청을 만들 때 서버가 비밀 값 두 개를 만든다. `device_code`는 차량만 보관하고 결과를 기다릴 때 쓴다. `user_code`는 QR에 담겨 휴대폰이 인증할 때 쓴다. QR이 보여도 `device_code`가 없으면 로그인 토큰을 받을 수 없다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `QR_LOGIN_ID` | INT | N | PK |
| `DEVICE_CODE_HASH` | CHAR(64) | N | 차량이 보관하는 `device_code`의 HMAC. UNIQUE |
| `USER_CODE_HASH` | CHAR(64) | N | QR에 담긴 `user_code`의 HMAC. UNIQUE |
| `DEVICE_ID` | VARCHAR(100) | N | QR을 띄운 차량의 설치 번호 |
| `STATUS` | VARCHAR(20) | N | `PENDING`(대기), `APPROVED`(휴대폰에서 인증됨), `USED`(차량이 토큰을 받아감) |
| `USER_ID` | INT | Y | FK → USER_INFO. 인증된 뒤에 채운다 |
| `EXPIRE_DT` | DATETIME | N | 만료 시각 |

- 휴대폰은 `user_code`로 `PENDING` 행을 찾아 `APPROVED`로 바꾼다. 차량은 `device_code`로 그 행을 찾아 결과를 받는다.
- 두 UNIQUE는 삭제된 행까지 포함한다. 만든 값이 겹쳐 중복 키 오류가 나면 Service가 값을 새로 만들어 다시 넣는다.
- `PENDING`일 때만 `USER_ID`가 비어 있다. 코드 값 조건이라 CHECK로 걸지 않고 Service에서 확인한다.
- 차량이 `APPROVED`를 확인하면 `LOGIN_SESSION`을 만들고 `USED`로 바꾼다. 이 트랜잭션은 첫 문장에서 그 행을 `FOR UPDATE`로 잠그고 상태를 다시 읽는다. 이미 `USED`면 거절하므로 한 QR로 두 번 로그인할 수 없다.
- 토큰을 받아가는 요청의 `DEVICE_ID`가 행의 값과 같아야 한다. QR을 찍어 간 다른 기기는 로그인 토큰을 받을 수 없다.

### CONSENT — 동의 기록

보호자(법정대리인) 확인과 수집·이용 동의를 남긴다. 고치지 않고 쌓기만 한다. 동의를 철회하면 `AGREE_YN = 0`인 행을 새로 넣는다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `CONSENT_ID` | INT | N | PK |
| `USER_ID` | INT | N | FK → USER_INFO |
| `CONSENT_TYPE` | VARCHAR(20) | N | `GUARDIAN`(법정대리인 확인), `CHILD_INFO`(필수: 아이 이름·연락처), `ARRIVAL_LOG`(선택: 차량 도착 기록) |
| `AGREE_YN` | TINYINT(1) | N | 동의 여부 |
| `TERMS_VERSION` | VARCHAR(20) | N | 동의한 약관·고지 문서의 버전 |
| `VERIFY_METHOD` | VARCHAR(20) | N | 확인 방법. `PHONE_OTP`(휴대폰 인증), `CHECK`(체크박스) |
| `PHONE_VERIFICATION_ID` | INT | Y | FK → PHONE_VERIFICATION. 휴대폰 인증으로 확인한 경우 |
| `CONSENT_DT` | DATETIME | N | 동의한 시각 |

- 지금 동의 상태는 그 계정·종류의 가장 최근 행이다. 이 테이블이 정본이다. `USER_INFO.ARRIVAL_LOG_YN`은 화면이 빨리 읽기 위한 사본이고 `ARRIVAL_LOG` 행을 넣는 트랜잭션에서 함께 바꾼다.
- 인덱스: `(USER_ID, CONSENT_TYPE, CONSENT_ID)`

## 아이·시간표

### CHILD — 아이

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `CHILD_ID` | INT | N | PK |
| `USER_ID` | INT | N | FK → USER_INFO |
| `CHILD_NAME` | VARCHAR(50) | N | 이름(화면 입력은 1~10자) |
| `PHONE_ENC` | VARBINARY(255) | N | 휴대폰 번호(숫자만). 암호화해서 저장한다 |
| `SEND_CHANNEL` | VARCHAR(20) | N | `SMS`, `ALIMTALK`. 처음 값 `SMS` |

- 부모 한 명이 아이를 여러 명 둘 수 있는 구조다. 명세서의 화면은 아이 1명 흐름이다.
- 아이 번호가 부모 번호와 같으면 등록을 거절한다. 복호화한 값끼리 Service에서 비교한다.
- 화면에 내려줄 때는 Service에서 복호화한 뒤 가운데 자리를 가린다.
- 삭제는 아래 "아이·계정 삭제" 순서를 따른다. 복구는 할 수 없다.

### ACADEMY — 학원

지원 학원가 안의 학원만 있다. 여러 부모가 같은 행을 가리킨다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `ACADEMY_ID` | INT | N | PK |
| `AREA_ID` | INT | N | FK → AREA |
| `ACADEMY_NAME` | VARCHAR(100) | N | 원천 데이터나 검색 결과의 이름 |
| `ADDRESS` | VARCHAR(200) | Y | 주소 |
| `LAT`·`LNG` | DECIMAL(10,7) | N | 위치 |
| `SOURCE_TYPE` | VARCHAR(20) | N | `TEAM`(운영팀), `NEIS`(학원·교습소 표준데이터), `PLACE`(장소 검색으로 추가) |
| `PLACE_ID` | VARCHAR(100) | Y | 장소 검색 API의 장소 ID |
| `SOURCE_REF` | VARCHAR(100) | Y | 원천 데이터의 학원 관리번호 |
| `OPEN_STATUS` | VARCHAR(20) | Y | 원천 데이터의 등록 상태. `OPEN`(운영), `CLOSED`(휴원·폐원) |

- 부모는 검색 결과에서만 학원 위치를 고른다. 고른 위치가 `AREA.BOUNDARY` 밖이면 등록을 거절한다.
- 학원 검색은 서버가 먼저 이 테이블에서 찾고(운영팀·표준데이터 학원), 그 뒤에 장소 검색 API 결과를 붙여 돌려준다. 이 테이블의 학원은 `ACADEMY_ID`로 고른다.
- 장소 검색 결과를 고르면 `PLACE_ID`가 같은 행이 있는지 먼저 본다. 없으면 이름이 같고 위치가 50m 안(가정값)인 행을 찾아 그 행에 `PLACE_ID`를 채워 쓴다. 그래도 없을 때만 `PLACE`로 새 행을 만든다. 운영팀이 검증한 학원이 검색 결과 때문에 둘로 갈리지 않게 한다.
- 부모가 지은 학원 이름은 여기가 아니라 `TIMETABLE.DISPLAY_NAME`에 둔다.
- 살아 있는 행 UNIQUE: `(PLACE_ID, ACTIVE_FLAG)`, `(SOURCE_REF, ACTIVE_FLAG)`. 값이 NULL인 행은 걸리지 않는다.

### TIMETABLE — 시간표

시간표 1건은 아이, 학원, 하원 시각이다. 요일은 `TIMETABLE_DAY`에 둔다. 편집·삭제·등록 개수 제한이 이 단위다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `TIMETABLE_ID` | INT | N | PK |
| `CHILD_ID` | INT | N | FK → CHILD |
| `ACADEMY_ID` | INT | N | FK → ACADEMY |
| `DISPLAY_NAME` | VARCHAR(20) | N | 부모가 지은 학원 이름. 화면에는 이 이름을 보여준다 |
| `END_TIME` | TIME | N | 하원 시각 |

- CHECK: `END_TIME BETWEEN '14:00:00' AND '23:30:00'`, `MINUTE(END_TIME) % 5 = 0 AND SECOND(END_TIME) = 0`
- 등록 개수 제한(가정 10건)과 같은 요일 ±40분 겹침 경고는 Service에서 한다.
- 진행 중인 픽업 계획이 있는 시간표는 삭제하지 않고, 학원·하원 시각을 바꾸거나 그 계획의 요일을 빼는 편집도 거절한다. 오늘 하원 시각만 바꾸는 것은 `TIMETABLE_EXCEPTION`으로 한다.
- 편집으로 요일을 뺐다가 다시 넣으면 새 행을 만들지 않고 삭제 표시된 `TIMETABLE_DAY` 행을 되살린다. `LAST_PICKUP_DATE`가 이어진다.
- 시간표를 지우고 같은 내용으로 다시 등록하면 새 시간표다. 그날 이미 탑승으로 끝났어도 계획이 다시 만들어질 수 있다. 감수한다.

### TIMETABLE_DAY — 시간표의 요일

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `TIMETABLE_DAY_ID` | INT | N | PK |
| `TIMETABLE_ID` | INT | N | FK → TIMETABLE |
| `DAY_OF_WEEK` | TINYINT | N | 1(월)~7(일) |
| `PICKUP_YN` | TINYINT(1) | N | 그 요일의 픽업 스위치. 처음 값 1 |
| `LAST_PICKUP_DATE` | DATE | Y | 탑승이나 만료로 끝난 마지막 픽업 날짜 |

- 살아 있는 행 UNIQUE: `(TIMETABLE_ID, DAY_OF_WEEK, ACTIVE_FLAG)`
- CHECK: `DAY_OF_WEEK BETWEEN 1 AND 7`
- 스위치가 켜진 행마다 그 요일에 픽업이 하나씩 생긴다. 하루에 여러 건이 될 수 있다.
- `LAST_PICKUP_DATE`가 만들려는 계획의 `PICKUP_DATE`와 같으면 계획을 다시 만들지 않는다. 끝난 계획은 이 행을 가리키는 참조가 비워져 UNIQUE로는 막을 수 없기 때문이다. 취소로 끝난 계획은 이 값을 바꾸지 않으므로 다시 시작할 수 있다.

### TIMETABLE_EXCEPTION — 날짜별 예외

시간표를 바꾸지 않고 특정 날짜만 다르게 한다. "오늘은 픽업 안 해"와 "오늘 하원 시각 변경"이 여기에 쌓인다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `TIMETABLE_EXCEPTION_ID` | INT | N | PK |
| `TIMETABLE_DAY_ID` | INT | N | FK → TIMETABLE_DAY |
| `TARGET_DATE` | DATE | N | 적용 날짜 |
| `EXCEPTION_TYPE` | VARCHAR(20) | N | `SKIP`(그날 건너뜀), `TIME_CHANGE`(하원 시각 변경) |
| `CHANGED_END_TIME` | TIME | Y | `TIME_CHANGE`일 때 바뀐 하원 시각 |

- 살아 있는 행 UNIQUE: `(TIMETABLE_DAY_ID, TARGET_DATE, ACTIVE_FLAG)`. 다시 바꾸면 그 행을 수정한다.
- CHECK: `(EXCEPTION_TYPE = 'TIME_CHANGE') = (CHANGED_END_TIME IS NOT NULL)`
- 바뀐 하원 시각의 범위(기존 시각의 2시간 전부터 3시간 뒤까지)는 Service에서 확인한다.

### 아이·계정 삭제

한 트랜잭션에서 아래 순서로 한다. 아이만 삭제할 때는 그 아이에 대해 0~5만 한다. 시간표 한 건을 삭제할 때도 0번의 잠금부터 한다.

비우는 값과 `DELETE_YN` 켜기는 한 행에 대해 한 UPDATE로 한다. 잠금은 Dapper로 먼저 잡는다. EF 저장 한 번에 여러 행을 맡기면 UPDATE 순서가 정해지지 않는다.

| 순서 | 대상 | 처리 |
|---|---|---|
| 0 | `AREA` 잠금 | 트랜잭션의 첫 문장으로, 그 아이들의 시간표가 가리키는 학원가의 `AREA` 행을 `AREA_ID` 순서로 `FOR UPDATE` 잠근다. 계획 생성이 같은 잠금을 기다리므로 삭제 도중에 새 계획이 끼어들지 못한다 |
| 1 | 진행 중인 `PICKUP_PLAN` | 잠금 조회(`FOR UPDATE`)로 찾아 `PICKUP_PLAN_ID` 순서로 취소한다(상태 바꾸기와 익명화 절차 그대로). 보낸 링크도 함께 끝난다 |
| 2 | `TIMETABLE_EXCEPTION` | `DELETE_YN`을 켠다 |
| 3 | `TIMETABLE_DAY` | `DELETE_YN`을 켠다 |
| 4 | `TIMETABLE` | `DELETE_YN`을 켜고 `DISPLAY_NAME`을 빈 문자열로 바꾼다 |
| 5 | `CHILD` | `DELETE_YN`을 켜고 `CHILD_NAME`을 빈 문자열로, `PHONE_ENC`를 빈 값으로 바꾼다 |
| 6 | `LOGIN_SESSION`, `QR_LOGIN` | `DELETE_YN`을 켠다. `QR_LOGIN`은 `USER_ID`를 NULL로 바꾼다 |
| 7 | `PHONE_VERIFICATION` | 그 번호의 행에서 `PHONE_HASH`·`CODE_HASH`를 빈 문자열로 바꾸고 `DELETE_YN`을 켠다. 남겨 두면 `CONSENT`를 거쳐 삭제된 계정의 번호 해시로 이어진다 |
| 8 | `USER_INFO` | `DELETE_YN`을 켜고 `PHONE_ENC`를 빈 값으로, `PHONE_HASH`·`USER_NAME`을 빈 문자열로, 호칭·차 정보·목적지를 NULL로 바꾼다 |

- `CONSENT`는 지우지 않는다. 동의를 받았다는 증빙이다. 보관 기간은 정해지지 않았다.
- 로그아웃은 삭제가 아니다. 그 차량의 `LOGIN_SESSION`만 끝내고 서버의 시간표와 아이 정보는 남는다.

## 기준 데이터

운영팀이 넣거나 서버가 공공데이터에서 적재한다. 관리 화면은 없다. 차량은 공공데이터를 직접 부르지 않는다.

### AREA — 학원가(서비스 지역)

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `AREA_ID` | INT | N | PK |
| `AREA_NAME` | VARCHAR(50) | N | 이름(예: 대치동) |
| `BOUNDARY` | POLYGON SRID 4326 | N | 구역 경계. SPATIAL INDEX |

### PICKUP_SPOT — 픽업 지점

현장 검증을 마친 지점만 넣는다. 제외 기준에 걸리는 곳은 넣지 않는다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PICKUP_SPOT_ID` | INT | N | PK |
| `AREA_ID` | INT | N | FK → AREA |
| `SPOT_NAME` | VARCHAR(100) | N | 이름(예: ○○ 앞) |
| `LAT`·`LNG` | DECIMAL(10,7) | N | 위치 |
| `GUIDE_TEXT` | VARCHAR(200) | Y | 찾는 법(예: 편의점 간판 옆) |
| `PHOTO_URL` | VARCHAR(500) | Y | 현장 사진 주소 |
| `CAPACITY` | TINYINT | N | 같은 시각대에 받을 수 있는 차 수 |
| `USE_YN` | TINYINT(1) | N | 배정에 쓰는지 여부. 처음 값 1 |

- CHECK: `CAPACITY >= 1`

### PICKUP_SPOT_BLOCK — 픽업 지점을 쓸 수 없는 시간

주정차 금지 시간과 현장에서 확인한 안내판 시간을 지점별로 가공해 넣는다. 그 시각에 걸리는 지점은 배정하지 않는다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PICKUP_SPOT_BLOCK_ID` | INT | N | PK |
| `PICKUP_SPOT_ID` | INT | N | FK → PICKUP_SPOT |
| `DAY_TYPE` | VARCHAR(20) | N | `WEEKDAY`(평일), `SAT`(토요일), `HOLIDAY`(일요일·공휴일) |
| `START_TIME` | TIME | N | 금지 시작 시각 |
| `END_TIME` | TIME | N | 금지 종료 시각 |
| `REASON` | VARCHAR(100) | Y | 사유(예: 황색 실선 안내판) |
| `SOURCE_REF` | VARCHAR(100) | Y | 원천 데이터의 관리번호. 현장 조사면 NULL |

- 오늘이 어느 `DAY_TYPE`인지는 요일과 `HOLIDAY` 테이블로 정한다.
- 원천 데이터를 지점별로 가공한 결과라 다시 적재할 때는 그 지점의 `SOURCE_REF`가 있는 행을 모두 삭제 표시하고 새로 넣는다.
- 인덱스: `(PICKUP_SPOT_ID, DAY_TYPE)`

### ACADEMY_PICKUP_SPOT — 학원별 픽업 지점 후보

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `ACADEMY_PICKUP_SPOT_ID` | INT | N | PK |
| `ACADEMY_ID` | INT | N | FK → ACADEMY |
| `PICKUP_SPOT_ID` | INT | N | FK → PICKUP_SPOT |
| `WALK_MIN` | TINYINT | N | 학원에서 지점까지 아이 도보 시간(분) |
| `WALK_PATH` | LINESTRING SRID 4326 | Y | 도보 경로 선 |
| `PRIORITY` | TINYINT | N | 배정 우선순위. 1이 가장 먼저 |

- 학원 행을 새로 만들 때 같은 `AREA`의 지점 중 가까운 순으로 최대 3곳의 행을 함께 만든다. `PRIORITY`는 가까운 순으로 1부터 붙인다. 도보 시간과 경로는 보행자 경로 API로 구하고, 실패하면 직선 거리를 설정값인 도보 속도로 나눠 올림하고 `WALK_PATH`는 비운다.
- 학원과 지점은 같은 `AREA`여야 한다. 제약으로는 걸 수 없어, 운영팀이 넣을 때는 SQL에서, 학원을 추가할 때는 Service에서 확인한다.
- 살아 있는 행 UNIQUE: `(ACADEMY_ID, PICKUP_SPOT_ID, ACTIVE_FLAG)`
- 인덱스: `(ACADEMY_ID, PRIORITY)`

### LOOP_ROUTE — 한 바퀴 경로

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `LOOP_ROUTE_ID` | INT | N | PK |
| `PICKUP_SPOT_ID` | INT | N | FK → PICKUP_SPOT |
| `DURATION_SEC` | SMALLINT | N | 한 바퀴 소요 시간(초) |
| `DISTANCE_M` | SMALLINT | N | 거리(m) |
| `RIGHT_TURN_CNT` | TINYINT | N | 우회전 수 |
| `ROUTE_PATH` | LINESTRING SRID 4326 | N | 경로 선 |

### RESTRICTED_ZONE — 제외·단속 구역

지도의 빨간 영역 표시와 정차 위치 경고에 쓴다. 픽업 지점을 고르는 1차 필터의 근거이기도 하다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `RESTRICTED_ZONE_ID` | INT | N | PK |
| `AREA_ID` | INT | N | FK → AREA. 이 구역이 속한 학원가. 차량에는 학원가 단위로 내려준다 |
| `ZONE_TYPE` | VARCHAR(20) | N | 아래 코드 |
| `GEOM` | GEOMETRY SRID 4326 | N | 점·선·면. SPATIAL INDEX |
| `BUFFER_M` | SMALLINT | N | `GEOM`에서 이 거리(m) 안까지 제외한다 |
| `DAY_TYPE` | VARCHAR(20) | Y | 적용 요일. `WEEKDAY`, `SAT`, `HOLIDAY`. NULL이면 매일 |
| `ACTIVE_START_TIME`·`ACTIVE_END_TIME` | TIME | Y | 적용 시각. NULL이면 종일 |
| `SOURCE_NAME` | VARCHAR(50) | Y | 원천 데이터 이름 |
| `SOURCE_REF` | VARCHAR(100) | Y | 원천 데이터의 관리번호 |

| `ZONE_TYPE` | 모양 | `BUFFER_M` | 출처 |
|---|---|---|---|
| `SCHOOL_ZONE`(어린이보호구역) | 면, 또는 시설 위치 점 | 면은 0, 점은 300(가정) | 어린이보호구역 표준데이터 |
| `NO_PARKING`(주정차 금지 구역) | 선 또는 점 | 미정 | 주정차금지구역 표준데이터 |
| `INTERSECTION`(교차로 모퉁이) | 점 | 5 | 현장 조사 |
| `CROSSWALK`(횡단보도) | 점 | 10 | 횡단보도 표준데이터 |
| `BUS_STOP`(버스정류장) | 점 | 10 | 버스정류장 위치정보 |
| `FIRE_HYDRANT`(소화전) | 점 | 5 | 데이터셋 미확인. 없으면 현장 조사 |
| `YELLOW_DOUBLE`(황색 복선) | 선 | 미정 | 현장 조사 |
| `YELLOW_SINGLE`(황색 실선) | 선 | 미정 | 현장 조사 |
| `CCTV`(단속 카메라) | 점 | 미정 | 서울시 단속 CCTV 위치정보 |
| `ACCIDENT_SPOT`(보행어린이 사고 다발지) | 면 | 0 | 도로교통공단 |
| `RESIDENT_PARKING`(거주자 우선 주차구역) | 선 또는 면 | 미정 | 현장 조사 |

- `BUFFER_M`이 0이어도 되는 것은 면뿐이다. 면은 안쪽의 거리가 0이라 그대로 걸린다. 점과 선은 0이면 좌표가 정확히 겹칠 때만 걸리므로 양수로 넣는다. 모양에 따라 달라지는 조건이라 CHECK로 걸지 않고 적재하는 SQL에서 확인한다.
- CHECK: `BUFFER_M >= 0`
- 요일이나 시각이 여러 구간이면 행을 구간마다 만든다.
- "이 위치가 제외 구역인가"는 두 단계로 조회한다. 먼저 위치 둘레의 사각형과 `MBRIntersects`로 후보를 추리고, 그 후보에만 `ST_Distance(GEOM, 점) <= BUFFER_M`을 적용한다. 거리 조건만 쓰면 공간 인덱스를 타지 않고 모든 행을 계산한다.
- 사각형도 SRID 4326으로 만든다. SRID가 다르면 오류가 난다. 가장 큰 `BUFFER_M`(m)을 도 단위로 바꿔 넓힌다. 위도는 `m / 111320`, 경도는 `m / (111320 × cos(위도))`다.
- 지원 학원가 둘레의 데이터만 넣는다. 전국 데이터를 넣지 않는다.
- 살아 있는 행 UNIQUE: `(SOURCE_NAME, SOURCE_REF, ACTIVE_FLAG)`. 공공데이터를 다시 적재할 때 이 값으로 같은 구역을 찾아 덮어쓴다. 현장 조사로 넣은 행은 `SOURCE_REF`가 NULL이라 걸리지 않는다.

### PARKING_LOT — 대기 장소(주차장)

전국주차장정보표준데이터의 컬럼 구성을 따른다. 학원가 반경 1km 안의 주차장만 넣는다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PARKING_LOT_ID` | INT | N | PK |
| `AREA_ID` | INT | N | FK → AREA. 이 주차장을 추천하는 학원가 |
| `LOT_NAME` | VARCHAR(100) | N | 이름 |
| `ADDRESS` | VARCHAR(200) | Y | 주소 |
| `LAT`·`LNG` | DECIMAL(10,7) | N | 위치 |
| `WEEKDAY_OPEN_TIME`·`WEEKDAY_CLOSE_TIME` | TIME | Y | 평일 운영 시간 |
| `SAT_OPEN_TIME`·`SAT_CLOSE_TIME` | TIME | Y | 토요일 운영 시간 |
| `HOLIDAY_OPEN_TIME`·`HOLIDAY_CLOSE_TIME` | TIME | Y | 일요일·공휴일 운영 시간 |
| `BASE_MIN` | SMALLINT | N | 기본 시간(분) |
| `BASE_FEE` | INT | N | 기본 요금(원) |
| `ADD_UNIT_MIN`·`ADD_FEE` | SMALLINT, INT | Y | 추가 단위 시간(분)과 요금(원) |
| `DAY_PASS_FEE` | INT | Y | 1일 주차권 요금(원) |
| `TOTAL_SPACES` | SMALLINT | Y | 주차 면수 |
| `REALTIME_REF` | VARCHAR(100) | Y | 실시간 주차대수 API의 주차장 코드. 없으면 화면은 "자리 정보 없음" |
| `AVAILABLE_SPACES`·`AVAILABLE_UPDATE_DT` | SMALLINT, DATETIME | Y | 마지막으로 조회한 빈자리 수와 조회 시각 |
| `SOURCE_REF` | VARCHAR(100) | Y | 표준데이터의 주차장 관리번호 |

- 추천할 때 저장된 빈자리가 5분 안의 값이면 그대로 쓰고, 아니면 실시간 API를 조회해 덮어쓴다. API가 실패하면 저장된 값을 쓴다.
- 빈자리 갱신은 조회 시각이 저장된 `AVAILABLE_UPDATE_DT`보다 나중일 때만 한다. 늦게 도착한 응답이 최신 값을 덮지 않게 한다.
- 빈자리 갱신은 `UPDATE_DT`·`UPDATE_USER`를 바꾸지 않는다. 감사 컬럼은 기준 데이터(요금, 운영 시간)를 고친 기록으로만 쓴다.
- 살아 있는 행 UNIQUE: `(SOURCE_REF, ACTIVE_FLAG)`. 공공데이터를 다시 적재할 때 이 값으로 같은 주차장을 찾아 덮어쓴다.

### HOLIDAY — 공휴일

특일 정보를 연 1회 적재한다. 주차장 운영 시간, 픽업 지점 금지 시간, 제외 구역 적용 요일을 정할 때와 "오늘도 학원 가나요?" 확인에 쓴다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `HOLIDAY_ID` | INT | N | PK |
| `HOLIDAY_DATE` | DATE | N | 날짜 |
| `HOLIDAY_NAME` | VARCHAR(50) | N | 이름 |

- 살아 있는 행 UNIQUE: `(HOLIDAY_DATE, ACTIVE_FLAG)`

## 픽업 진행

명세서의 "오늘 계획"이 `PICKUP_PLAN`, "세션"이 `PICKUP_SESSION`이다. 계획은 배정과 차량의 진행 상태를 갖고, 세션은 아이에게 보낸 링크 한 건이다.

### PICKUP_PLAN — 오늘 계획

차량이 오늘 계획을 처음 조회할 때(추천 출발 30분 전부터) 만든다. 그때 픽업 지점과 도착 시각을 배정하고 정원을 잡는다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PICKUP_PLAN_ID` | INT | N | PK |
| `USER_ID` | INT | Y | FK → USER_INFO. 끝나면 NULL |
| `CHILD_ID` | INT | Y | FK → CHILD. 끝나면 NULL |
| `TIMETABLE_DAY_ID` | INT | Y | FK → TIMETABLE_DAY. 끝나면 NULL |
| `ACADEMY_ID` | INT | Y | FK → ACADEMY. 끝나면 NULL |
| `AREA_ID` | INT | N | FK → AREA. 학원의 학원가를 복사해 둔다. 끝나도 남긴다 |
| `PICKUP_DATE` | DATE | N | 픽업 날짜 |
| `END_TIME` | TIME | Y | 그날 적용된 하원 시각. 끝나면 NULL |
| `PICKUP_SPOT_ID` | INT | Y | FK → PICKUP_SPOT. 배정 지점. 쓸 수 있는 지점이 없으면 NULL |
| `SLOT_DT` | DATETIME | Y | 배정된 도착 시각(분 단위) |
| `WALK_MIN` | TINYINT | Y | 배정 지점까지 아이 도보 시간(분). 끝나면 NULL |
| `EXPIRE_DT` | DATETIME | N | 만료 시각. `SLOT_DT`(없으면 그날 하원 시각)에 설정값(기본 1시간)을 더한다. 재배정하거나 하원 시각이 바뀌면 다시 계산한다 |
| `STATUS` | VARCHAR(20) | N | 아래 코드 |
| `PARKING_LOT_ID` | INT | Y | FK → PARKING_LOT. 대기 장소를 골랐을 때 |
| `START_DT` | DATETIME | Y | 경로를 시작한 시각 |
| `WAIT_START_DT` | DATETIME | Y | 대기 장소 도착 시각 |
| `STOP_DT` | DATETIME | Y | 픽업 지점에 처음 정차한 시각. 한 바퀴 돌고 다시 서도 바꾸지 않는다 |
| `BOARD_DT` | DATETIME | Y | 탑승 감지 시각 |
| `CLOSE_DT` | DATETIME | Y | 계획이 끝난 시각 |

| `STATUS` | 뜻 |
|---|---|
| `PLANNED` | 배정됨, 출발 전 |
| `STARTED` | 경로 시작, 픽업 지점으로 이동 중 |
| `WAITING` | 대기 장소에서 대기 중 |
| `STOPPED` | 픽업 지점 정차, 아이를 기다리는 중 |
| `LOOPING` | 한 바퀴 도는 중 |
| `BOARDED` | 탑승 완료 |
| `EXPIRED` | 탑승 감지 없이 `EXPIRE_DT`가 지남 |
| `CANCELED` | 부모가 취소했거나 시간표·아이가 삭제됨 |

- 아이 화면의 "곧 도착"(도착 1분 전)은 상태로 저장하지 않는다. 배정 시각과 현재 시각으로 계산한다.
- UNIQUE: `(TIMETABLE_DAY_ID, PICKUP_DATE)`. 진행 중에 같은 계획을 두 번 만들 수 없다. 끝난 계획은 `TIMETABLE_DAY_ID`가 NULL이라 걸리지 않으므로 `ACTIVE_FLAG`가 필요 없다. 중복 키 오류가 나면 이미 만들어진 계획을 돌려준다.
- 탑승이나 만료로 끝난 뒤 다시 만드는 것은 `TIMETABLE_DAY.LAST_PICKUP_DATE`로 막는다.
- CHECK: `(PICKUP_SPOT_ID IS NULL) = (SLOT_DT IS NULL)`, `(PICKUP_SPOT_ID IS NULL) OR (WALK_MIN IS NOT NULL) OR (DELETE_YN = 1)`
- 인덱스: `(PICKUP_SPOT_ID, SLOT_DT)`, `(USER_ID, PICKUP_DATE)`
- 상태, 시각 컬럼, `PICKUP_EVENT`는 같은 일을 함께 적는다. 셋은 항상 같은 트랜잭션에서 쓴다.

**배정 시각.** 아이가 픽업 지점에 도착할 것으로 보는 시각이다. `하원 시각 + 정리 시간 + WALK_MIN`으로 계산한다. 정리 시간은 설정값이다(가정 1분).

**배정.** 같은 지점에서 배정 시각 앞뒤로 설정값(기본 2분) 안에 있는 진행 중 계획의 수가 `PICKUP_SPOT.CAPACITY`를 넘지 않게 한다. 배정과 재배정은 아래 순서를 한 트랜잭션에서 지킨다. [ADR-0009]

1. 트랜잭션의 첫 문장으로 그 학원가의 `AREA` 행을 잠근다: `SELECT AREA_ID FROM AREA WHERE AREA_ID = ? FOR UPDATE`. 이 문장보다 먼저 다른 조회를 하지 않는다. 먼저 조회하면 그 시점의 스냅샷이 고정되어, 잠금을 얻고도 직전에 커밋된 계획을 보지 못한다.
2. 새 계획을 만들 때는 `TIMETABLE_DAY` 행을 `FOR UPDATE`로 읽어 삭제되지 않았는지, `PICKUP_YN`이 켜져 있는지, `LAST_PICKUP_DATE`가 그날이 아닌지 확인한다. 잠그지 않고 읽으면 탑승 처리나 삭제와 겹칠 때 계획이 잘못 만들어진다.
3. `ACADEMY_PICKUP_SPOT.PRIORITY` 순으로 후보 지점을 본다. `USE_YN`이 꺼졌거나 그 시각이 `PICKUP_SPOT_BLOCK`에 걸리는 지점은 건너뛴다.
4. 후보 지점에서 `SLOT_DT`가 배정 시각의 앞뒤 간격 안에 있는 진행 중 계획을 세어 정원과 비교한다. 꽉 찼으면 배정 시각을 1분씩 늦춰 다시 보고(설정값까지, 가정 2분), 그래도 없으면 다음 지점으로 넘긴다.
5. 모든 후보가 안 되면 `PICKUP_SPOT_ID`와 `SLOT_DT`를 비운 채 계획을 만든다. 화면은 "가까운 픽업 장소를 찾지 못했어요"를 보여준다.
6. 계획을 넣거나 고치고 커밋한다.

- 일반 조회로 세고 넣으면 동시에 들어온 두 요청이 서로의 삽입을 보지 못해 정원을 넘는다. 그래서 `AREA` 행 잠금으로 한 학원가의 배정을 순서대로 처리한다.
- 잠금을 쥔 동안 문자 발송이나 경로 조회 같은 외부 호출을 하지 않는다.
- 잠금 조회는 Dapper로 쓴다.
- 출발하지 않은 `PLANNED` 계획도 정원을 잡고 있다. `EXPIRE_DT`가 지나면 만료 처리로 풀린다.
- 추천 출발 시각이 지나 늦게 출발하면 배정 시각을 뒤로 옮겨 재배정한다. 하원 시각을 오늘만 바꿔도 재배정한다.

**상태 바꾸기와 이벤트 넣기.** 계획의 상태를 바꾸거나 `PICKUP_EVENT`를 넣는 트랜잭션은 첫 문장에서 계획 행을 잠그고 현재 상태를 다시 읽는다: `SELECT … WHERE PICKUP_PLAN_ID = ? FOR UPDATE`. 이 조회에는 `DELETE_YN` 조건을 붙이지 않는다. 이미 끝난 계획이면 아무것도 하지 않는다. 예외는 둘이다. 계획을 만드는 트랜잭션(`PLANNED` 이벤트)은 잠글 계획 행이 아직 없어 배정 절차의 `AREA` 잠금 안에서 한다. 아이·계정·시간표 삭제는 `AREA` 행을 먼저 잠근 뒤 계획 행들을 잠근다.

- 만료·탑승·취소가 겹쳐도 먼저 잠근 쪽만 적용된다.
- 한 계획의 이벤트가 번호 순서대로 커밋된다. 잠그지 않으면 번호가 작은 이벤트가 늦게 커밋되어 화면이 놓칠 수 있다.
- 재배정은 `AREA` 행을 먼저 잠그고 그다음 계획 행을 잠근다. 순서를 바꾸지 않는다.
- 계획을 끝낼 때는 계획 행을 잠근 직후, 계획 행을 수정하기 전에 `TIMETABLE_DAY` 행을 잠근다. 그다음 계획을 수정하고, 탑승이나 만료로 끝나는 경우 `LAST_PICKUP_DATE`를 그 계획의 `PICKUP_DATE`로 바꾼다. 계획 행을 먼저 수정하면 같은 시간표로 계획을 만들려는 요청과 서로 기다릴 수 있다.
- 계획을 끝낼 때 `PICKUP_SESSION.CLOSE_DT`도 채운다.
- `UPDATE_USER`에는 그 변경을 일으킨 쪽의 역할을 넣는다. 부모가 취소했으면 `PARENT`, 만료 처리면 `SYSTEM`이다.

**만료 처리.** 주기 작업이 `EXPIRE_DT`가 지난 진행 중 계획을 찾아 `EXPIRED`로 끝낸다. 주기는 설정값이다.

**익명화.** 계획이 `BOARDED`·`EXPIRED`·`CANCELED`가 되면 같은 트랜잭션에서 아래를 한다. [ADR-0008]

1. `USER_ID`·`CHILD_ID`·`TIMETABLE_DAY_ID`·`ACADEMY_ID`·`END_TIME`·`WALK_MIN`을 NULL로 바꾼다.
2. 그 계획의 `END_TIME_CHANGED` 이벤트는 `DETAIL`을 NULL로 바꾼다. 하원 시각이 들어 있기 때문이다.
3. `DELETE_YN`을 켠다. `PICKUP_SESSION`·`PICKUP_EVENT`도 함께 켠다.

계획 행의 1과 3은 한 UPDATE로 한다. 나눠서 하면 `WALK_MIN`을 비우는 순간 CHECK에 걸린다.

끝난 뒤 남는 값은 `AREA_ID`, `PICKUP_DATE`, `PICKUP_SPOT_ID`, `SLOT_DT`, `EXPIRE_DT`, `STATUS`, `PARKING_LOT_ID`, 시각 컬럼 다섯, 그리고 이벤트 이력이다.

- 학원 참조와 하원 시각을 남기면 작은 학원은 날짜와 하원 시각만으로 아이가 좁혀진다. 그래서 비운다.
- 남는 픽업 지점과 배정 시각으로 그 지점을 쓰는 학원들까지는 좁힐 수 있다. 지점 하나를 여러 학원이 함께 쓰므로 아이 한 명으로 이어지지는 않는다고 보고 남긴다. [ADR-0008]

**픽업 계열 세 테이블의 `DELETE_YN`.** `PICKUP_PLAN`·`PICKUP_SESSION`·`PICKUP_EVENT`에서 `DELETE_YN`은 "끝나서 익명화됨"을 뜻한다. 이 세 테이블의 조회에는 `DELETE_YN = FALSE` 조건을 붙이지 않고, 진행 중인지는 계획의 `STATUS`로 가린다(세션은 `CLOSE_DT`와 계획의 `EXPIRE_DT`). 끝난 계획의 마지막 이벤트(탑승, 만료, 취소)를 화면이 읽어야 하고, 끝난 링크를 다시 열었을 때 안내해야 하고, 통계가 끝난 행을 읽어야 하기 때문이다. 끝난 계획은 차량에 다시 열어 주지 않는다. 부모 참조가 비워져 누구의 계획인지 확인할 수 없기 때문이다. 차량은 계획을 끝낸 요청의 응답으로 결과를 받고, 서버가 만료시킨 경우는 진행 중 계획이 없다는 것과 `TIMETABLE_DAY.LAST_PICKUP_DATE`로 안다. 아이에게는 링크 토큰으로만 열리고 끝났다는 상태만 돌려준다.

### PICKUP_SESSION — 픽업 세션(아이에게 보낸 링크)

계획 하나에 세션 하나다. 링크를 처음 보낼 때 만든다. 다시 알릴 때도 같은 링크를 보낸다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PICKUP_SESSION_ID` | INT | N | PK |
| `PICKUP_PLAN_ID` | INT | N | FK → PICKUP_PLAN. UNIQUE |
| `TOKEN_NONCE` | CHAR(32) | N | 토큰을 만들 때 쓰는 난수(16바이트 hex) |
| `TOKEN_HASH` | CHAR(64) | N | 링크 토큰의 HMAC. UNIQUE |
| `SEND_CHANNEL` | VARCHAR(20) | N | 보낸 채널. `SMS`, `ALIMTALK` |
| `CLOSE_DT` | DATETIME | Y | 링크가 끝난 시각 |

- 토큰은 `HMAC-SHA256(서버 비밀키, PICKUP_PLAN_ID와 TOKEN_NONCE)`다. 원문은 저장하지 않는다. 다시 알릴 때는 같은 식으로 다시 계산해 같은 주소를 보낸다. 명세서는 토큰을 UUID v4로 적었는데, 원문을 저장하지 않으려고 이 방식으로 바꿨다.
- 링크를 열 때는 받은 토큰을 해시해 `TOKEN_HASH`로 찾는다. 없으면 "링크를 찾을 수 없어요"다.
- 링크는 계획과 함께 끝난다. 계획이 탑승·만료·취소로 끝날 때 `CLOSE_DT`를 채운다. 만료 처리가 아직 돌지 않았어도 `PICKUP_PLAN.EXPIRE_DT`가 지났으면 끝난 링크로 본다.
- 끝난 뒤에도 `TOKEN_HASH`는 남긴다. 끝난 링크를 다시 열었을 때 "이 링크는 끝났어요"를 보여주기 위해서다. 장소와 차 정보는 돌려주지 않는다.
- 전화번호는 이 테이블에 저장하지 않는다. 발송할 때 `CHILD.PHONE_ENC`를 읽어 쓴다.

### PICKUP_EVENT — 픽업 중 일어난 일

차량 화면과 아이 화면이 같은 이력을 읽어 상태를 맞춘다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PICKUP_EVENT_ID` | INT | N | PK |
| `PICKUP_PLAN_ID` | INT | N | FK → PICKUP_PLAN |
| `EVENT_TYPE` | VARCHAR(20) | N | 아래 코드 |
| `EVENT_DT` | DATETIME | N | 일어난 시각. 차량이 보낸 이벤트는 차량에서 일어난 시각이다 |
| `CLIENT_EVENT_ID` | CHAR(36) | Y | 차량이 이벤트마다 붙이는 번호(UUID). 서버나 아이 화면이 만든 이벤트는 NULL |
| `DETAIL` | JSON | Y | 유형별 부가 값 |

| `EVENT_TYPE` | 뜻 | `DETAIL` |
|---|---|---|
| `PLANNED` | 계획 생성, 배정 | 배정 지점, 배정 시각 |
| `STARTED` | 경로 시작 | — |
| `WAIT_STARTED` | 대기 장소 도착 | — |
| `END_TIME_CHANGED` | 오늘 하원 시각 변경 | 전·후 시각 |
| `REASSIGNED` | 지점·시각 재배정 | 전·후 지점, 전·후 시각, 사유 |
| `LINK_SENT` | 링크 발송 | 채널, 성공 여부, 발송 업체 결과 코드 |
| `LINK_OPENED` | 아이가 링크 열람 | — |
| `CHILD_LEAVING` | 아이가 "지금 나가요" | — |
| `STOPPED` | 픽업 지점 정차 | — |
| `WRONG_STOP` | 배정 지점이 아닌 곳에 정차 | 걸린 `ZONE_TYPE` |
| `REMIND_SENT` | "도착했어요" 다시 알림 | 채널, 성공 여부, 발송 업체 결과 코드 |
| `LOOP_STARTED` | 한 바퀴 시작 | `LOOP_ROUTE_ID`, 다시 도착 시각 |
| `BOARDED` | 탑승 감지 | — |
| `EXPIRED` | 만료 | — |
| `CANCELED` | 취소 | — |

- UNIQUE: `(PICKUP_PLAN_ID, CLIENT_EVENT_ID)`. 차량은 연결이 끊기면 이벤트를 쌓아 두었다가 다시 보낸다. 같은 이벤트가 두 번 와도 한 번만 들어간다. NULL은 겹쳐도 걸리지 않는다.
- 횟수 제한(다시 알림 1회, "지금 나가요" 1회)은 이 이력을 세어 Service에서 확인한다.
- `DETAIL`에는 위 표에 적힌 값만 넣는다. 발송 업체 응답을 그대로 넣지 않는다. 수신 번호가 들어 있을 수 있다.
- 이벤트의 순서와 "여기부터 새 이벤트" 기준은 `PICKUP_EVENT_ID`다. `EVENT_DT`는 차량 시각이고 늦게 도착할 수 있어 순서 기준으로 쓰지 않는다. 화면은 `PICKUP_PLAN_ID = ? AND PICKUP_EVENT_ID > ?`로 새 이벤트를 읽는다. 이벤트를 넣을 때 계획 행을 잠그므로 번호 순서와 커밋 순서가 같다.
- 인덱스는 위 UNIQUE 하나로 충분하다. `PICKUP_PLAN_ID`가 맨 앞이라 FK 인덱스를 겸한다. 새 이벤트 조회는 그 계획의 이벤트를 훑어 번호로 거른다. 계획 하나의 이벤트는 수십 건이라 따로 인덱스를 두지 않는다.

## DDL을 적용해 확인한 것

`backend/sql/20261110_initial_schema.sql`을 로컬 MySQL 8.4.7에 적용하고 스캐폴드해 확인했다(2026-11-10).

| 항목 | 결과 |
|---|---|
| 공간 타입 스캐폴드 | `POLYGON`·`LINESTRING` 컬럼은 건너뛴다. `AREA.BOUNDARY`, `LOOP_ROUTE.ROUTE_PATH`, `ACADEMY_PICKUP_SPOT.WALK_PATH`가 엔티티에 없다. `GEOMETRY`(`RESTRICTED_ZONE.GEOM`)는 점만 담는 타입으로 잡힌다 |
| 공간 컬럼이 있는 테이블의 쓰기 | `AREA`·`LOOP_ROUTE`는 NOT NULL 공간 컬럼이 엔티티에 없어 EF Core로 넣을 수 없다. `RESTRICTED_ZONE`도 선·면을 EF Core로 넣을 수 없다. 이 세 테이블의 등록·수정과 `WALK_PATH` 쓰기는 Dapper로 한다. 읽기는 원래 Dapper다 |
| 생성 컬럼 `ACTIVE_FLAG` | 스캐폴드는 일반 컬럼으로 잡는다. `RideMateContext.Extensions.cs`에서 저장 대상에서 빼면 EF Core 등록·수정이 되고 값도 읽혀 온다 |
| 살아 있는 행 UNIQUE | 살아 있는 행끼리는 중복이 거부되고, 삭제 표시한 행은 같은 값으로 여러 개 있을 수 있다 |
| 생성 컬럼이 낀 UNIQUE와 FK | FK 컬럼이 맨 앞인 UNIQUE가 있으면 InnoDB가 인덱스를 따로 만들지 않는다 |
| EF 트랜잭션 안의 Dapper | `BeginTransaction()`으로 연 트랜잭션을 `GetDbTransaction()`으로 꺼내 Dapper 명령에 넘기면 잠금 조회와 EF 저장이 한 트랜잭션에 들어간다. 롤백하면 둘 다 취소된다 |
| 공간 계산 | SRID 4326에서 선과 점, 면과 점 사이의 `ST_Distance`가 m 단위로 나온다. `ST_Contains`로 점이 구역 안인지 판정된다 |
| CHECK | 위반하는 값은 거부된다 |

잠금 순서(동시에 들어온 계획 생성과 탑승 처리)는 아직 실행해 보지 않았다.

## 아직 정하지 않은 것

기능 명세서의 "결정 필요" 시트는 반영하지 않았다. 그 시트에서 정해지면 여기서 지운다.

| 항목 | 영향 |
|---|---|
| QR 로그인의 휴대폰 쪽 흐름 | `device_code`·`user_code`로 나누는 것은 정했다. 휴대폰에서 무엇으로 본인을 확인하는지와 `user_code`의 길이는 정하지 않았다 |
| 탈퇴 화면 | 명세서에 없다. 개인정보 안내는 "탈퇴 시까지 보관"이다 |
| 동의 기록 보관 기간 | 계정을 삭제한 뒤 `CONSENT`를 언제까지 두는지 |
| 차 정보·호칭·운전자 실명의 입력 화면 | 명세서에 입력처가 없다. 실명은 개인정보 고지(RM-C-P1)의 수집 항목에도 넣어야 한다 |
| 같은 아이를 두 계정이 등록하는 경우 | 엄마와 아빠가 각자 등록하면 같은 날 계획이 둘 생긴다 |
| 픽업 후 목적지를 여러 개 둘지 | 지금은 계정당 하나다. 학원이나 요일마다 다르면 테이블로 나눈다 |
| 만료까지의 시간 | 배정 도착 시각 + 1시간으로 뒀다. 명세서는 만료 시점을 결정 필요로 적었다 |
| 만료된 링크의 화면 | 명세서 P-05는 "404와 같은 화면", 입력 검증과 상태 정의는 "이 링크는 끝났어요"다. 스키마는 뒤쪽을 지원한다 |
| 끝난 계획의 보관 | 명세서는 "세션은 종료 후 삭제"로 고지한다. 익명화한 행을 남기는 것을 고지 문구에 넣어야 한다 |
| 정리 시간, 앞뒤 간격, 늦추는 한도 | 가정값 1분, 2분, 2분 |
| 인증번호 재발송 간격, 같은 학원으로 보는 거리 | 가정값 1분, 50m |
| 제외 반경 | `NO_PARKING`·`YELLOW_*`·`CCTV`·`RESIDENT_PARKING`의 `BUFFER_M`. 단속 카메라가 비추는 구간은 데이터에 없다 |
| 어린이보호구역 데이터의 모양 | 경계가 없고 시설 위치만 있으면 반경 300m로 넣는다 |
| 실시간 주차대수의 범위 | 시영 주차장만 제공한다. 강남구 운영 공영주차장이 포함되는지 |
| 장소 검색 API | 어느 지도 사업자를 쓰는지. `PLACE_ID`의 형식과 대소문자 구분이 달라진다 |
| 아이 도보 속도 | 보행자 경로 API가 실패했을 때 `WALK_MIN` 추정에 쓰는 설정값 |
| 교통 소통 정보의 저장 | 서버 5분 캐시다. DB 테이블 없이 메모리 캐시로 둔다고 가정했다 |
| 잠금 순서의 실제 검증 | 이 문서의 잠금 순서는 실행해 보지 않았다. 구현할 때 같은 시간표로 계획 생성과 탑승 처리를 동시에 보내 교착과 정원 초과가 없는지 확인한다 |
| DB 시간대 | 서버 코드와 DB의 현재 시각이 같은 시간대여야 한다. 배포처가 정해지면 맞춘다 |
