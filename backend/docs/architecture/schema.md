# 스키마 설계

RideMate 백엔드의 테이블 설계다. 테이블 규칙(이름, 감사 컬럼, 삭제 방식)은 `database.md`가 정본이고, 이 문서는 그 규칙 위에서 어떤 테이블과 컬럼을 두는지를 적는다. 실제 DDL은 `backend/sql/`에 쌓는다.

- 범위: 화면 V1~V10, M1~M5 전부(후순위 화면 포함).
- 결정 이유는 [ADR-0008]에 있다.

## 공통

- 모든 테이블에 감사 컬럼 `CREATE_DT`·`CREATE_USER`·`UPDATE_DT`·`UPDATE_USER`·`DELETE_YN`을 둔다. 아래 표에서는 생략한다.
- PK는 `INT AUTO_INCREMENT`다.
- 코드 값 컬럼은 `VARCHAR(20)`에 영문 대문자 코드를 넣는다. 코드 목록은 각 표에 적는다.
- 점 하나로 충분한 위치(학원, 픽업 지점, 주차장, 집, 회사)는 `LAT`·`LNG` `DECIMAL(10,7)` 두 컬럼으로 둔다.
- 선·면이 필요한 값(구역 경계, 제외 구역, 도보 경로, 한 바퀴 경로)은 MySQL 공간 타입에 SRID 4326으로 둔다.
- 참조 컬럼에는 FK 제약을 건다. FK 컬럼의 인덱스는 InnoDB가 자동으로 만들게 두지 않고 DDL에 이름을 붙여 적는다. 아래 표의 "인덱스"에는 FK 인덱스 말고 따로 필요한 것만 적는다.
- 구조 조건은 CHECK 제약으로 건다(범위, 함께 채워져야 하는 컬럼). 코드 값 목록은 CHECK로 걸지 않는다. 코드가 늘 때마다 DDL을 바꾸지 않기 위해서다.
- `LAT`·`LNG`로 공간 값을 만들 때는 `ST_GeomFromText(CONCAT('POINT(', LAT, ' ', LNG, ')'), 4326)` 하나만 쓴다. SRID 4326의 WKT는 위도, 경도 순서다.

### 살아 있는 행에만 거는 UNIQUE

삭제 표시된 행과 겹칠 수 있는 조합은 그대로 UNIQUE를 걸 수 없다. 대신 생성 컬럼 `ACTIVE_FLAG`를 두고 UNIQUE 키 끝에 넣는다.

```sql
ACTIVE_FLAG TINYINT GENERATED ALWAYS AS (IF(DELETE_YN = 0, 1, NULL)) VIRTUAL
```

삭제된 행은 `ACTIVE_FLAG`가 NULL이라 서로 겹쳐도 통과하고, 살아 있는 행끼리만 중복이 막힌다. 이 방식을 쓰는 테이블은 표에 "살아 있는 행 UNIQUE"로 적는다. 중복 키 오류는 Service에서 "이미 있음"으로 처리한다.

### 감사 컬럼에 넣는 값

`CREATE_USER`·`UPDATE_USER`에는 개인을 가리키는 값을 넣지 않는다. 역할만 넣는다.

| 값 | 쓰는 때 |
|---|---|
| `PARENT` | 차량 앱 요청 |
| `CHILD` | 모바일 웹(픽업 링크) 요청 |
| `SYSTEM` | 서버가 스스로 한 처리(배정, 만료, 익명화) |
| `ADMIN` | 운영팀이 넣은 기준 데이터 |

## 테이블 관계

```text
PARENT ──< CHILD ──< TIMETABLE >── ACADEMY >── AREA
                        │             │          │
                        │             │          ├──< PICKUP_SPOT ──< LOOP_ROUTE
                        │             └──< ACADEMY_PICKUP_SPOT >──┘
                        └──< TIMETABLE_EXCEPTION

PICKUP_SESSION ──< PICKUP_EVENT
       └──────── PICKUP_LINK (1:1)

RESTRICTED_ZONE, PARKING_LOT   (다른 테이블이 참조하지 않는 기준 데이터.
                                PICKUP_SESSION만 PARKING_LOT을 참조한다)
```

## 부모·아이·시간표

### PARENT — 부모(차량 앱 설치 단위)

