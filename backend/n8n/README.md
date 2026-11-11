# 도로 소통 수집기

대치동 학원가의 서울 TOPIS 소통 정보와 평촌 학원가의 경기도 GITS 소통 정보를 n8n 워크플로로 수집해 같은 compose의 MySQL에 저장한다.

## 띄우기

`backend/`에서 다음 명령을 실행한다.

```sh
docker compose up -d n8n
```

n8n 화면은 `http://127.0.0.1:5678`에서 연다. n8n은 compose의 MySQL이 정상 상태가 된 뒤 시작하며, 컨테이너 안에서는 `mysql:3306`의 `ridemate` 데이터베이스에 `ridemate` 사용자로 접속한다.

## MySQL 자격 증명

n8n이 뜬 뒤 `backend/`에서 다음 명령을 실행한다.

```sh
sh n8n/setup-credentials.sh
```

스크립트는 `.env`의 `MYSQL_PASSWORD`로 id `rmMysqlCred00001`, 이름 `ridemate-mysql`인 MySQL 자격 증명을 가져온다. 비밀번호는 저장소 파일이나 명령줄 인자에 남기지 않는다. 같은 id로 다시 실행하면 기존 자격 증명을 덮어쓴다.

## 처음 설치 (새로 클론했거나 `n8n-data` 볼륨을 잃었을 때)

자격 증명을 넣은 뒤 `backend/`에서 워크플로 네 개를 가져오고 수집기 두 개를 활성화한다. JSON은 `active: false`로 저장돼 있어 가져오기만 하면 수집이 돌지 않는다.

```sh
for f in n8n/workflows/*.json; do
  docker compose exec -T n8n sh -c 'cat > /tmp/wf.json && n8n import:workflow --input=/tmp/wf.json; rc=$?; rm -f /tmp/wf.json; exit $rc' < "$f"
done
docker compose exec -T n8n n8n publish:workflow --id=topisCollect0001
docker compose exec -T n8n n8n publish:workflow --id=gitsCollect00001
docker compose restart n8n
docker compose logs n8n --since 1m | grep "published workflows"
```

마지막 명령에 `2 published workflows`가 보여야 한다. CLI로 `publish:workflow`를 해도 떠 있는 n8n은 스케줄을 다시 읽지 않으므로 반드시 재시작한다(2026-11-11 실측: 재시작 전까지 회차가 기록되지 않았다). 링크 목록 만들기(`topisLinkBuild01`, `gitsLinkBuild001`)는 수동 워크플로라 활성화하지 않는다. 화면에서 실행하거나 아래 "5분 수집"의 `n8n execute` 명령에서 id만 바꿔 실행한다.

## API 키

`.env`의 `TOPIS_API_KEY`와 `GITS_API_KEY`에 각 제공처에서 발급받은 키를 넣는다. 발급 화면에 표시된 키 문자열을 그대로 넣는다(이미 URL 인코딩된 형태로 바꾸지 않는다). 빈 문자열이면 해당 소스의 키가 없는 것으로 처리한다. 키를 바꾼 뒤에는 `backend/`에서 컨테이너를 다시 생성해 환경변수를 반영한다.

```sh
docker compose up -d n8n
```

워크플로는 키를 각각 `$env.TOPIS_API_KEY`, `$env.GITS_API_KEY`로 읽는다. 키는 워크플로 JSON, 명령줄 인자, 로그, 실행 이력이나 `ERROR_MESSAGE`에 넣지 않는다.

## 링크 목록 만들기 — TOPIS

`TOPIS_API_KEY`를 넣은 뒤 한 번 실행하고, 대치동 중심·반경이나 대상 구가 바뀌었을 때 다시 실행한다. 수동 워크플로 `TOPIS 링크 목록 만들기`의 첫 Code 노드 `설정`에 `centerX`, `centerY`, `radiusM`, `regionPrefixes`가 모여 있다.

