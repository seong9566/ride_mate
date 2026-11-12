# 스키마 설계

RideMate 백엔드의 테이블 설계다. 테이블 규칙(이름, 감사 컬럼, 삭제 방식)은 `database.md`가 정본이고, 이 문서는 그 규칙 위에서 어떤 테이블과 컬럼을 두는지를 적는다. 실제 DDL은 `backend/sql/`에 쌓는다.

- 범위: 화면 RM-V00~V11, RM-M01~M04 전부(후순위 화면 포함). 기능 명세서 v0.3 기준이다.
- 결정 이유는 [ADR-0008] 계정·개인정보·익명화, [ADR-0009] 정원 배정, [ADR-0010] 중복 방지 UNIQUE, [ADR-0011] 공간 타입, [ADR-0021] 늦은 출발의 정원 초과에 있다.
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
- 도로 소통 수집: `TRAFFIC_LINK`, `TRAFFIC_COLLECT_RUN`, `TRAFFIC_SPEED`, `TRAFFIC_HOURLY_SPEED`, `TRAFFIC_PROFILE`
- 픽업 진행: `PICKUP_PLAN`, `PICKUP_SESSION`, `PICKUP_EVENT`

## 계정·인증

운전자(부모)만 계정을 가진다. 아이 계정은 없다. 비밀번호는 없고 휴대폰 인증번호나 QR로 로그인한다. [ADR-0008] [ADR-0012]

### USER_INFO — 운전자 계정

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `USER_ID` | INT | N | PK |
| `PHONE_ENC` | VARBINARY(255) | N | 휴대폰 번호(숫자만). 보호자 인증번호를 보낼 때 쓴다 |
| `PHONE_HASH` | CHAR(64) | N | 휴대폰 번호의 HMAC. 로그인할 때 계정을 찾는다 |
| `USER_NAME` | VARCHAR(50) | N | 운전자 이름. 형식 없는 문자열이고, 차량의 계정 목록(RM-V00)에 입력한 그대로 보여준다 [ADR-0014] |
| `CAR_MODEL` | VARCHAR(50) | Y | 차종 |
| `CAR_COLOR` | VARCHAR(20) | Y | 차 색 |
| `CAR_NO_LAST4` | CHAR(4) | Y | 차량 번호 뒷자리 |
| `DEST_ADDRESS` | VARCHAR(200) | Y | 픽업 후 목적지 주소. 내비의 "집"이나 검색해서 고른 곳 |
| `DEST_LAT`·`DEST_LNG` | DECIMAL(10,7) | Y | 픽업 후 목적지 위치 |
| `AUTO_SEND_YN` | TINYINT(1) | N | 링크 자동 발송 여부. 처음 값 0(매번 음성 확인) |
| `ARRIVAL_LOG_YN` | TINYINT(1) | N | 차량 도착 기록 사용 여부. 처음 값 0. 기록 자체는 차 안에만 있고 서버에 오지 않는다 |
| `WAKE_BEFORE_MIN` | TINYINT | N | 쉬는 모드에서 깨우는 시점(출발 몇 분 전, 0이면 출발 시각). 처음 값 5 |
| `SETUP_DONE_DT` | DATETIME | Y | 초기 설정 완료 시각. 4/4 완료 API가 채운다. NULL이면 로그인 응답의 `setup_required`가 true |

- 살아 있는 행 UNIQUE: `(PHONE_HASH, ACTIVE_FLAG)`
- 차 정보는 아이 화면에 보이는 값이다. 기획이 확정되지 않아 NULL 허용으로 두고 최초 설정에서 선택 입력으로 받는다.
- `CAR_COLOR`는 `WHITE`·`BLACK`·`GRAY`·`ETC`만 받는다. DB는 자유 문자열로 두고(기획이 색상 목록을 바꿀 수 있어 CHECK를 걸지 않는다) 검사는 Service에서 한다.
- `CAR_MODEL`은 화면 입력이 앞뒤 공백을 지운 뒤 1~20자(유니코드 코드 포인트 수)다. 칸은 VARCHAR(50)이다.
- 집·차·자동 발송 저장과 초기 설정 완료는 트랜잭션 첫 문장에서 `USER_INFO`를 `FOR UPDATE`로 잠그고 그 뒤 바꾸는 칸(`DEST_*`·`CAR_*`·`AUTO_SEND_YN`·`SETUP_DONE_DT`)만 갱신한다. 요청에 없는 칸을 옛 값으로 덮어쓰지 않는다.
- 아이 화면에 보이는 호칭은 따로 두지 않고 운전자 이름(`USER_NAME`)을 쓴다. 가입 화면에서 엄마·아빠·할머니·직접 입력으로 받는다. [ADR-0016]
- 운전자 이름은 가입할 때 필수로 받는다. 형식은 검사하지 않는다. 앞뒤 공백을 지운 값을 저장하고, 지운 뒤 비었거나 50자(유니코드 코드 포인트 수, MySQL `CHAR_LENGTH` 기준)를 넘으면 거절한다. 입력 화면은 아직 명세서에 없다.
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
| `ROTATE_DT` | DATETIME | Y | 지금 값을 `PREV_TOKEN_HASH`로 넘긴 시각. 직전 값 허용 시간(24시간)의 기준이다 |
| `DEVICE_ID` | VARCHAR(100) | N | 앱이 처음 실행할 때 만든 임의의 설치 번호. 차대번호가 아니다 |
| `EXPIRE_DT` | DATETIME | N | 만료 시각 |
| `LAST_USED_DT` | DATETIME | N | 마지막으로 쓰인 시각 |

- 이 테이블을 읽는 모든 조회에 `SESSION_TYPE` 조건을 붙인다. refresh와 계정 목록은 `CAR`만, 쿠키 인증은 `WEB`만 본다. 종류를 가리지 않는 것은 계정 삭제뿐이다.
- 로그아웃하면 `DELETE_YN`을 켠다. refresh 토큰과 쿠키 인증은 `DELETE_YN = FALSE`이고 `EXPIRE_DT`가 지나지 않은 행만 통과시킨다.
- refresh 토큰을 쓰면 같은 행을 고친다. 새 값은 세션과 낸 값으로 계산하므로(`security.md`) 같은 값을 내면 늘 같은 새 값이 나온다. 지금 값이면 `PREV_TOKEN_HASH`에 지금 값을, `TOKEN_HASH`에 새 값을, `ROTATE_DT`에 지금을 넣는다. `PREV_TOKEN_HASH`와 같고 `ROTATE_DT`가 24시간 안이면 지금 값과 같은 새 값을 다시 주고 `TOKEN_HASH`·`PREV_TOKEN_HASH`·`ROTATE_DT`는 그대로 둔다(계산한 값이 `TOKEN_HASH`와 다르면 거절한다). 둘 다 아니면 거절한다. 어느 쪽이든 `EXPIRE_DT`는 지금 + 90일, `LAST_USED_DT`는 지금으로 고치고, 이 처리는 그 행을 `FOR UPDATE`로 잠그고 한다.
- `WEB` 행은 `DEVICE_ID`가 빈 문자열이고 `PREV_TOKEN_HASH`·`ROTATE_DT`를 쓰지 않는다.
- 인덱스: `(USER_ID, DEVICE_ID)`, `(PREV_TOKEN_HASH)`, `(DEVICE_ID, SESSION_TYPE)`(차 계정 목록)

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
- 주기 작업(`PhoneVerificationCleanupWorker`, 서버 시작 때와 10분마다)이 `DELETE_YN = 0`인 행 중 인증에 성공한 지 10분이 지난 행(`VERIFY_DT`가 있고 10분 전보다 이전, 용도와 관계없다)과 인증하지 않고 만료된 지 24시간이 지난 행(`VERIFY_DT`가 없고 `EXPIRE_DT`가 24시간 전보다 이전)의 `PHONE_HASH`·`CODE_HASH`를 빈 문자열로 바꾸고 `DELETE_YN`을 켠다. 한 번에 500행씩, 대상이 없을 때까지 되풀이하며 같은 회차 안에서는 앞 묶음의 마지막 ID 다음부터 읽는다. 행은 지우지 않는다(`CONSENT.PHONE_VERIFICATION_ID`가 가리킨다). 인증 성공 뒤 10분은 보호자 동의 저장(`AddConsent`)이 인증 행을 찾는 시간(`GuardianConsentValidMinutes`)이라 그 전에는 지우지 않는다. 동의 증빙에 필요한 시각과 방법은 `CONSENT`에 있다.
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
| `MATCH_CODE` | CHAR(4) | N | 차와 휴대폰에 함께 보이는 숫자 4자리. 다른 사람의 QR인지 운전자가 눈으로 확인한다 |
| `STATUS` | VARCHAR(20) | N | `PENDING`(대기), `SCANNED`(휴대폰이 QR을 열었다), `VERIFIED`(휴대폰에서 본인 확인을 마쳤고 허용을 기다린다), `APPROVED`(휴대폰에서 허용했다), `USED`(차량이 토큰을 받아갔다), `CANCELED`(취소됐다) |
| `CANCEL_REASON` | VARCHAR(20) | Y | `CANCELED`일 때만 채운다. `USER`(차에서 취소·새 QR), `DRIVING`(차가 출발해 중단), `PHONE`(휴대폰에서 취소) |
| `USER_ID` | INT | Y | FK → USER_INFO. 휴대폰에서 본인 확인을 마치면(`VERIFIED`) 채운다 |
| `EXPIRE_DT` | DATETIME | N | 만료 시각(만든 뒤 3분) |
| `LAST_POLL_DT` | DATETIME | Y | 차량이 마지막으로 결과를 물은 시각. 폴링 간격(3초)보다 1초 넘게 빨리 다시 물으면 `slow_down`이다 |