로그인이 없다. 앱 첫 실행 때 서버가 `PARENT_KEY`를 발급하고 차량이 보관한다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PARENT_ID` | INT | N | PK |
| `PARENT_KEY` | CHAR(36) | N | 서버 발급 익명 키(UUID). UNIQUE |
| `VIN_HASH` | CHAR(64) | Y | 차대번호 SHA-256. 차량 SDK 권한 승인 뒤에만 채운다 |
| `CALL_NAME` | VARCHAR(20) | N | 아이에게 보이는 호칭(엄마, 아빠) |
| `CAR_MODEL` | VARCHAR(50) | Y | 차종. 부모 입력 |
| `CAR_COLOR` | VARCHAR(20) | Y | 차 색. 부모 입력 |
| `CAR_NO_LAST4` | CHAR(4) | Y | 차량 번호 뒷자리 |
| `HOME_LAT`·`HOME_LNG` | DECIMAL(10,7) | Y | 집 위치 |
| `WORK_LAT`·`WORK_LNG` | DECIMAL(10,7) | Y | 회사 위치 |
| `AUTO_SEND_YN` | TINYINT(1) | N | 링크 자동 발송 여부 |
| `WAKE_BEFORE_MIN` | TINYINT | N | 쉬는 모드에서 깨우는 시점(출발 몇 분 전, 0이면 출발 시각) |
| `LAST_ACCESS_DT` | DATETIME | N | 마지막 요청 시각. 오래 쓰지 않은 부모를 정리하는 기준 |

- 삭제할 때 `DELETE_YN`을 켜면서 `VIN_HASH`·차 정보·집·회사 위치를 NULL로 바꾼다. 아이 행도 함께 삭제한다. 복구는 할 수 없다.
- 앱을 다시 설치하면 새 `PARENT_KEY`가 발급되어 옛 행을 지울 사람이 없다. 그래서 `LAST_ACCESS_DT`가 일정 기간 지난 부모를 서버가 같은 방식으로 삭제한다.

### CHILD — 아이

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `CHILD_ID` | INT | N | PK |
| `PARENT_ID` | INT | N | FK → PARENT |
| `CHILD_NAME` | VARCHAR(50) | N | 이름 |
| `PHONE_ENC` | VARBINARY(255) | N | 휴대폰 번호. 암호화해서 저장한다 |
| `SEND_CHANNEL` | VARCHAR(20) | N | `SMS`, `ALIMTALK` |

- 화면에 내려줄 때는 Service에서 복호화한 뒤 가운데 자리를 가린다.
- 삭제할 때 `DELETE_YN`을 켜면서 `CHILD_NAME`을 빈 문자열로, `PHONE_ENC`를 빈 값으로 바꾼다. 복구는 할 수 없다.
- 인덱스: `(PARENT_ID)`

### ACADEMY — 학원

운영팀이 미리 넣은 학원과 부모가 추가한 학원이 같은 테이블에 있다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `ACADEMY_ID` | INT | N | PK |
| `AREA_ID` | INT | Y | FK → AREA. 지원 학원가 밖이면 NULL |
| `ACADEMY_NAME` | VARCHAR(100) | N | 이름 |
| `ADDRESS` | VARCHAR(200) | Y | 주소 |
| `LAT`·`LNG` | DECIMAL(10,7) | N | 위치 |
| `SOURCE_TYPE` | VARCHAR(20) | N | `TEAM`(운영팀), `NAVI`(내비 검색), `MANUAL`(직접 입력) |
| `NAVI_PLACE_ID` | VARCHAR(100) | Y | 내비 장소 ID. `NAVI`일 때만 |
| `OWNER_PARENT_ID` | INT | Y | FK → PARENT. `MANUAL`로 넣은 부모. 그 부모에게만 보인다 |

- `TEAM`·`NAVI` 학원은 모든 부모가 공유한다. `NAVI`는 `NAVI_PLACE_ID`가 같으면 기존 행을 쓴다.
- `AREA_ID`가 NULL인 학원은 시간표 등록과 출발 시각 안내만 되고, 픽업 지점·도착 시각 배정은 하지 않는다.
- 부모가 추가한 학원의 `AREA_ID`는 등록할 때 위치가 `AREA.BOUNDARY` 안인지로 정한다.
- 살아 있는 행 UNIQUE: `(NAVI_PLACE_ID, ACTIVE_FLAG)`. 두 부모가 같은 내비 장소를 동시에 추가해도 행이 하나만 생긴다. `NAVI_PLACE_ID`가 NULL인 행은 걸리지 않는다.

### TIMETABLE — 요일 시간표

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `TIMETABLE_ID` | INT | N | PK |
| `CHILD_ID` | INT | N | FK → CHILD |
| `ACADEMY_ID` | INT | N | FK → ACADEMY |
| `DAY_OF_WEEK` | TINYINT | N | 1(월)~7(일) |
| `END_TIME` | TIME | N | 하원 시각 |
| `PICKUP_YN` | TINYINT(1) | N | 픽업 스위치. 꺼져 있으면 알림·계산을 하지 않는다 |

- 요일을 여러 개 골라 추가하면 요일마다 한 행씩 만든다.
- 스위치가 켜진 행마다 그날 픽업이 하나씩 생긴다. 하루에 여러 건이 될 수 있다.
- CHECK: `DAY_OF_WEEK BETWEEN 1 AND 7`
- 인덱스: `(CHILD_ID, DAY_OF_WEEK)`

### TIMETABLE_EXCEPTION — 날짜별 예외

시간표 원본을 바꾸지 않고 특정 날짜만 다르게 한다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `TIMETABLE_EXCEPTION_ID` | INT | N | PK |
| `TIMETABLE_ID` | INT | N | FK → TIMETABLE |
| `TARGET_DATE` | DATE | N | 적용 날짜 |
| `EXCEPTION_TYPE` | VARCHAR(20) | N | `SKIP`(그날 건너뜀), `TIME_CHANGE`(하원 시각 변경) |
| `CHANGED_END_TIME` | TIME | Y | `TIME_CHANGE`일 때 바뀐 하원 시각 |

- 같은 시간표·같은 날짜에 유효한 행은 하나다. 다시 바꾸면 그 행을 수정한다.
- 살아 있는 행 UNIQUE: `(TIMETABLE_ID, TARGET_DATE, ACTIVE_FLAG)`
- CHECK: `(EXCEPTION_TYPE = 'TIME_CHANGE') = (CHANGED_END_TIME IS NOT NULL)`

## 기준 데이터

운영팀이 넣는다. 관리 화면은 없고 `backend/sql/`의 SQL로 넣는다.

### AREA — 학원가

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
| `CAPACITY` | TINYINT | N | 한 시각 칸에 받을 수 있는 차 수 |
| `USE_YN` | TINYINT(1) | N | 배정에 쓰는지 여부 |

- `CAPACITY`는 차 수다. 형제를 함께 태우는 차는 한 대로 센다.
- CHECK: `CAPACITY >= 1`

### ACADEMY_PICKUP_SPOT — 학원별 픽업 지점 후보

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `ACADEMY_PICKUP_SPOT_ID` | INT | N | PK |
| `ACADEMY_ID` | INT | N | FK → ACADEMY |
| `PICKUP_SPOT_ID` | INT | N | FK → PICKUP_SPOT |
| `WALK_MIN` | TINYINT | N | 학원에서 지점까지 아이 도보 시간(분) |
| `WALK_PATH` | LINESTRING SRID 4326 | Y | 도보 경로 선 |
| `PRIORITY` | TINYINT | N | 배정 우선순위. 1이 가장 먼저 |

- 부모가 추가한 학원이 학원가 안이면, 등록할 때 같은 `AREA`의 지점 중 직선 거리가 가까운 곳으로 행을 만들고 `WALK_PATH`는 비운다. 도보 시간은 거리로 추정한다.
- 학원과 지점은 같은 `AREA`여야 한다. 제약으로는 걸 수 없어 기준 데이터를 넣는 SQL에서 확인한다.
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

원천 데이터 7종을 한 테이블에 담는다. 지도 빨간 영역 표시와 정차 위치 경고에 쓴다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `RESTRICTED_ZONE_ID` | INT | N | PK |
| `ZONE_TYPE` | VARCHAR(20) | N | 아래 코드 |
| `GEOM` | GEOMETRY SRID 4326 | N | 점·선·면. SPATIAL INDEX |
| `BUFFER_M` | SMALLINT | N | `GEOM`에서 이 거리(m) 안까지 제외한다 |
| `ACTIVE_START_TIME` | TIME | Y | 적용 시작 시각. NULL이면 상시 |
| `ACTIVE_END_TIME` | TIME | Y | 적용 종료 시각 |
| `SOURCE_NAME` | VARCHAR(50) | Y | 원천 데이터 이름 |
| `SOURCE_REF` | VARCHAR(100) | Y | 원천 데이터의 관리번호 |

| `ZONE_TYPE` | 모양 | `BUFFER_M` | 적용 시간 |
|---|---|---|---|
| `SCHOOL_ZONE`(어린이보호구역) | 면 | 0 | 상시 |
| `INTERSECTION`(교차로 모퉁이) | 점 | 5 | 상시 |
| `CROSSWALK`(횡단보도) | 선 또는 면 | 미정 | 상시 |
| `BUS_STOP`(버스정류장) | 점 | 10 | 상시 |
| `FIRE_HYDRANT`(소화전) | 점 | 5 | 상시 |
| `YELLOW_DOUBLE`(황색 복선) | 선 | 0 | 상시 |
| `YELLOW_SINGLE`(황색 실선) | 선 | 0 | 안내판 시간대 |
| `CCTV`(단속 카메라) | 점 | 미정 | 상시 |

- CHECK: `(ACTIVE_START_TIME IS NULL) = (ACTIVE_END_TIME IS NULL)`, `BUFFER_M >= 0`
- "이 위치가 제외 구역인가"는 두 단계로 조회한다. 먼저 위치 둘레의 사각형(가장 큰 `BUFFER_M`만큼 넓힌 것)과 `MBRIntersects`로 후보를 추리고, 그 후보에만 `ST_Distance(GEOM, 점) <= BUFFER_M`을 적용한다. 거리 조건만 쓰면 공간 인덱스를 타지 않고 모든 행을 계산한다.
- 지원 학원가 둘레의 데이터만 넣는다. 전국 데이터를 넣지 않는다.

### PARKING_LOT — 대기 장소(주차장)

전국주차장정보표준데이터의 컬럼 구성을 따른다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PARKING_LOT_ID` | INT | N | PK |
| `LOT_NAME` | VARCHAR(100) | N | 이름 |
| `ADDRESS` | VARCHAR(200) | Y | 주소 |
| `LAT`·`LNG` | DECIMAL(10,7) | N | 위치 |
| `WEEKDAY_OPEN_TIME`·`WEEKDAY_CLOSE_TIME` | TIME | Y | 평일 운영 시간 |
| `SAT_OPEN_TIME`·`SAT_CLOSE_TIME` | TIME | Y | 토요일 운영 시간 |
| `HOLIDAY_OPEN_TIME`·`HOLIDAY_CLOSE_TIME` | TIME | Y | 공휴일 운영 시간 |
| `BASE_MIN` | SMALLINT | N | 기본 시간(분) |
| `BASE_FEE` | INT | N | 기본 요금(원) |
| `ADD_UNIT_MIN` | SMALLINT | Y | 추가 단위 시간(분) |
| `ADD_FEE` | INT | Y | 추가 단위 요금(원) |
| `DAY_PASS_FEE` | INT | Y | 1일 주차권 요금(원) |
| `TOTAL_SPACES` | SMALLINT | Y | 주차 면수 |
| `REALTIME_REF` | VARCHAR(100) | Y | 실시간 빈자리 API에서 쓰는 주차장 ID |
| `AVAILABLE_SPACES` | SMALLINT | Y | 마지막으로 조회한 빈자리 수 |
| `AVAILABLE_UPDATE_DT` | DATETIME | Y | 빈자리 수를 조회한 시각 |
| `SOURCE_REF` | VARCHAR(100) | Y | 표준데이터의 주차장 관리번호 |