워크플로는 `LinkWithLoad`에서 얻은 링크 중 링크 ID 앞 세 자리가 `regionPrefixes`에 포함된 링크만 `LinkVerInfo`로 조회해 외부 호출 수를 줄인 뒤, TM 정점과 중심 사이의 실제 거리가 `radiusM` 이하인 링크만 저장한다. 대상 구를 바꿀 때는 `regionPrefixes`를 해당 구 코드 목록으로 바꾸고, 필요한 경우 중심 좌표와 반경도 함께 바꾼다. 접두사 필터는 호출을 줄이기 위한 사전 필터이며 최종 범위 판정을 대신하지 않는다.

TOPIS 링크 ID 앞 세 자리는 `LinkInfo`의 구 코드(`reg_cd`)와 같다. 기본값 `121`·`122`·`123`은 서초구·강남구·송파구다(샘플 응답의 도로 위치로 확인: `121` 서초대로·서운로, `122` 분당수서로 청담대교 남단, `123` 석촌호수로).

결과 링크가 0개면 기존 목록을 건드리지 않고 `NO_LINKS_IN_RESULT`로 끝난다. 공개 샘플 키(`sample`)는 5행만 줘서 대치동 링크가 나오지 않으므로 이 결과가 정상이다. TOPIS가 한 건이라도 `ERROR-500`이나 타임아웃으로 실패하면 목록을 바꾸지 않고 실패로 끝나므로, 일시적인 서버 오류면 다시 실행한다. TOPIS는 가끔 10초 넘게 응답해서, 이 워크플로의 HTTP 타임아웃은 30초다(5분 수집은 회차가 겹치지 않게 10초).

2026-11-11 실제 키 실행: 후보 942개 정점 조회(약 7분) → 반경 안 114개(중심 거리 172~1,471m, 삼성로·영동대로·선릉로·남부순환로·도곡로·역삼로·개포로 등 14개 도로).

실행 뒤 살아 있는 TOPIS 링크 수와 중심 거리 범위는 다음 SQL로 확인한다.

```sql
SELECT COUNT(*), MIN(CENTER_DIST_M), MAX(CENTER_DIST_M)
FROM TRAFFIC_LINK
WHERE SOURCE = 'TOPIS' AND DELETE_YN = 0;
```

## 링크 목록 만들기 — GITS

`GITS_API_KEY`를 넣은 뒤 수동 워크플로 `GITS 링크 목록 만들기`를 한 번 실행한다. 첫 Code 노드 `설정`에 두 값이 있다.

- `routeNames`: GITS 주요도로 목록(`getRoadInfoList`, 154개)의 실제 이름. 기본값은 `안양판교로/관악대로`, `국도 1호선(평택~안양)`, `국도 47호선(안산~서울)`이다. 평촌 학원가 안쪽 도로(평촌대로·시민대로·귀인로·흥안대로)는 GITS 주요도로에 없다(2026-11-11 실측).
- `nodeKeywords`: GITS는 도로 전 구간(세 도로 합계 약 2,000개 링크)을 주고 링크 좌표는 주지 않는다. 시작·끝 지점명에 이 낱말(`평촌`·`범계`·`안양`·`비산`·`인덕원`·`호계`·`관양` 등)이 든 링크만 남긴다. 2026-11-11 실행 결과 184개다. 지점명에 지명이 없는 링크(`속성변화점` 등)는 빠질 수 있다.

실행의 마지막 `성공 결과` 노드에서 `missingRouteNames`를 확인한다. 값이 비어 있지 않으면 GITS 도로 목록에 없었던 설정 이름이므로, 실제 도로 이름과 평촌 대상 범위를 확인한 뒤 `routeNames`를 고친다. 도로별 살아 있는 링크 수는 다음 SQL로 확인한다.

```sql
SELECT ROAD_NAME, COUNT(*)
FROM TRAFFIC_LINK
WHERE SOURCE = 'GITS' AND DELETE_YN = 0
GROUP BY ROAD_NAME;
```

## 키를 받은 뒤