- 상태는 `PENDING` → `SCANNED` → `VERIFIED` → `APPROVED` → `USED`로만 나아간다. 휴대폰에서 "다른 번호로 로그인"을 고르면 `VERIFIED`에서 `SCANNED`로 돌아가고 `USER_ID`를 비운다. `USED` 전이면 어느 상태에서든 `CANCELED`가 될 수 있다.
- 만료는 상태가 아니라 `EXPIRE_DT`로 판단한다. 만료된 행에서는 휴대폰의 모든 요청을 거절한다. 차량은 `APPROVED`가 아니면 만료 뒤 `expired_token`을 받고, `APPROVED`여도 만료 뒤 60초가 지나면 `expired_token`을 받는다.
- 휴대폰은 `user_code`로, 차량은 `device_code`로 행을 찾는다.
- 인증 트랜잭션은 `PHONE_VERIFICATION` → `QR_LOGIN` → `USER_INFO` → `LOGIN_SESSION` 순서로 잠근다. 잠글 행을 모두 `FOR UPDATE`로 잡은 뒤에 일반 조회를 한다(먼저 조회하면 스냅샷이 고정돼 잠근 행의 최신 값을 못 본다).
- 두 UNIQUE는 삭제된 행까지 포함한다. 만든 값이 겹쳐 중복 키 오류가 나면 Service가 값을 새로 만들어 다시 넣는다.
- `VERIFIED`·`APPROVED`·`USED`일 때만 `USER_ID`가 있다(`CANCELED`는 취소 전 상태를 따른다). 코드 값 조건이라 CHECK로 걸지 않고 Service에서 확인한다.
- 차량이 `APPROVED`를 확인하면 `LOGIN_SESSION`을 만들고 `USED`로 바꾼다. 이 트랜잭션은 첫 문장에서 그 행을 `FOR UPDATE`로 잠그고 상태를 다시 읽는다. 이미 `USED`면 거절하므로 한 QR로 두 번 로그인할 수 없다.
- 토큰을 받아가는 요청의 `DEVICE_ID`가 행의 값과 같아야 한다. QR을 찍어 간 다른 기기는 로그인 토큰을 받을 수 없다.

### CONSENT — 동의 기록

보호자(법정대리인) 확인과 수집·이용 동의를 남긴다. 고치지 않고 쌓기만 한다. 동의를 철회하면 `AGREE_YN = 0`인 행을 새로 넣는다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `CONSENT_ID` | INT | N | PK |
| `USER_ID` | INT | N | FK → USER_INFO |
| `CONSENT_TYPE` | VARCHAR(20) | N | `TERMS`(필수: 서비스 이용약관, 가입 때), `PRIVACY`(필수: 개인정보 수집·이용, 가입 때), `GUARDIAN`(법정대리인 확인), `CHILD_INFO`(필수: 아이 이름·연락처), `ARRIVAL_LOG`(선택: 차량 도착 기록) |
| `AGREE_YN` | TINYINT(1) | N | 동의 여부 |
| `TERMS_VERSION` | VARCHAR(20) | N | 동의한 약관·고지 문서의 버전 |
| `VERIFY_METHOD` | VARCHAR(20) | N | 확인 방법. `PHONE_OTP`(휴대폰 인증), `CHECK`(체크박스) |
| `PHONE_VERIFICATION_ID` | INT | Y | FK → PHONE_VERIFICATION. 휴대폰 인증으로 확인한 경우 |
| `CONSENT_DT` | DATETIME | N | 동의한 시각 |

- 지금 동의 상태는 그 계정·종류의 가장 최근 행이다. 이 테이블이 정본이다. `USER_INFO.ARRIVAL_LOG_YN`은 화면이 빨리 읽기 위한 사본이고 `ARRIVAL_LOG` 행을 넣는 트랜잭션에서 함께 바꾼다.
- 초기 설정의 보호자 동의(`GUARDIAN`·`CHILD_INFO`·`ARRIVAL_LOG` 3행)는 그 번호의 가장 최근 `GUARDIAN` 인증번호 행이 인증에 성공한 지 10분 안일 때만 넣는다. 선택 항목에 동의하지 않아도 `ARRIVAL_LOG` 행을 `AGREE_YN = 0`으로 넣는다.
- 인덱스: `(USER_ID, CONSENT_TYPE, CONSENT_ID)`

## 아이·시간표

### CHILD — 아이

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `CHILD_ID` | INT | N | PK |
| `USER_ID` | INT | N | FK → USER_INFO |
| `CHILD_NAME` | VARCHAR(50) | N | 이름(화면 입력은 1~10자) |
| `PHONE_ENC` | VARBINARY(255) | N | 휴대폰 번호(숫자만). 암호화해서 저장한다 |
| `PHONE_HASH` | CHAR(64) | Y | 휴대폰 번호의 HMAC. 같은 번호가 이미 등록됐는지, 부모 번호와 같은지 비교한다. NULL은 이 칸을 넣기 전에 만든 행이거나 삭제한 행이다 |
| `SEND_CHANNEL` | VARCHAR(20) | N | `SMS`, `ALIMTALK`. 처음 값 `SMS` |

- 살아 있는 행 UNIQUE: `(PHONE_HASH, ACTIVE_FLAG)`. 값이 NULL인 행은 걸리지 않는다.
- 부모 한 명이 아이를 여러 명 둘 수 있는 구조다. 명세서의 화면은 아이 1명 흐름이다.
- 한 계정에는 살아 있는 아이를 하나만 등록한다(초기 설정 화면이 아이 1명 흐름). 여러 명은 화면이 생기면 바꾼다.
- 다른 계정의 살아 있는 아이와 번호가 같으면 등록을 거절한다(두 계정이 같은 아이를 등록하지 못한다).
- 아이 번호가 부모 번호와 같으면 등록을 거절한다. 같은지는 `PHONE_HASH`끼리 Service에서 비교한다.
- 화면에 내려줄 때는 Service에서 복호화한 뒤 가운데 자리를 가린다.
- 아이 등록은 트랜잭션 첫 문장에서 `USER_INFO`를 `FOR UPDATE`로 잠그고 그 뒤에 기존 아이·번호를 조회한다. 계정당 살아 있는 아이 1명은 DB 제약이 아니라 이 잠금으로 지킨다. 아이를 추가하는 다른 경로도 같은 잠금을 먼저 잡는다.
- 아이 정보 수정(`UpdateChild`)도 트랜잭션 첫 문장에서 `USER_INFO`를 먼저 잠근 뒤 그 계정의 살아 있는 아이를 확인한다. 번호를 바꾸면 `PHONE_ENC`와 `PHONE_HASH`를 함께 바꾸고, 지금 번호와 같은 번호는 바꾸지 않은 것으로 보아 부모 번호·중복 판정을 건너뛴다. 진행 중 픽업이 있어도 막지 않는다. `PICKUP_*`에는 번호를 저장하지 않으므로 이후 발송부터 새 번호로 나간다.
- 삭제는 아래 "아이·계정 삭제" 순서를 따른다. 복구는 할 수 없다.

### ACADEMY — 학원

좌표가 있는 학원은 지원 학원가 안에 있다. 좌표 없는 학원도 있다(학원가 NULL). 여러 부모가 같은 행을 가리킨다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `ACADEMY_ID` | INT | N | PK |
| `AREA_ID` | INT | Y | FK → AREA. 좌표가 없으면 NULL |
| `ACADEMY_NAME` | VARCHAR(100) | N | 원천 데이터나 검색 결과의 이름 |
| `ADDRESS` | VARCHAR(200) | N | 주소 |
| `LAT`·`LNG` | DECIMAL(10,7) | Y | 위치. 앱이 좌표를 주지 않으면 NULL |
| `SOURCE_TYPE` | VARCHAR(20) | N | `TEAM`(운영팀), `NEIS`(학원·교습소 표준데이터), `PLACE`(장소 검색으로 추가) |
| `PLACE_ID` | VARCHAR(100) | Y | 장소 검색 API의 장소 ID. 차량 앱 SDK의 검색 결과로 만든 행은 NULL이다 |
| `SOURCE_REF` | VARCHAR(100) | Y | 원천 데이터의 학원 관리번호 |
| `OPEN_STATUS` | VARCHAR(20) | Y | 원천 데이터의 등록 상태. `OPEN`(운영), `CLOSED`(휴원·폐원) |