- 추천할 때 실시간 API를 조회하고 결과를 `AVAILABLE_SPACES`·`AVAILABLE_UPDATE_DT`에 덮어쓴다. API가 실패하면 저장된 값을 쓴다.
- 빈자리 갱신은 조회 시각이 저장된 `AVAILABLE_UPDATE_DT`보다 나중일 때만 한다. 늦게 도착한 응답이 최신 값을 덮지 않게 한다.
- 빈자리 갱신은 `UPDATE_DT`·`UPDATE_USER`를 바꾸지 않는다. 감사 컬럼은 기준 데이터(요금, 운영 시간)를 고친 기록으로만 쓴다.
- 인덱스: `(LAT)`. 근처 주차장을 위도 범위로 먼저 추린다.

## 픽업 진행

### PICKUP_SESSION — 픽업 1건

차량에서 경로를 시작할 때 만든다. 그때 픽업 지점과 도착 시각 칸을 확정한다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PICKUP_SESSION_ID` | INT | N | PK |
| `PARENT_ID` | INT | Y | FK → PARENT. 끝나면 NULL |
| `CHILD_ID` | INT | Y | FK → CHILD. 끝나면 NULL |
| `TIMETABLE_ID` | INT | Y | FK → TIMETABLE. 끝나면 NULL |
| `ACADEMY_ID` | INT | Y | FK → ACADEMY. 끝나면 NULL |
| `AREA_ID` | INT | Y | FK → AREA. 학원의 학원가를 복사해 둔다. 학원가 밖 학원이면 NULL. 끝나도 남긴다 |
| `PICKUP_DATE` | DATE | N | 픽업 날짜 |
| `END_TIME` | TIME | N | 그날 적용된 하원 시각 |
| `PICKUP_SPOT_ID` | INT | Y | FK → PICKUP_SPOT. 배정 지점. 학원가 밖 학원이면 NULL |
| `SLOT_DT` | DATETIME | Y | 배정된 도착 시각(2분 칸) |
| `WALK_MIN` | TINYINT | Y | 배정 지점까지 아이 도보 시간(분) |
| `STATUS` | VARCHAR(20) | N | 아래 코드 |
| `PARKING_LOT_ID` | INT | Y | FK → PARKING_LOT. 대기 장소를 골랐을 때 |
| `WAIT_START_DT` | DATETIME | Y | 대기 장소 도착 시각 |
| `STOP_DT` | DATETIME | Y | 픽업 지점에 처음 정차한 시각. 한 바퀴 돌고 다시 서도 바꾸지 않는다 |
| `BOARD_DT` | DATETIME | Y | 탑승 감지 시각 |
| `CLOSE_DT` | DATETIME | Y | 세션이 끝난 시각 |

| `STATUS` | 뜻 |
|---|---|
| `STARTED` | 경로 시작, 픽업 지점으로 이동 중 |
| `WAITING` | 대기 장소에서 대기 중 |
| `STOPPED` | 픽업 지점 정차, 아이를 기다리는 중 |
| `LOOPING` | 한 바퀴 도는 중 |
| `BOARDED` | 탑승 완료 |
| `EXPIRED` | 탑승 감지 없이 만료 시각이 지남 |
| `CANCELED` | 부모가 취소 |

- UNIQUE: `(TIMETABLE_ID, PICKUP_DATE)`. 경로 시작 요청이 두 번 와도 세션은 하나만 생긴다. 끝난 세션은 `TIMETABLE_ID`가 NULL이라 걸리지 않으므로 `ACTIVE_FLAG`가 필요 없다. 중복 키 오류가 나면 이미 만들어진 세션을 돌려준다.
- CHECK: `(PICKUP_SPOT_ID IS NULL) = (SLOT_DT IS NULL)`
- 인덱스: `(PICKUP_SPOT_ID, SLOT_DT)`, `(PARENT_ID, PICKUP_DATE)`
- 상태, 시각 컬럼, `PICKUP_EVENT`는 같은 일을 함께 적는다. 셋은 항상 같은 트랜잭션에서 쓴다.

**배정.** 칸은 2분 단위다. 같은 지점·같은 칸의 진행 중인 차 수가 `PICKUP_SPOT.CAPACITY`를 넘지 않게 한다. 배정과 재배정은 아래 순서를 한 트랜잭션에서 지킨다. [ADR-0008]

1. 트랜잭션의 첫 문장으로 그 학원가의 `AREA` 행을 잠근다: `SELECT AREA_ID FROM AREA WHERE AREA_ID = ? FOR UPDATE`. 이 문장보다 먼저 다른 조회를 하지 않는다. 먼저 조회하면 그 시점의 스냅샷이 고정되어, 잠금을 얻고도 직전에 커밋된 세션을 보지 못한다.
2. 같은 부모의 진행 중 세션 가운데 같은 날짜에 같은 지점 후보를 쓰는 것이 있으면 그 지점과 칸을 그대로 쓴다. 형제를 한 차로 태우는 경우다.
3. 없으면 후보 지점·칸마다 진행 중인 세션을 `COUNT(DISTINCT PARENT_ID)`로 세어 정원과 비교한다. 꽉 찼으면 다음 칸 또는 `ACADEMY_PICKUP_SPOT.PRIORITY`가 다음인 지점으로 넘긴다.
4. 세션을 넣거나 고치고 커밋한다.

- 일반 조회로 세고 넣으면 동시에 들어온 두 요청이 서로의 삽입을 보지 못해 정원을 넘는다. 그래서 `AREA` 행 잠금으로 한 학원가의 배정을 순서대로 처리한다.
- 잠금을 쥔 동안 문자 발송이나 경로 조회 같은 외부 호출을 하지 않는다.
- 잠금 조회는 Dapper로 쓴다.
- 칸 길이는 설정값이다. 진행 중인 세션이 없을 때만 바꾼다. 도중에 바꾸면 길이가 다른 칸이 시간상 겹쳐도 다른 칸으로 세어진다.

**상태 바꾸기.** 상태를 바꾸는 트랜잭션은 첫 문장에서 세션 행을 잠그고 현재 상태를 다시 읽는다: `SELECT … WHERE PICKUP_SESSION_ID = ? FOR UPDATE`. 이미 끝난 세션이면 아무것도 하지 않는다. 만료·탑승·취소가 겹쳐도 먼저 잠근 쪽만 적용된다.

**익명화.** 세션이 `BOARDED`·`EXPIRED`·`CANCELED`가 되면 같은 트랜잭션에서 아래를 한다. [ADR-0008]

1. `PARENT_ID`·`CHILD_ID`·`TIMETABLE_ID`·`ACADEMY_ID`를 NULL로 바꾼다.
2. `DELETE_YN`을 켠다. `PICKUP_LINK`·`PICKUP_EVENT`도 함께 켠다.

남는 행은 학원가·픽업 지점·시각·상태뿐이라 익명 기록으로 쓴다. 학원 참조를 남기면 직접 입력한 학원은 등록한 부모로 이어지고, 작은 학원은 날짜와 하원 시각만으로 아이가 좁혀지므로 학원 종류와 관계없이 비운다. 통계는 `DELETE_YN`과 관계없이 이 행들을 읽는다. 이 조회만 `DELETE_YN = FALSE` 조건을 붙이지 않는다.

### PICKUP_LINK — 픽업 링크

세션 하나에 링크 하나다. 다시 알릴 때도 같은 링크를 보낸다. 토큰은 저장하지 않고 필요할 때 다시 계산한다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PICKUP_LINK_ID` | INT | N | PK |
| `PICKUP_SESSION_ID` | INT | N | FK → PICKUP_SESSION. UNIQUE |
| `TOKEN_NONCE` | CHAR(32) | N | 토큰을 만들 때 쓰는 난수(16바이트 hex) |
| `TOKEN_HASH` | CHAR(64) | N | 링크 토큰의 SHA-256. UNIQUE |
| `SEND_CHANNEL` | VARCHAR(20) | N | 보낸 채널. `SMS`, `ALIMTALK` |
| `EXPIRE_DT` | DATETIME | N | 만료 시각. 발송 시각에 설정값(기본 1시간)을 더한다 |
| `CLOSE_DT` | DATETIME | Y | 링크가 끝난 시각 |