### TOPIS

1. 발급 페이지에서 일일 호출 한도와 데이터 이용 조건을 확인한다.
2. `.env`의 `TOPIS_API_KEY`에 키를 넣고 `docker compose up -d n8n`으로 컨테이너를 다시 생성한다.
3. `TOPIS 링크 목록 만들기`를 실행하고 대치동 범위의 링크가 만들어졌는지 확인한다.
4. 5분마다 링크 수만큼 호출하므로 `링크 수 × 120`이 일일 한도 안인지 확인한다. 넘으면 수집 범위나 시간대를 줄인다.
5. 다음 TOPIS 회차의 `STATUS`가 `OK`인지 확인한다.

### GITS

1. 발급 페이지에서 일일 호출 한도와 데이터 이용 조건을 확인한다.
2. `.env`의 `GITS_API_KEY`에 키를 넣고 `docker compose up -d n8n`으로 컨테이너를 다시 생성한다.
3. `GITS 링크 목록 만들기`를 실행하고 도로 이름이 실제 응답과 맞는지, 평촌 대상 도로와 링크가 나왔는지 확인한다.
4. 5분마다 서로 다른 도로 수만큼 호출하므로 `도로 수 × 120`이 일일 한도 안인지 확인한다. 넘으면 수집 범위나 시간대를 줄인다.
5. 다음 GITS 회차의 `STATUS`가 `OK`인지 확인한다.

## GITS 키 받은 뒤 확인할 것

2026-11-11 실제 키로 확인한 것:

- 응답은 XML이고 `ServiceResult/msgBody/itemList`가 반복된다.
- `collDate`는 `2026-11-11 16:05:00.0`처럼 소수 초가 붙는다.
- `trvlTime`은 초다(72m 링크를 21km/h로 13초).
- 5분 수집 첫 회차: 184개 중 180개 저장, 4개는 응답에 없음(`LINK_NOT_IN_RESPONSE`).

남은 확인:

- 일일 호출 한도와 이용 조건
- 같은 이름의 도로가 여러 `routeId`로 나오는지(다른 시·군의 같은 이름 도로). 나오면 `SELECT ROUTE_ID, ROAD_NAME, COUNT(*) FROM TRAFFIC_LINK WHERE SOURCE = 'GITS' AND DELETE_YN = 0 GROUP BY ROUTE_ID, ROAD_NAME`으로 보고 평촌과 무관한 도로를 뺄 방법을 정한다
- 세 API(`getRoadInfoList`·`getRoadLinkInfoList`·`getRoadLinkTrafficInfoList`)가 결과를 페이지로 나누는지(`numOfRows`·`pageNo` 같은 인자). 나눈다면 `missingRouteNames`나 `LINK_NOT_IN_RESPONSE`가 계속 나온다

결과 링크가 0개면(도로 이름이 모두 틀렸거나 도로에 링크가 없을 때) 기존 목록을 건드리지 않고 `NO_LINKS_IN_RESULT`로 끝난다.

## 5분 수집

TOPIS와 GITS 수집 워크플로는 매일 14:00부터 23:55까지 5분마다 실행한다. 각 워크플로는 소스별 수집 대상 링크의 속도와 여행 시간을 `TRAFFIC_SPEED`에 저장하고, 같은 5분 회차의 처리 결과를 `TRAFFIC_COLLECT_RUN`에 한 행으로 남긴다. 같은 소스와 회차 시각으로 다시 실행해도 회차와 속도 행은 중복으로 쌓이지 않는다.

회차의 `STATUS`는 다음 뜻이다.

- `OK`: 대상 링크를 모두 저장했다.
- `PARTIAL`: 대상 링크 중 일부만 저장했다.
- `NO_KEY`: 해당 소스의 API 키가 비어 있어 외부 API를 호출하지 않았다.
- `FAILED`: 대상 링크가 없거나 저장된 링크가 한 건도 없다.