- 학원 이름·주소 문자열은 앱 SDK의 장소 검색에서 받아 필수이고, 좌표는 선택이다. 서버가 이 테이블을 먼저 찾고 장소 검색 API 결과를 붙여 돌려주는 학원 검색(`/places`)과 `ACADEMY_ID`로 고르는 방식은 지금은 쓰지 않는다(앱 SDK 검색).
- 좌표가 있으면 그 점을 담는 `AREA`를 찾아 `AREA_ID`를 채운다. 담는 `AREA`가 없으면(`AREA.BOUNDARY` 밖) 등록을 거절한다. 좌표가 없으면 `AREA_ID`는 NULL이다.
- 이름·주소를 정규화(앞뒤 공백 제거, 연속 공백 한 칸)해 같은 살아 있는 행을 재사용한다. 그 행에 좌표가 없고 새 요청에 좌표가 있으면 좌표와 `AREA_ID`를 채운다. 새 행은 `PLACE`로 만든다.
- 좌표가 확정되면(카드 AC) `LAT`·`LNG`·`AREA_ID`를 다시 필수로 되돌린다.
- 좌표 없는 학원은 픽업 지점 배정과 단속 판정을 하지 않는다. 출발 안내는 `NO_ACADEMY_LOCATION`을 준다.
- 부모가 지은 학원 이름은 여기가 아니라 `TIMETABLE.DISPLAY_NAME`에 둔다.
- 살아 있는 행 UNIQUE: `(PLACE_ID, ACTIVE_FLAG)`, `(SOURCE_REF, ACTIVE_FLAG)`. 값이 NULL인 행은 걸리지 않는다.
- CHECK: `(LAT IS NULL) = (LNG IS NULL)`, `AREA_ID IS NULL OR LAT IS NOT NULL`(좌표 없이 학원가만 있는 행 금지)

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
- 시간표 등록과 픽업 스위치 변경은 트랜잭션 첫 문장에서 `USER_INFO`를 `FOR UPDATE`로 잠근 뒤 개수·겹침·소유를 확인한다(T13 아이 등록과 같은 순서). 새 학원 행을 학원가와 함께 넣거나 좌표를 채우면 FK 때문에 그 `AREA` 행에 공유 잠금이 걸린다 — 삭제·계획 생성처럼 `AREA`를 먼저 잠그는 경로를 구현할 때 순서를 확인한다.
- 진행 중인 픽업 계획이 있는 시간표는 삭제하지 않고, 학원·하원 시각을 바꾸거나 그 계획의 요일을 빼는 편집도 거절한다. 오늘 하원 시각만 바꾸는 것은 `TIMETABLE_EXCEPTION`으로 한다.
- 시간표 삭제(T22)는 아래 순서를 한 트랜잭션에서 지킨다. "아이·계정 삭제"의 0번 잠금과 2~4행을 쓰고, 1번(계획 취소) 대신 진행 중 계획이 있으면 거절한다.
  1. 트랜잭션을 열기 전에 일반 조회로 그 시간표 학원의 `AREA_ID`를 읽는다. 트랜잭션 안에서 먼저 조회하면 `AREA` 잠금 전의 스냅샷이 고정되기 때문이다.
  2. 첫 문장으로 그 `AREA` 행을 `FOR UPDATE`로 잠근다. 학원에 좌표가 없으면(`AREA_ID` NULL) 잠그지 않는다.
  3. `USER_INFO`를 `FOR UPDATE`로 잠근다. 같은 계정의 등록·스위치 변경과 직렬화한다.
  4. 일반 조회보다 먼저 `FOR SHARE` 잠금 읽기로 학원가를 다시 읽는다. 학원 행에 공유 잠금이 걸려 그사이 다른 계정이 학원가를 채우지 못하고, 학원을 바꾸는 편집은 `USER_INFO`를 잠그므로 커밋까지 값이 바뀌지 않는다. 1 뒤에 다른 계정의 등록이 학원가를 채웠거나 같은 계정의 편집이 학원을 다른 학원가로 옮겼으면 이때 그 `AREA`를 잠근다(순서가 어긋나 드물게 교착할 수 있고, 교착은 재시도한다. 재시도 때는 1이 새 값을 읽는다).
  5. 일반 조회로 소유(살아 있는 아이의 살아 있는 시간표)를 확인하고 요일 ID를 모두 읽는다.
  6. 그 요일들을 가리키는 진행 중 계획을 일반 조회로 찾고, 있으면 그 계획들만 PK 순서로 `FOR UPDATE` 잠가 최신 `STATUS`·`TIMETABLE_DAY_ID`로 다시 확인한다. 남은 것이 있으면 거절한다. `AREA` 잠금 때문에 새 계획은 생기지 않고, 그사이 끝난 계획은 잠금 읽기에서 빠진다.
  7. `TIMETABLE_EXCEPTION` → `TIMETABLE_DAY` → `TIMETABLE` 순으로 지운다.
- 시간표 편집(T22)은 삭제의 1~6을 그대로 따르고 다음만 다르다.
  - 1·2에서 지금 학원의 학원가와 함께 요청 좌표를 담는 학원가도 트랜잭션 밖에서 고르고, 둘을 `AREA_ID` 순서로 잠근다. 새 학원 행을 넣거나 좌표를 채울 때 FK로 걸리는 그 학원가의 공유 잠금을 미리 잡아 그 학원가의 계획 생성과 순서를 맞춘다.
  - 4에서 다시 읽은 학원가가 2에서 잠근 두 학원가에 없을 때만 잠근다.
  - 6에서 진행 중 계획을 찾는 요일은 학원(`ACADEMY_ID`)이나 하원 시각이 바뀌면 그 시간표의 요일 전부, 아니면 빼는 요일뿐이다. 이름 변경과 요일 추가는 확인하지 않는다.
  - 7 대신 뺀 요일의 `TIMETABLE_EXCEPTION` → 뺀 `TIMETABLE_DAY` → 되살릴 `TIMETABLE_DAY` → 새 `TIMETABLE_DAY` → `TIMETABLE` 순으로 쓴다.
- 등록은 `USER_INFO` → (학원 FK) `AREA` 공유 잠금, 삭제·편집은 `AREA` → `USER_INFO` 순이라 같은 계정의 등록과 삭제·편집이 동시에 오면 교착할 수 있다. 세 API 모두 교착을 재시도한다. 경로 시작의 계획 생성(`AREA` → … → 계획 INSERT의 `USER_INFO` FK 공유 잠금)과 순서를 맞추는 쪽을 골랐다.
- 편집으로 요일을 뺐다가 다시 넣으면 새 행을 만들지 않고 삭제 표시된 `TIMETABLE_DAY` 행을 되살린다. `LAST_PICKUP_DATE`가 이어진다. 되살린 행의 `PICKUP_YN`은 새 요일과 같이 1이고, 뺄 때 지운 날짜별 예외는 되살리지 않는다.
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

한 트랜잭션에서 아래 순서로 한다. 아이만 삭제할 때는 그 아이에 대해 0~5만 한다. 시간표 한 건을 삭제할 때도 0번의 잠금부터 한다(순서 전체는 위 `TIMETABLE` 절).

비우는 값과 `DELETE_YN` 켜기는 한 행에 대해 한 UPDATE로 한다. 잠금은 Dapper로 먼저 잡는다. EF 저장 한 번에 여러 행을 맡기면 UPDATE 순서가 정해지지 않는다.

| 순서 | 대상 | 처리 |
|---|---|---|
| 0 | `AREA` 잠금 | 트랜잭션의 첫 문장으로, 그 아이들의 시간표가 가리키는 학원가의 `AREA` 행을 `AREA_ID` 순서로 `FOR UPDATE` 잠근다. 계획 생성이 같은 잠금을 기다리므로 삭제 도중에 새 계획이 끼어들지 못한다 |
| 1 | 진행 중인 `PICKUP_PLAN` | 잠금 조회(`FOR UPDATE`)로 찾아 `PICKUP_PLAN_ID` 순서로 취소한다(상태 바꾸기와 익명화 절차 그대로). 보조 인덱스로 잠그지 않는다(`PICKUP_PLAN` 절 "상태 바꾸기"). 보낸 링크도 함께 끝난다 |
| 2 | `TIMETABLE_EXCEPTION` | `DELETE_YN`을 켠다 |
| 3 | `TIMETABLE_DAY` | `DELETE_YN`을 켠다 |
| 4 | `TIMETABLE` | `DELETE_YN`을 켜고 `DISPLAY_NAME`을 빈 문자열로 바꾼다 |
| 5 | `CHILD` | `DELETE_YN`을 켜고 `CHILD_NAME`을 빈 문자열로, `PHONE_ENC`를 빈 값으로, `PHONE_HASH`를 NULL로 바꾼다 |
| 6 | `LOGIN_SESSION`, `QR_LOGIN` | `DELETE_YN`을 켠다. `QR_LOGIN`은 `USER_ID`를 NULL로 바꾼다 |
| 7 | `PHONE_VERIFICATION` | 그 번호의 행에서 `PHONE_HASH`·`CODE_HASH`를 빈 문자열로 바꾸고 `DELETE_YN`을 켠다. 남겨 두면 `CONSENT`를 거쳐 삭제된 계정의 번호 해시로 이어진다 |
| 8 | `USER_INFO` | `DELETE_YN`을 켜고 `PHONE_ENC`를 빈 값으로, `PHONE_HASH`·`USER_NAME`을 빈 문자열로, 차 정보·목적지를 NULL로 바꾼다 |

- `CONSENT`는 지우지 않는다. 동의를 받았다는 증빙이다. 보관 기간은 정해지지 않았다.
- 탈퇴를 구현할 때 아이 등록(`USER_INFO` → `CHILD` 순서로 잠금)과 잠금 순서가 맞는지 확인한다.
- 로그아웃은 삭제가 아니다. 그 차량의 `LOGIN_SESSION`만 끝내고 서버의 시간표와 아이 정보는 남는다.

## 기준 데이터