- 토큰은 `HMAC-SHA256(서버 비밀키, PICKUP_LINK_ID와 TOKEN_NONCE)`다. 원문은 저장하지 않는다. 다시 알릴 때는 같은 식으로 다시 계산해 같은 주소를 보낸다.
- 링크를 열 때는 받은 토큰을 해시해 `TOKEN_HASH`로 찾는다.
- 서버 비밀키는 환경변수로 주입한다(`../security.md`).
- 탑승하면 바로 `CLOSE_DT`를 채운다. 탑승이 감지되지 않아도 `EXPIRE_DT`가 지나면 끝난 링크로 본다.
- 끝난 뒤에도 `TOKEN_HASH`는 남긴다. 끝난 링크를 다시 열었을 때 "이 링크는 끝났어요"를 보여주기 위해서다. 이 조회도 `DELETE_YN` 조건을 붙이지 않는다.
- 전화번호는 이 테이블에 저장하지 않는다. 발송할 때 `CHILD.PHONE_ENC`를 읽어 쓴다.

### PICKUP_EVENT — 픽업 중 일어난 일

차량 화면과 아이 화면이 같은 이력을 읽어 상태를 맞춘다.

| 컬럼 | 타입 | NULL | 설명 |
|---|---|---|---|
| `PICKUP_EVENT_ID` | INT | N | PK |
| `PICKUP_SESSION_ID` | INT | N | FK → PICKUP_SESSION |
| `EVENT_TYPE` | VARCHAR(20) | N | 아래 코드 |
| `EVENT_DT` | DATETIME | N | 일어난 시각 |
| `DETAIL` | JSON | Y | 유형별 부가 값 |