속도는 0~300km/h 밖이면 비정상 값(`INVALID_DATA`)으로 보고 저장하지 않는다. 회차 행을 넣거나 링크 목록을 읽는 DB 단계가 실패하면(자격 증명 없음, DB 중지) 실행이 오류로 끝나 n8n 실행 이력에 남는다. 이 단계에서는 아직 외부 호출 전이라 실행 데이터에 키가 없다.

TOPIS 워크플로를 수동으로 실행하려면 `backend/`에서 다음 명령을 실행한다.

```sh
docker compose exec -T -e N8N_RUNNERS_BROKER_PORT=5691 n8n n8n execute --id=topisCollect0001
```

떠 있는 n8n이 작업 브로커 포트 5679를 쓰므로, `n8n execute`에는 다른 브로커 포트를 줘야 한다. 그렇지 않으면 "port 5679 is already in use"로 실행되지 않는다.

워크플로 JSON을 고쳤으면 다시 가져온 뒤 활성화하고 재시작한다. JSON은 `active: false`로 저장돼 있으므로 수집 워크플로는 가져올 때마다 다시 publish한다.

```sh
docker compose exec -T n8n sh -c 'cat > /tmp/wf.json && n8n import:workflow --input=/tmp/wf.json; rc=$?; rm -f /tmp/wf.json; exit $rc' < n8n/workflows/topis-collect.json
docker compose exec -T n8n n8n publish:workflow --id=topisCollect0001
docker compose restart n8n
```

MySQL 노드의 쿼리 파라미터는 `$1`부터 쿼리에 나오는 순서대로 한 번씩만 쓰고, 값은 `={{ [a, b] }}` 배열로 넘긴다. n8n은 `$n`을 모두 `?`로 바꾸고 값을 번호순으로 채우므로, 순서가 어긋나거나 같은 번호를 두 번 쓰면 값이 엇갈린다. `queryBatching: independently`는 쓰지 않는다(여러 행을 넣을 때 실패한다).

### 수집 공백 확인

다음 SQL의 시작일과 종료일을 확인할 범위로 바꿔 실행한다. 날짜 범위의 매일 14:00~23:55에 해당하는 5분 칸을 만들고, TOPIS·GITS 각 소스에서 `TRAFFIC_COLLECT_RUN` 행이 없는 칸만 출력한다.

```sql
WITH RECURSIVE
params AS (
    SELECT DATE('2026-11-01') AS from_date, DATE('2026-11-07') AS to_date
),
dates AS (
    SELECT from_date AS collect_date
    FROM params
    UNION ALL
    SELECT DATE_ADD(d.collect_date, INTERVAL 1 DAY)
    FROM dates d
    CROSS JOIN params p
    WHERE d.collect_date < p.to_date
),
slots AS (
    SELECT 0 AS slot_index
    UNION ALL
    SELECT slot_index + 1
    FROM slots
    WHERE slot_index < 119
),
sources AS (
    SELECT 'TOPIS' AS source
    UNION ALL
    SELECT 'GITS' AS source
),
expected AS (
    SELECT
        s.source,
        DATE_ADD(
            DATE_ADD(CAST(d.collect_date AS DATETIME), INTERVAL 14 HOUR),
            INTERVAL sl.slot_index * 5 MINUTE
        ) AS run_dt
    FROM dates d
    CROSS JOIN slots sl
    CROSS JOIN sources s
)
SELECT e.source, e.run_dt
FROM expected e
LEFT JOIN TRAFFIC_COLLECT_RUN r
    ON r.SOURCE = e.source
   AND r.RUN_DT = e.run_dt
   AND r.DELETE_YN = 0
WHERE r.COLLECT_RUN_ID IS NULL
ORDER BY e.run_dt, e.source;
```

## 출처와 이용 조건

- 서울 TOPIS 데이터는 공공누리 제1유형으로, 출처를 표시하면 상업적 이용과 변경이 가능하다.
- 경기도 GITS 데이터의 이용 조건은 API 키를 발급받을 때 확인한다.