운영팀이 넣거나 서버가 공공데이터에서 적재한다. 관리 화면은 없다. 차량은 공공데이터를 직접 부르지 않는다.

### AREA — 학원가(서비스 지역)

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `AREA_ID` | INT | N | PK |
| `AREA_NAME` | VARCHAR(50) | N | 이름(예: 대치동) |
| `AREA_CODE` | VARCHAR(20) | Y | 학원가 코드(`DAECHI`·`PYEONGCHON`) |
| `BOUNDARY` | POLYGON SRID 4326 | N | 구역 경계. SPATIAL INDEX |

대치동·평촌 행은 `sql/20261112_area_seed.sql`로 넣는다(사각형 경계, 2026-11-12 사용자 확인).

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

`RESTRICTED_ZONE`·`AREA` 행을 바꾸는 SQL은 어떤 컬럼을 바꾸든 `UPDATE_DT`를 갱신한다. 단속 구역 목록 API의 앱 캐시 버전에 쓰인다. 적재 작업은 차량이 조회하는 하원 시간대(14~24시)를 피해 돌린다(버전이 초 단위 수정 시각이라 같은 초 안의 연속 수정을 놓칠 수 있다).

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
| `DAY_PASS_FEE` | INT | Y | 1일 주차권 요금(원). 1일 최대 요금은 `DAY_MAX_FEE` |
| `DAY_MAX_FEE` | INT | Y | 1일 최대 요금(원). 누적 요금의 상한 |
| `TOTAL_SPACES` | SMALLINT | Y | 주차 면수 |
| `REALTIME_REF` | VARCHAR(100) | Y | 실시간 주차대수 API의 주차장 코드. 없으면 화면은 "자리 정보 없음" |
| `AVAILABLE_SPACES`·`AVAILABLE_UPDATE_DT` | SMALLINT, DATETIME | Y | 마지막으로 조회한 빈자리 수와 조회 시각 |
| `SOURCE_REF` | VARCHAR(100) | Y | 표준데이터의 주차장 관리번호 |
| `FEE_SOURCE` | VARCHAR(20) | Y | 요금·운영 시간을 고른 출처. `STANDARD`(전국주차장정보표준데이터), `SEOUL`(서울시 공영주차장 안내 정보 `GetParkInfo`) |
| `FEE_BASE_DATE` | DATE | Y | 고른 출처의 기준일 |
| `SEOUL_REF` | VARCHAR(20) | Y | 서울시 공영주차장 코드(`PKLT_CD`). 연결 안 되면 NULL |

- 주차장은 n8n 워크플로 `parkingCollect01`이 하루 1회 공영만 적재한다(민영 제외 — 요금 출처 없음).
- 표준데이터와 서울시가 같은 주차장에 다른 값을 주면 기준일(`referenceDate` vs `LAST_DATA_SYNC_TM`)이 최신인 쪽을 쓰고 `FEE_SOURCE`·`FEE_BASE_DATE`에 남긴다.
- CHECK: `(FEE_SOURCE IS NULL) = (FEE_BASE_DATE IS NULL)`
- 추천할 때 저장된 빈자리가 5분 안의 값이면 그대로 쓰고, 아니면 실시간 API를 조회해 덮어쓴다. API가 실패하면 저장된 값을 쓴다.
- `REALTIME_REF`가 없거나, 실시간 API가 성공했는데 응답에 그 주차장이 없으면 저장된 빈자리를 쓰지 않고 "자리 정보 없음"으로 둔다.
- 수집기가 `REALTIME_REF`를 바꾸면(연결이 끊기거나 다른 시설로 바뀌면) `AVAILABLE_SPACES`·`AVAILABLE_UPDATE_DT`를 함께 비운다.
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

## 도로 소통 수집

대치동·평촌 학원가의 도로 소통 이력을 n8n 수집기가 적재한다. 감사 컬럼은 `CREATE_USER = SYSTEM`, `UPDATE_USER = SYSTEM`으로 넣는다.

코드 값: 수집 테이블의 `SOURCE`는 `TOPIS`·`GITS`, `TRAFFIC_PROFILE.SOURCE`는 `COLLECTED`·`MONTHLY`, `AREA_CODE`는 `DAECHI`·`PYEONGCHON`, `STATUS`는 `OK`·`PARTIAL`·`NO_KEY`·`FAILED`다.

### TRAFFIC_LINK — 수집 대상 링크

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `TRAFFIC_LINK_ID` | INT | N | PK |
| `SOURCE` | VARCHAR(20) | N | 소스 |
| `AREA_CODE` | VARCHAR(20) | N | 학원가 |
| `LINK_ID` | VARCHAR(20) | N | 소스의 링크 ID(TOPIS 링크 ID와 GITS 링크 ID는 체계가 다르다) |
| `ROUTE_ID` | VARCHAR(20) | Y | GITS 도로 ID. 5분 수집에서 도로 단위로 부를 때 쓴다. TOPIS는 NULL |
| `ROAD_NAME` | VARCHAR(100) | N | 도로명 |
| `ST_NODE_NAME`·`ED_NODE_NAME` | VARCHAR(100) | N | 시작·끝 지점명 |
| `AXIS_DIR` | VARCHAR(20) | Y | 방향(TOPIS `axis_dir`, GITS `routeWay`) |
| `DISTANCE_M` | INT | N | 링크 길이(m) |
| `CENTER_DIST_M` | INT | Y | 학원가 중심에서 링크까지 최단 거리(m). 좌표가 있는 TOPIS만 채운다 |

- 살아 있는 행 UNIQUE: `(SOURCE, LINK_ID, ACTIVE_FLAG)`. 목록을 다시 만들 때 이 값으로 같은 링크를 찾아 덮어쓰고, 결과에서 빠진 링크는 삭제 표시한다.
- 인덱스: `(SOURCE, AREA_CODE)`
- CHECK: `DISTANCE_M >= 0 AND (CENTER_DIST_M IS NULL OR CENTER_DIST_M >= 0)`
- 좌표는 저장하지 않는다.

### TRAFFIC_COLLECT_RUN — 수집 회차(소스별)

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `COLLECT_RUN_ID` | INT | N | PK |
| `SOURCE` | VARCHAR(20) | N | 소스 |
| `RUN_DT` | DATETIME | N | 회차 시각(KST, 5분 경계) |
| `STATUS` | VARCHAR(20) | N | `OK`·`PARTIAL`·`NO_KEY`·`FAILED` |
| `LINK_COUNT` | INT | N | 대상 링크 수 |
| `SAVED_COUNT` | INT | N | 저장한 행 수 |
| `ERROR_MESSAGE` | VARCHAR(500) | Y | 실패 요약. 키·URL을 넣지 않는다 |

- UNIQUE: `(SOURCE, RUN_DT)`
- CHECK: `LINK_COUNT >= 0 AND SAVED_COUNT BETWEEN 0 AND LINK_COUNT`
- 수집 공백 = 수집 시간대 안의 5분 칸 중 그 소스의 행이 없는 칸.
- `NO_KEY`는 키가 비어 호출하지 않은 회차, `FAILED`는 저장한 행이 0인 회차, `PARTIAL`은 일부 링크만 저장한 회차다.

### TRAFFIC_SPEED — 5분 소통 이력

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `TRAFFIC_SPEED_ID` | INT | N | PK |
| `TRAFFIC_LINK_ID` | INT | N | FK → TRAFFIC_LINK |
| `COLLECT_RUN_ID` | INT | N | FK → TRAFFIC_COLLECT_RUN |
| `COLLECTED_DT` | DATETIME | N | 회차 시각(5분 경계). 한 회차의 행은 모두 같은 값이다 |
| `SOURCE_COLLECTED_DT` | DATETIME | Y | 소스가 준 수집 시각(GITS `collDate`). TOPIS는 NULL |
| `SPEED_KMH` | SMALLINT | N | 속도(TOPIS `prcs_spd`, GITS `spd`). 소수로 오므로(예 77.4) 정수로 반올림한다 |
| `TRAVEL_SEC` | INT | N | 여행시간 초(TOPIS `prcs_trv_time`, GITS `trvlTime`을 초로 맞춘 값). 소수면 반올림한다 |

- UNIQUE: `(TRAFFIC_LINK_ID, COLLECTED_DT)`. 같은 회차가 두 번 돌아도 중복으로 쌓이지 않는다. FK 인덱스를 겸한다.
- 인덱스: `(COLLECT_RUN_ID)`, `(COLLECTED_DT)`
- CHECK: `SPEED_KMH >= 0 AND TRAVEL_SEC >= 0`
- 요일·시간대 집계는 n8n이 `TRAFFIC_PROFILE`로 만든다(요일 `WEEKDAY(COLLECTED_DT) + 1`, 5분 칸 `HOUR*60 + MINUTE`).

### TRAFFIC_HOURLY_SPEED — TOPIS 월별 시간 속도

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `TRAFFIC_HOURLY_SPEED_ID` | INT | N | PK |
| `TRAFFIC_LINK_ID` | INT | N | FK → TRAFFIC_LINK |
| `SPEED_DATE` | DATE | N | 파일의 `일자` |
| `HOUR_NO` | TINYINT | N | 파일 열 `~HH시`의 HH. 1~24 |
| `SPEED_KMH` | DECIMAL(5,1) | N | 속도(km/h) |