| `EVENT_TYPE` | 뜻 | `DETAIL` |
|---|---|---|
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

- `DETAIL`에는 위 표에 적힌 값만 넣는다. 발송 업체 응답을 그대로 넣지 않는다. 수신 번호가 들어 있을 수 있다.
- 이벤트의 순서와 "여기부터 새 이벤트" 기준은 `PICKUP_EVENT_ID`다. `EVENT_DT`는 초 단위라 같은 초에 일어난 이벤트의 순서를 가리지 못한다. 화면은 `PICKUP_SESSION_ID = ? AND PICKUP_EVENT_ID > ?`로 새 이벤트를 읽는다.
- 인덱스는 FK 인덱스 `(PICKUP_SESSION_ID)` 하나로 충분하다. 보조 인덱스에는 PK가 붙어 있어 위 조회를 그대로 처리한다.

## 아직 정하지 않은 것

| 항목 | 영향 |
|---|---|
| API 인증 방식 | `PARENT_KEY`를 그대로 요청에 싣는지, 토큰으로 바꾸는지 |
| 전화번호 암호화 방식과 키 보관 | `PHONE_ENC` 길이, 설정 항목 |
| 차량 SDK의 차대번호 권한 승인 여부 | `VIN_HASH`를 채울 수 있는지 |
| 실시간 빈자리 API 출처 | `REALTIME_REF`에 넣을 값 |
| 횡단보도·단속 카메라 제외 반경 | `RESTRICTED_ZONE.BUFFER_M` 기본값 |
| EF Core 공급자의 공간 타입 스캐폴드 지원 | 지원하지 않으면 공간 컬럼은 Dapper로만 읽고 쓴다. 공간 컬럼이 있는 테이블 하나를 먼저 스캐폴드해 확인한다 |
| 생성 컬럼(`ACTIVE_FLAG`)의 스캐폴드 결과 | 쓰기에서 빠지는 컬럼으로 잡히는지. 아니면 EF 저장이 실패한다 |
| EF 트랜잭션 안에서 Dapper 쓰기 | 배정·상태 바꾸기의 잠금 조회가 EF 저장과 같은 트랜잭션에 들어가는 방법 |
| 만료를 누가 처리하는지 | 주기 작업인지 조회할 때 판정하는지. 조회할 때만 하면 아무도 열지 않은 세션은 익명화되지 않는다 |
| 링크 만료 1시간의 기준 시각 | 발송 시각 기준이면 일찍 출발해 대기하는 경우 탑승 전에 만료될 수 있다. 다시 알릴 때 늘릴지도 정해야 한다 |
| 오래 정차한 차의 정원 | 2분 넘게 서 있거나 한 바퀴 도는 차를 다음 칸 정원에 넣을지 |
| 오래 쓰지 않은 부모를 삭제하는 기간 | `LAST_ACCESS_DT` 기준 값 |
| `PARENT_KEY` 저장 형태 | 인증에 그대로 쓰면 `TOKEN_HASH`처럼 해시로 저장해야 한다 |
| `VIN_HASH` 용도와 해시 방식 | 재설치 뒤 부모를 다시 찾는 데 쓰면 인덱스가 필요하다. 차대번호는 형식이 정해져 있어 비밀값 없이 해시하면 되짚을 수 있다 |
| 집·회사 위치 암호화 | 전화번호는 암호화하고 집 위치는 그대로 둔 상태다 |
| 황색 실선 적용 시간의 형태 | 요일 구분이나 하루 두 구간이면 시각 컬럼 한 쌍으로 부족하다 |
| DB 시간대 | 서버 코드와 DB의 현재 시각이 같은 시간대여야 한다. 배포처가 정해지면 맞춘다 |
| 서버가 출발 알림을 직접 보내는지 | 서버가 전체 시간표를 요일로 훑는다면 `TIMETABLE (DAY_OF_WEEK, PICKUP_YN)` 인덱스가 필요하다 |