- UNIQUE: `(TRAFFIC_LINK_ID, SPEED_DATE, HOUR_NO)`
- CHECK: `HOUR_NO BETWEEN 1 AND 24`, `SPEED_KMH > 0`
- 행을 지우지 않으므로 `ACTIVE_FLAG` 없이 일반 UNIQUE를 쓴다.

### TRAFFIC_PROFILE — 혼잡 집계표

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `TRAFFIC_PROFILE_ID` | INT | N | PK |
| `AREA_CODE` | VARCHAR(20) | N | 학원가 코드 |
| `DAY_OF_WEEK` | TINYINT | N | 요일 번호. 월 1~일 7 |
| `SLOT_MIN` | SMALLINT | N | 자정부터 지난 분. 0~1435, 5분 단위 |
| `SOURCE` | VARCHAR(20) | N | `COLLECTED`·`MONTHLY` |
| `SPEED_KMH` | DECIMAL(5,1) | N | 집계 속도(km/h) |
| `SAMPLE_DAYS` | SMALLINT | N | 집계 표본 일수 |

- 한 칸은 `(AREA_CODE, DAY_OF_WEEK, SLOT_MIN, SOURCE)`로 정의한다. `SLOT_MIN`은 자정부터 지난 분이므로 하루를 5분 간격으로 나눈다.
- UNIQUE: `(AREA_CODE, DAY_OF_WEEK, SLOT_MIN, SOURCE)`
- CHECK: `DAY_OF_WEEK BETWEEN 1 AND 7`, `SLOT_MIN BETWEEN 0 AND 1435 AND MOD(SLOT_MIN, 5) = 0`, `SPEED_KMH > 0`, `SAMPLE_DAYS >= 0`
- 행을 지우지 않으므로 `ACTIVE_FLAG` 없이 일반 UNIQUE를 쓴다. 이번 집계에서 근거가 사라진 칸은 행을 지우지 않고 `SAMPLE_DAYS = 0`으로 바꾼다.

## 픽업 진행

명세서의 "오늘 계획"이 `PICKUP_PLAN`, "세션"이 `PICKUP_SESSION`이다. 계획은 배정과 차량의 진행 상태를 갖고, 세션은 아이에게 보낸 링크 한 건이다.

### PICKUP_PLAN — 오늘 계획

차량이 경로 시작(`UpdatePickupPlanStart`)을 부를 때 오늘 계획이 없으면 만든다. 서버는 하원 시각 − `PickupPlan:CreateLeadHours`(3시간)보다 이른 요청을 거절한다(`api.md` "오늘 픽업 계획"). 그때 픽업 지점과 도착 시각을 배정하고 정원을 잡는다. [ADR-0023]

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
| `PICKUP_SPOT_ID` | INT | Y | FK → PICKUP_SPOT. 배정 지점. 쓸 수 있는 지점이 없거나 임의 좌표면 NULL |
| `SLOT_DT` | DATETIME | Y | 배정된 도착 시각(분 단위). 지점이나 임의 좌표가 있으면 값이 있다 |
| `WALK_MIN` | TINYINT | Y | 배정 지점(임의 좌표면 그 좌표)까지 아이 도보 시간(분). 끝나면 NULL |
| `CUSTOM_LAT`·`CUSTOM_LNG` | DECIMAL(10,7) | Y | 부모가 고른 픽업 좌표. 기본 지점이면 NULL. 끝나면 NULL |
| `CUSTOM_NAME` | VARCHAR(100) | Y | 임의 좌표의 장소 이름(카카오 주소 변환 결과). 끝나면 NULL |
| `CUSTOM_WALK_PATH` | LINESTRING SRID 4326 | Y | 학원에서 임의 좌표까지 도보 경로 선. 카카오 도보 경로가 실패했으면 NULL. 끝나면 NULL. 스캐폴드가 건너뛰므로 엔티티에 없고 읽기·쓰기는 Dapper로 한다 |
| `EXPIRE_DT` | DATETIME | N | 만료 시각. `SLOT_DT`(없으면 그날 하원 시각)에 설정값(기본 1시간)을 더한다. 재배정하거나 하원 시각이 바뀌면 다시 계산한다 |
| `STATUS` | VARCHAR(20) | N | 아래 코드 |
| `PARKING_LOT_ID` | INT | Y | FK → PARKING_LOT. 대기 장소에 도착했을 때(`UpdatePickupPlanWait`) 그 주차장. 다른 주차장으로 옮기면 바꾸고, 출발해도 비우지 않는다 |
| `START_DT` | DATETIME | Y | 경로를 시작한 시각 |
| `WAIT_START_DT` | DATETIME | Y | 대기 장소에 처음 도착한 시각. `UpdatePickupPlanWait`가 비어 있을 때만 `eventDt`로 채운다. 다른 주차장으로 옮기거나 출발했다가 다시 대기해도 바꾸지 않는다 |
| `STOP_DT` | DATETIME | Y | 처음 정차한 시각(잘못된 위치 포함). `UpdatePickupPlanStop`이 비어 있을 때만 `eventDt`로 채운다. 차를 옮겨 다시 서거나 한 바퀴 돌고 다시 서도 바꾸지 않는다 |
| `BOARD_DT` | DATETIME | Y | 탑승 감지 시각. `UpdatePickupPlanBoard`가 `eventDt`로 채운다(끝내기 UPDATE 안에서 함께 쓴다 — 계획 PK → `TIMETABLE_DAY` 잠금 뒤의 익명화 UPDATE 한 문장) |
| `CLOSE_DT` | DATETIME | Y | 계획이 끝난 시각 |

| `STATUS` | 뜻 |
|---|---|
| `PLANNED` | 배정됨, 출발 전. 계획을 경로 시작에서 만들므로 새로 생기지 않는다 [ADR-0023] |
| `STARTED` | 경로 시작, 픽업 지점으로 이동 중 |
| `WAITING` | 대기 장소에서 대기 중 |
| `STOPPED` | 픽업 지점 정차, 아이를 기다리는 중 |
| `LOOPING` | 한 바퀴 도는 중 |
| `BOARDED` | 탑승 완료 |
| `EXPIRED` | 탑승 감지 없이 `EXPIRE_DT`가 지남 |
| `CANCELED` | 부모가 취소했거나 시간표·아이가 삭제됨 |

- 아이 화면의 "곧 도착"(도착 1분 전)은 상태로 저장하지 않는다. 배정 시각과 현재 시각으로 계산한다.
- UNIQUE: `(TIMETABLE_DAY_ID, PICKUP_DATE)`. 진행 중에 같은 계획을 두 번 만들 수 없다. 끝난 계획은 `TIMETABLE_DAY_ID`가 NULL이라 걸리지 않으므로 `ACTIVE_FLAG`가 필요 없다. 중복 키 오류가 나면 이미 만들어진 계획을 시작한다(경로 시작).
- 탑승이나 만료로 끝난 뒤 다시 만드는 것은 `TIMETABLE_DAY.LAST_PICKUP_DATE`로 막는다. 부모 취소(`DeletePickupPlan`)는 이 값을 바꾸지 않아 그날 다시 만들 수 있다.
- 임의 좌표(`CUSTOM_*`)는 공유 지점이 아니라 정원 개념이 없다. 임의 좌표 계획은 `PICKUP_SPOT_ID`가 NULL이고, `CUSTOM_LAT`·`CUSTOM_LNG`·`CUSTOM_NAME`은 셋 다 있거나 셋 다 없다.
- CHECK:
  - `CK_PICKUP_PLAN_01`: `DELETE_YN = 1 OR (SLOT_DT IS NULL) = (PICKUP_SPOT_ID IS NULL AND CUSTOM_LAT IS NULL)` — 장소(지점 또는 임의 좌표)가 있으면 `SLOT_DT`가 있다. 익명화된 행은 장소 칸이 비어도 `SLOT_DT`가 남으므로 예외다
  - `CK_PICKUP_PLAN_02`: `(PICKUP_SPOT_ID IS NULL AND CUSTOM_LAT IS NULL) OR WALK_MIN IS NOT NULL OR DELETE_YN = 1` — 장소가 있으면 `WALK_MIN`이 있다. 익명화된 행은 예외다
  - `CK_PICKUP_PLAN_03`: `(CUSTOM_LAT IS NULL) = (CUSTOM_LNG IS NULL) AND (CUSTOM_LAT IS NULL) = (CUSTOM_NAME IS NULL)` — 임의 좌표 칸 셋은 함께 있거나 함께 없다
  - `CK_PICKUP_PLAN_04`: `PICKUP_SPOT_ID IS NULL OR CUSTOM_LAT IS NULL` — 지점과 임의 좌표는 동시에 있을 수 없다
- 인덱스: `(PICKUP_SPOT_ID, SLOT_DT)`, `(USER_ID, PICKUP_DATE)`
- 상태, 시각 컬럼, `PICKUP_EVENT`는 같은 일을 함께 적는다. 셋은 항상 같은 트랜잭션에서 쓴다.

**배정 시각.** 아이가 픽업 지점에 도착할 것으로 보는 시각이다. `하원 시각 + 정리 시간 + WALK_MIN`으로 계산한다. 정리 시간은 설정값이다(가정 1분). 경로 시작이 계획을 만들 때는 차 도착 예상(`eventDt + driveMin`, 분 단위 올림)보다 이르게 잡지 않는다. [ADR-0023]

**배정.** 같은 지점에서 배정 시각 앞뒤로 설정값(기본 2분) 안에 있는 진행 중 계획의 수가 `PICKUP_SPOT.CAPACITY`를 넘지 않게 한다. 진행 중 계획은 `STATUS`가 끝나지 않았고 `EXPIRE_DT`가 서버 현재 시각보다 뒤인 계획이다. 만료 처리가 아직 돌지 않아 `STATUS`가 남아 있어도 `EXPIRE_DT`가 지났으면 세지 않는다. 배정과 재배정은 아래 순서를 한 트랜잭션에서 지킨다. [ADR-0009]

1. 트랜잭션의 첫 문장으로 그 학원가의 `AREA` 행을 잠근다: `SELECT AREA_ID FROM AREA WHERE AREA_ID = ? FOR UPDATE`. 이 문장보다 먼저 다른 조회를 하지 않는다. 먼저 조회하면 그 시점의 스냅샷이 고정되어, 잠금을 얻고도 직전에 커밋된 계획을 보지 못한다.
2. 새 계획을 만들 때는 `TIMETABLE_DAY` 행을 `FOR UPDATE`로 읽어 삭제되지 않았는지, `PICKUP_YN`이 켜져 있는지, `LAST_PICKUP_DATE`가 그날이 아닌지 확인한다. 잠그지 않고 읽으면 탑승 처리나 삭제와 겹칠 때 계획이 잘못 만들어진다. 이때 시간표의 학원·학원가·하원 시각도 다시 읽는다. 1의 잠금을 기다리는 사이 시간표 편집이 학원을 다른 학원가로 바꿨으면 잠근 학원가가 틀리므로, 트랜잭션을 끝내고 처음부터 다시 한다.
3. `ACADEMY_PICKUP_SPOT.PRIORITY` 순으로 후보 지점을 본다. `USE_YN`이 꺼졌거나 아이 도착 시각이 `PICKUP_SPOT_BLOCK`에 걸리는 지점은 건너뛴다(하한을 적용하거나 늦춘 시각은 다시 보지 않는다).
4. 후보 지점에서 `SLOT_DT`가 배정 시각(계획을 만들 때는 아이 도착과 차 도착 예상 중 늦은 쪽, 장소 바꾸기에서 계획이 `STARTED`면 지금 `SLOT_DT`보다 이르지 않게)의 앞뒤 간격 안에 있는 진행 중 계획(`EXPIRE_DT` > 지금)을 세어 정원과 비교한다. 꽉 찼으면 배정 시각을 1분씩 늦춰 다시 보고(설정값까지, 가정 2분), 그래도 없으면 다음 지점으로 넘긴다.
5. 모든 후보가 안 되면 `PICKUP_SPOT_ID`와 `SLOT_DT`를 비운 채 계획을 만든다. 화면은 "가까운 픽업 장소를 찾지 못했어요"를 보여준다.
6. 계획을 넣거나 고치고 커밋한다.

- 일반 조회로 세고 넣으면 동시에 들어온 두 요청이 서로의 삽입을 보지 못해 정원을 넘는다. 그래서 `AREA` 행 잠금으로 한 학원가의 배정을 순서대로 처리한다.
- 잠금을 쥔 동안 문자 발송이나 경로 조회 같은 외부 호출을 하지 않는다.
- 잠금 조회는 Dapper로 쓴다.
- 출발한 계획만 정원을 잡는다. 계획은 경로 시작에서 만들어진다. `EXPIRE_DT`가 지나면 세지 않고, 만료 처리가 돌면 상태도 끝난다. [ADR-0023]
- 임의 좌표 계획은 3~5를 하지 않는다. 정원을 보지 않고 `PICKUP_SPOT_ID`는 NULL, `SLOT_DT`는 `하원 시각 + 정리 시간 + 도보 시간`(분 단위)이다. 경로 시작이 계획을 만들 때는 이 값과 차 도착 예상 중 늦은 쪽이고, 장소 바꾸기에서 계획이 `STARTED`면 지금 `SLOT_DT`보다 앞당기지 않는다. 만드는 트랜잭션은 위 순서(`AREA` 잠금 먼저)를 그대로 지킨다.
- 진행 중 계획을 다시 시작할 때(대기 뒤 출발) 차 도착 예상이 배정 시각보다 늦으면 배정 시각을 뒤로 옮겨 재배정한다. 새로 만든 계획은 이미 차 도착 예상 이후로 배정된다. 하원 시각을 오늘만 바꿔도 재배정한다.
- 늦은 출발의 재배정은 정원을 넘을 수 있다. [ADR-0021] 같은 지점에서 차 도착 예상 시각부터 `PickupPlan:SlotShiftMaxMin`까지 1분씩 늦춰 정원이 남는 첫 시각을 쓰고, 자리가 없으면 같은 지점에 차 도착 예상 시각으로 배정한다. 다른 지점으로 옮기지 않는다. 아이가 이미 나섰을 수 있고 차는 이미 출발했으므로 지점을 바꾸는 쪽이 더 위험하다. 임의 좌표 계획은 `SLOT_DT`를 차 도착 예상 시각으로 바꿀 뿐 정원이 없다.

**상태 바꾸기와 이벤트 넣기.** 계획의 상태를 바꾸거나 `PICKUP_EVENT`를 넣는 트랜잭션은 첫 문장에서 계획 행을 잠그고 현재 상태를 다시 읽는다: `SELECT … WHERE PICKUP_PLAN_ID = ? FOR UPDATE`. 이 조회에는 `DELETE_YN` 조건을 붙이지 않는다. 이미 끝난 계획이면 아무것도 하지 않는다. 예외는 둘이다. 계획을 만드는 트랜잭션(`PLANNED`·`STARTED` 이벤트)은 잠글 계획 행이 아직 없어 배정 절차의 `AREA` 잠금 안에서 한다. 아이·계정·시간표 삭제는 `AREA` 행을 먼저 잠근 뒤 계획 행들을 잠근다.

- 만료·탑승·취소가 겹쳐도 먼저 잠근 쪽만 적용된다.
- 한 계획의 이벤트가 번호 순서대로 커밋된다. 잠그지 않으면 번호가 작은 이벤트가 늦게 커밋되어 화면이 놓칠 수 있다.
- 재배정은 `AREA` 행을 먼저 잠그고 그다음 계획 행을 잠근다. 순서를 바꾸지 않는다.
- 삭제처럼 계획을 요일·아이·계정 쪽에서 찾아 잠글 때는 보조 인덱스(`TIMETABLE_DAY_ID`·`CHILD_ID`·`USER_ID`)로 잠금 조회하지 않는다. 계획을 끝내는 트랜잭션은 PK를 먼저 잠근 뒤 그 보조 인덱스 값을 NULL로 바꾸므로, 보조 인덱스 항목을 먼저 잠근 쪽과 교착한다(2026-11-11 로컬에서 `UK_PICKUP_PLAN_01`로 재현). 일반 조회로 후보 ID를 찾고 `FORCE INDEX (PRIMARY)`로 PK 순서대로 잠근 뒤 최신 값으로 다시 확인한다.
- 계획을 끝낼 때는 계획 행을 잠근 직후, 계획 행을 수정하기 전에 `TIMETABLE_DAY` 행을 잠근다. 그다음 계획을 수정하고, 탑승이나 만료로 끝나는 경우 `LAST_PICKUP_DATE`를 그 계획의 `PICKUP_DATE`로 바꾼다. 계획 행을 먼저 수정하면 같은 시간표로 계획을 만들려는 요청과 서로 기다릴 수 있다.
- 계획을 끝낼 때 `PICKUP_SESSION.CLOSE_DT`도 채운다.
- `UPDATE_USER`에는 그 변경을 일으킨 쪽의 역할을 넣는다. 부모가 취소했으면 `PARENT`, 만료 처리면 `SYSTEM`이다.

**만료 처리.** 주기 작업이 `EXPIRE_DT`가 지난 진행 중 계획을 찾아 `EXPIRED`로 끝낸다. 주기는 설정값이다.

**차량 이벤트로 상태 바꾸기.** 대기 장소 도착(`UpdatePickupPlanWait`), 정차(`UpdatePickupPlanStop`), 탑승(`UpdatePickupPlanBoard`), 부모 취소(`DeletePickupPlan`)는 위 "상태 바꾸기" 순서를 따른다. 재배정이 없어 `AREA`를 잠그지 않고, 첫 문장이 계획 행 PK 잠금이다. 탑승·취소는 계획을 끝내므로 그다음 `TIMETABLE_DAY`를 잠근다(계획 PK → `TIMETABLE_DAY` → 계획 UPDATE).

- `EVENT_DT`와 시각 컬럼(`WAIT_START_DT`·`STOP_DT`·`BOARD_DT`)에는 차량이 보낸 `eventDt`를 쓴다. 취소는 차량 시각을 받지 않아 `CANCELED` 이벤트의 `EVENT_DT`가 서버 시각이다. `CLOSE_DT`는 끝낸 서버 시각이라 탑승의 `EVENT_DT`(`eventDt`)가 `CLOSE_DT`보다 앞일 수 있다.
- 정차는 배정 장소(기본 지점 좌표, 임의 좌표면 그 좌표)에서 `PickupPlan:StopRadiusM`(기본 30m, 1 미만이면 기동 실패) 안이면 `STOPPED`, 밖이면 `WRONG_STOP` 이벤트다. 배정 장소가 없는 계획은 `STOPPED`다. 어느 쪽이든 `STATUS`는 `STOPPED`다. 정차 좌표와 거리는 판정에만 쓰고 저장하지 않는다.
- 취소의 취소 문자 대상(아이 번호·부모 이름)은 익명화 전에 같은 트랜잭션에서 읽어 두고, 문자는 커밋한 뒤 잠금 밖에서 보낸다. 결과는 이벤트로 남기지 않는다(`api.md` "상태 바꾸기").

**끝난 계획의 재요청.** 계획이 끝나면 `USER_ID`가 비어 소유를 확인할 수 없다. 응답을 못 받은 차량이나 오프라인 대기열이 같은 요청을 다시 보낼 수 있으므로, 위 네 API는 `PICKUP_EVENT`에 같은 계획·같은 `CLIENT_EVENT_ID`의 이벤트가 있고 그 유형이 그 API의 유형(대기 `WAIT_STARTED`, 정차 `STOPPED`·`WRONG_STOP`, 탑승 `BOARDED`, 취소 `CANCELED`)이면 처음 요청이 반영된 것으로 보고 200을 돌려준다. 이 확인은 `PLAN_CLOSED` 같은 409 판정보다 먼저이고 잠근 뒤에도 같다. 그 밖의 끝난 계획은 404다. 이 조회는 `PICKUP_EVENT`의 UNIQUE `(PICKUP_PLAN_ID, CLIENT_EVENT_ID)`를 쓰고 `DELETE_YN` 조건을 붙이지 않는다.

**익명화.** 계획이 `BOARDED`·`EXPIRED`·`CANCELED`가 되면 같은 트랜잭션에서 아래를 한다. [ADR-0008]

1. `USER_ID`·`CHILD_ID`·`TIMETABLE_DAY_ID`·`ACADEMY_ID`·`END_TIME`·`WALK_MIN`과 `CUSTOM_LAT`·`CUSTOM_LNG`·`CUSTOM_NAME`·`CUSTOM_WALK_PATH`를 NULL로 바꾼다.
2. 그 계획의 `END_TIME_CHANGED`·`LEAVE_SCHEDULED` 이벤트는 `DETAIL`을 NULL로 바꾼다. 하원 시각과 아이가 정한 출발 시각이 들어 있기 때문이다.
3. `DELETE_YN`을 켠다. `PICKUP_SESSION`·`PICKUP_EVENT`도 함께 켠다.

계획 행의 1과 3은 한 UPDATE로 한다. 나눠서 하면 `WALK_MIN`을 비우는 순간 CHECK에 걸린다. `CUSTOM_LAT`·`CUSTOM_LNG`·`CUSTOM_NAME`도 한 UPDATE에서 함께 비운다(`CK_PICKUP_PLAN_03`). 임의 좌표 계획은 `PICKUP_SPOT_ID`가 NULL이고 `SLOT_DT`가 남으므로, `DELETE_YN`을 켜지 않고 좌표만 비우면 `CK_PICKUP_PLAN_01`에 걸린다.

끝난 뒤 남는 값은 `AREA_ID`, `PICKUP_DATE`, `PICKUP_SPOT_ID`, `SLOT_DT`, `EXPIRE_DT`, `STATUS`, `PARKING_LOT_ID`, 시각 컬럼 다섯, 그리고 이벤트 이력이다. 임의 좌표 칸(`CUSTOM_*` 넷)은 남기지 않는다. 끝난 계획에 남는 장소 정보는 지점 ID뿐이다.

- 학원 참조와 하원 시각을 남기면 작은 학원은 날짜와 하원 시각만으로 아이가 좁혀진다. 그래서 비운다.
- 부모가 고른 임의 좌표는 학원 근처라 남기면 아이를 좁힐 수 있다. 그래서 좌표·이름·도보 경로 선을 비운다.
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
- `TOKEN_NONCE`는 16바이트 CSPRNG 난수의 소문자 hex 32자다. 토큰 원문은 HMAC 입력 문자열 `"pickup-link:" + PICKUP_PLAN_ID + ":" + TOKEN_NONCE`의 결과 32바이트를 base64url(패딩 없음) 43자로 쓴 값이다. `TOKEN_HASH`는 입력 문자열 `"pickup-link-token:" + 토큰 원문`의 HMAC을 소문자 hex 64자로 쓴 값이다. 접두어는 `security.md`의 다른 해시와 겹치지 않는다.
- 링크 주소는 설정값 `PickupLink:BaseUrl` + `/` + 토큰 원문이다.
- 링크를 열 때는 받은 토큰을 위 식으로 해시해 `TOKEN_HASH`로 찾는다. 없으면 "링크를 찾을 수 없어요"다.
- 세션은 링크 발송 API(`AddPickupLink`)가 만든다. 발송이 실패해도 세션은 남고, 다시 보낼 때 같은 링크를 쓴다. 다시 알림 API(`AddPickupRemind`)는 세션을 만들지 않는다.
- `SEND_CHANNEL`은 지금 항상 `SMS`다. 아이 정보의 채널이 `ALIMTALK`이어도 SMS로 보낸다.
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
| `PLANNED` | 계획 생성, 배정 | `{"pickupSpotId": 12 또는 null, "custom": true/false, "slotDt": "2026-11-12T22:04:00" 또는 null}` |
| `STARTED` | 경로 시작 | — |
| `WAIT_STARTED` | 대기 장소 도착 | `{"parkingLotId": 12}` |
| `END_TIME_CHANGED` | 오늘 하원 시각 변경 | 전·후 시각 |
| `REASSIGNED` | 지점·시각 재배정 | `{"reason": "LATE_START"·"LOCATION_CHANGED", "fromSpotId", "toSpotId", "fromCustom", "toCustom", "fromSlotDt", "toSlotDt"}` |
| `LINK_SENT` | 링크 발송 | 채널, 성공 여부, 발송 업체 결과 코드 |
| `LINK_OPENED` | 아이가 링크 열람 | — |
| `CHILD_LEAVING` | 아이가 "지금 나가요" | — |
| `LEAVE_SCHEDULED` | 아이가 출발 시각을 정하거나 바꿈 | 출발 시각 `{"leaveDt":"2026-11-12T22:03:00"}` |
| `LEAVE_CANCELED` | 아이가 출발 시각을 취소 | — |
| `STOPPED` | 배정 장소 반경 안 정차(배정 장소가 없는 계획의 정차 포함) | — |
| `WRONG_STOP` | 배정 장소 반경 밖에 정차 | `{"zoneTypes": ["NO_PARKING", …]}`(걸린 `ZONE_TYPE`, 없으면 빈 배열) 또는 `{"zoneTypes": null}`(판정 불가: 학원가 밖·구역 데이터 없음). 좌표·거리는 넣지 않는다 |
| `REMIND_SENT` | "도착했어요" 다시 알림 | 채널, 성공 여부, 발송 업체 결과 코드 |
| `LOOP_STARTED` | 한 바퀴 시작 | `LOOP_ROUTE_ID`, 다시 도착 시각 |
| `BOARDED` | 탑승 감지 | — |
| `EXPIRED` | 만료 | — |
| `CANCELED` | 취소 | — |

- `LINK_SENT`·`REMIND_SENT`의 `DETAIL`은 `{"channel":"SMS","success":true,"resultCode":"2000"}` 모양(`channel`, `success`, `resultCode`)이다. `resultCode`는 사업자의 결과 코드 문자열뿐이다(접수 `2000`, 개발 발송기 `DEV`, 시간 초과 `TIMEOUT`, 그 밖의 예외 `ERROR`).
  - 보내기 전에 `success`와 `resultCode`가 null인 진행 중 이벤트를 먼저 넣고 커밋한다. 잠금 밖에서 문자를 보낸 뒤 계획 행을 다시 잠그고 같은 행의 `DETAIL`을 결과로 고친다. 이 두 이벤트는 넣은 뒤 결과 칸을 채우려고 한 번 고친다.
  - `success`가 null이면 결과를 기다리는 진행 중 발송이다. `CREATE_DT`가 `PickupLink:InFlightTimeoutSec`(30초)보다 오래됐는데 결과가 없으면 실패로 센다.
  - 시도 횟수에는 성공·실패·진행 중을 모두 센다. 한도는 `PickupLink:MaxAttempts`(3)다.
- UNIQUE: `(PICKUP_PLAN_ID, CLIENT_EVENT_ID)`. 차량은 연결이 끊기면 이벤트를 쌓아 두었다가 다시 보낸다. 같은 이벤트가 두 번 와도 한 번만 들어간다. NULL은 겹쳐도 걸리지 않는다.
- 횟수 제한(다시 알림 1회, "지금 나가요" 1회)은 이 이력을 세어 Service에서 확인한다.
- 출발 시각은 `CHILD_LEAVING` 전까지 몇 번이고 바꿀 수 있다. 지금 상태는 마지막 `LEAVE_SCHEDULED`·`LEAVE_CANCELED`로 정한다.
- `DETAIL`에는 위 표에 적힌 값만 넣는다. 발송 업체 응답을 그대로 넣지 않는다. 수신 번호가 들어 있을 수 있다.
- `PLANNED`·`REASSIGNED`의 `DETAIL`에 임의 좌표, 장소 이름, 주소를 넣지 않는다. `custom` 플래그만 남긴다. 계획이 끝나 익명화한 뒤에도 이벤트 이력은 남기 때문이다.
- `EVENT_DT`: 차량이 일으킨 이벤트(`STARTED`, 사유가 `LATE_START`인 `REASSIGNED`, `WAIT_STARTED`, `STOPPED`, `WRONG_STOP`, `BOARDED`)는 차량이 보낸 `eventDt`다. 사유가 `LOCATION_CHANGED`인 `REASSIGNED`, `CANCELED`, 서버가 만든 이벤트(`PLANNED`, `EXPIRED` 등)는 서버 현재 시각이다.
- `BOARDED`·`CANCELED`의 `CLIENT_EVENT_ID`는 차량 요청의 값이다. 만료로 끝난 `EXPIRED`는 NULL이다.
- 이벤트의 순서와 "여기부터 새 이벤트" 기준은 `PICKUP_EVENT_ID`다. `EVENT_DT`는 차량 시각이고 늦게 도착할 수 있어 순서 기준으로 쓰지 않는다. 화면은 `PICKUP_PLAN_ID = ? AND PICKUP_EVENT_ID > ?`로 새 이벤트를 읽는다. 이벤트를 넣을 때 계획 행을 잠그므로 번호 순서와 커밋 순서가 같다.
- 인덱스는 위 UNIQUE 하나로 충분하다. `PICKUP_PLAN_ID`가 맨 앞이라 FK 인덱스를 겸한다. 새 이벤트 조회는 그 계획의 이벤트를 훑어 번호로 거른다. 계획 하나의 이벤트는 수십 건이라 따로 인덱스를 두지 않는다.

## DDL을 적용해 확인한 것

`backend/sql/20261110_initial_schema.sql`을 로컬 MySQL 8.4.7에 적용하고 스캐폴드해 확인했다(2026-11-10).

| 항목 | 결과 |
|---|---|
| 공간 타입 스캐폴드 | `POLYGON`·`LINESTRING` 컬럼은 건너뛴다. `AREA.BOUNDARY`, `LOOP_ROUTE.ROUTE_PATH`, `ACADEMY_PICKUP_SPOT.WALK_PATH`, `PICKUP_PLAN.CUSTOM_WALK_PATH`가 엔티티에 없다. `GEOMETRY`(`RESTRICTED_ZONE.GEOM`)는 점만 담는 타입으로 잡힌다 |
| 공간 컬럼이 있는 테이블의 쓰기 | `AREA`·`LOOP_ROUTE`는 NOT NULL 공간 컬럼이 엔티티에 없어 EF Core로 넣을 수 없다. `RESTRICTED_ZONE`도 선·면을 EF Core로 넣을 수 없다. 이 세 테이블의 등록·수정과 `WALK_PATH` 쓰기는 Dapper로 한다. 읽기는 원래 Dapper다 |
| 생성 컬럼 `ACTIVE_FLAG` | 스캐폴드는 일반 컬럼으로 잡는다. `RideMateContext.Extensions.cs`에서 저장 대상에서 빼면 EF Core 등록·수정이 되고 값도 읽혀 온다 |
| 살아 있는 행 UNIQUE | 살아 있는 행끼리는 중복이 거부되고, 삭제 표시한 행은 같은 값으로 여러 개 있을 수 있다 |
| 생성 컬럼이 낀 UNIQUE와 FK | FK 컬럼이 맨 앞인 UNIQUE가 있으면 InnoDB가 인덱스를 따로 만들지 않는다 |
| EF 트랜잭션 안의 Dapper | `BeginTransaction()`으로 연 트랜잭션을 `GetDbTransaction()`으로 꺼내 Dapper 명령에 넘기면 잠금 조회와 EF 저장이 한 트랜잭션에 들어간다. 롤백하면 둘 다 취소된다 |
| 공간 계산 | SRID 4326에서 선과 점, 면과 점 사이의 `ST_Distance`가 m 단위로 나온다. `ST_Contains`로 점이 구역 안인지 판정된다 |
| CHECK | 위반하는 값은 거부된다 |

잠금 순서(동시에 들어온 계획 생성과 탑승 처리)는 아직 실행해 보지 않았다. 시간표 삭제(T22)는 로컬에서 다른 세션이 `AREA`를 쥔 경우, 계획을 끝내는 트랜잭션(계획 PK → `TIMETABLE_DAY` → 계획 UPDATE)과 겹친 경우, 계획 생성(`AREA` → `TIMETABLE_DAY` → 계획 INSERT)이 먼저 잠근 경우를 손으로 흉내 내 교착 없이 기다렸다가 최신 상태로 판정하는 것을 확인했다(2026-11-11). 시간표 편집도 다른 세션이 지금·새 학원가의 `AREA`를 쥔 경우와 계획을 끝내는 트랜잭션이 뺄 요일의 계획을 쥔 경우에 기다렸다가 최신 상태로 판정하는 것을 확인했다(2026-11-11). 트랜잭션 밖 조회 뒤 같은 계정의 편집이 학원을 다른 학원가로 옮긴 경우에도 삭제·편집이 새 학원가의 `AREA`를 잠그려고 기다리는 것을 확인했다(2026-11-11). 계획 생성·끝내기 코드가 생기면 실제 코드로 다시 확인한다. 실제 코드로는 다른 세션이 `AREA`를 쥔 동안 시간표 삭제(T22)와 계획 생성(`AddPickupPlan`)을 함께 보내, 둘 다 `AREA`에서 기다렸다가 생성이 먼저면 삭제가 409(진행 중 계획), 삭제가 먼저면 생성이 101로 교착 없이 최신 상태로 판정하는 것을 확인했다(2026-11-12). 이 실행 확인은 계획 생성과 시간표 삭제의 조합뿐이다. 만료 주기 작업(계획 PK → `TIMETABLE_DAY` → UPDATE)과 시간표 삭제·계획 생성이 겹치는 경우는 코드를 읽어 잠금 순서가 위 규칙과 같은 것만 확인했고 실행해 보지 않았다.

## 아직 정하지 않은 것

기능 명세서의 "결정 필요" 시트는 반영하지 않았다. 그 시트에서 정해지면 여기서 지운다.

| 항목 | 영향 |
|---|---|
| 탈퇴 화면 | 명세서에 없다. 개인정보 안내는 "탈퇴 시까지 보관"이다 |
| 동의 기록 보관 기간 | 계정을 삭제한 뒤 `CONSENT`를 언제까지 두는지 |
| 개인정보 고지의 운전자 이름 | 가입 때 받는 운전자 이름을 개인정보 고지(RM-C-P1)의 수집 항목에 넣어야 한다 |
| 약관 문서 버전 | `CONSENT.TERMS_VERSION`의 형식이 정해지지 않았다. 지금은 서버 상수로 기록한다 |
| 픽업 후 목적지를 여러 개 둘지 | 지금은 계정당 하나다. 학원이나 요일마다 다르면 테이블로 나눈다 |
| 만료까지의 시간 | 배정 도착 시각 + 1시간(`PickupPlan:ExpireAfterHours`)으로 뒀다. 명세서는 만료 시점을 결정 필요로 적었다 |
| 만료된 링크의 화면 | 명세서 P-05는 "404와 같은 화면", 입력 검증과 상태 정의는 "이 링크는 끝났어요"다. 스키마는 뒤쪽을 지원한다 |
| 끝난 계획의 보관 | 명세서는 "세션은 종료 후 삭제"로 고지한다. 익명화한 행을 남기는 것을 고지 문구에 넣어야 한다 |
| 정리 시간, 앞뒤 간격, 늦추는 한도 | 가정값 1분(`DepartureGuide:TidyUpMin`), 2분(`PickupPlan:SlotWindowMin`), 2분(`PickupPlan:SlotShiftMaxMin`) |
| 인증번호 재발송 간격 | 가정값 1분 |
| 제외 반경 | `NO_PARKING`·`YELLOW_*`·`CCTV`·`RESIDENT_PARKING`의 `BUFFER_M`. 단속 카메라가 비추는 구간은 데이터에 없다 |
| 어린이보호구역 데이터의 모양 | 경계가 없고 시설 위치만 있으면 반경 300m로 넣는다 |
| 실시간 주차대수의 범위 | 시영 주차장만 제공한다. 강남구 운영 공영주차장이 포함되는지 |
| 장소 검색 API (보류) | 지금은 앱 SDK 검색을 쓰므로 서버의 장소 검색은 보류다. 서버가 쓰게 되면 어느 지도 사업자를 쓰는지. `PLACE_ID`의 형식과 대소문자 구분이 달라진다. 도보 경로는 카카오맵 `v2/routing/walk`로 정했지만 장소 검색 사업자는 아직 정하지 않았다 |
| 아이 도보 속도 | 보행자 경로 API가 실패했을 때 `WALK_MIN` 추정에 쓰는 설정값. `DepartureGuide:ChildWalkKmh` 4.0(가정)으로 뒀다 |
| 교통 소통 정보의 저장 | T42: 서버는 `TRAFFIC_PROFILE`만 읽는다 |
| 잠금 순서의 실제 검증 | 이 문서의 잠금 순서는 실행해 보지 않았다. 구현할 때 같은 시간표로 계획 생성과 탑승 처리를 동시에 보내 교착과 정원 초과가 없는지 확인한다 |
| DB 시간대 | 서버 코드와 DB의 현재 시각이 같은 시간대여야 한다. 배포처가 정해지면 맞춘다 |
