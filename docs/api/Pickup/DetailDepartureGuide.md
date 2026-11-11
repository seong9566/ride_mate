# 출발 안내 계산

`GET /api/v1/Pickup/M/sign/DetailDepartureGuide`

오늘 남은 픽업 하나에 대해 아이가 픽업 장소에 도착하는 시각, 아이와 동시에 도착하려면 언제 출발해야 하는지, 그 장소가 단속 구역인지를 계산해 돌려준다. 앱이 현재 시각·위치·차량 소요 시간을 보내면 서버가 시간표·픽업 지점·단속 구역을 합친다. 계산 전용이라 DB에 아무것도 쓰지 않는다.

## Header

- Authorization(Bearer jwt) : mandatory

## Request

query 값이다. 본문은 없다.

- currentDt (string): 앱의 현재 시각. KST 로컬 시각을 오프셋 없이 쓴다(예: `2026-11-11T21:14:00`). `Z`나 `+09:00`을 붙이지 않는다. 오늘 날짜·요일과 남은 여유 계산의 기준이다 — 필수
- currentLat (number): 차량 현재 위도. -90~90 — 필수
- currentLng (number): 차량 현재 경도. -180~180 — 필수
- driveMin (integer): 내비(NaviHelper)로 구한, 차량이 픽업 장소까지 가는 시간(분). 0~300. 없으면 서버가 직선 거리로 추정한다 — 선택
- pickupLat (number): 부모가 바꾼 픽업 장소 위도. -90~90. `pickupLng`와 함께 보낸다 — 선택
- pickupLng (number): 부모가 바꾼 픽업 장소 경도. -180~180. `pickupLat`와 함께 보낸다 — 선택

`pickupLat`와 `pickupLng`는 둘 다 보내거나 둘 다 보내지 않는다. 없으면 서버가 학원의 기본 픽업 지점을 고르고, 있으면 그 좌표로 계산한다.

```text
?currentDt=2026-11-11T21:14:00&currentLat=37.4900&currentLng=127.0500
```

```text
?currentDt=2026-11-11T21:15:00&currentLat=37.4900&currentLng=127.0500&driveMin=34
```

```text
?currentDt=2026-11-11T21:15:00&currentLat=37.4900&currentLng=127.0500&driveMin=34&pickupLat=37.5025&pickupLng=127.0625
```

오늘 남은 픽업은 이렇게 고른다.

- 로그인한 계정의 살아 있는 아이 시간표 중 오늘 요일에 픽업하는 것이 후보다.
- 오늘 이미 픽업을 마친 것과 오늘 하루 건너뛰기로 한 것은 뺀다. 오늘만 하원 시각을 바꿨으면 바꾼 시각을 쓴다.
- 후보 중 하원 시각 + 1시간이 `currentDt`보다 늦은 것(하원한 지 1시간이 지나지 않았거나 아직 하원 전)만 남기고, 하원 시각이 가장 이른 하나를 고른다. 하원 전이라도 오늘 첫 픽업을 돌려준다.

## Response

data (object): 계산 결과

- academyName (string): 부모가 지정한 학원 이름
- endTime (string): 오늘 적용된 하원 시각. `HH:mm`
- pickup (object): 픽업 장소. 쓸 수 있는 기본 지점이 없으면 null
  - pickupSpotSeq (integer): 기본 지점이면 픽업 지점 ID, 부모가 보낸 좌표(`pickupLat`·`pickupLng`)이면 null
  - spotName (string): 기본 지점 이름. 부모가 보낸 좌표이면 null
  - lat (number): 픽업 장소 위도
  - lng (number): 픽업 장소 경도
  - walkMin (integer): 아이가 학원에서 픽업 장소까지 걷는 시간(분)
  - walkSource (string): `walkMin`의 출처. 아래 표
  - walkPath (array of objects): 학원 → 픽업 장소 도보 경로 선. 학원 쪽 점부터 픽업 장소 쪽 점 순서다. 경로가 없거나 `walkSource`가 `ESTIMATE`면 null
    - lat (number): 위도
    - lng (number): 경도
- childArrivalDt (string): 아이가 픽업 장소에 도착하는 시각(`2026-11-11T22:04:00` 형식, 오프셋 없는 KST). `pickup`이 null이면 null
- driveMin (integer): 계산에 쓴 차량 소요 시간(분). `pickup`이 null이면 null
- driveSource (string): `driveMin`의 출처. 아래 표. `pickup`이 null이면 null
- departureDt (string): 추천 출발 시각(`childArrivalDt`와 같은 형식). `pickup`이 null이면 null
- slackMin (integer): 남은 여유(분). 음수면 이미 늦은 것이다. `pickup`이 null이면 null
- enforcement (object): 픽업 장소의 단속 구역 판정. `pickup`이 null이면 null
  - status (string): 판정 결과. 아래 표
  - zoneTypes (array of strings): 걸린 구역 유형(중복 없음). `status`가 `RESTRICTED`가 아니면 빈 배열
- reasons (array of objects): 이 픽업 장소를 고른 근거. 해당하는 것이 없으면 빈 배열. 아래 표
  - code (string): 근거 코드
  - zoneTypes (array of strings): 코드에 따라 들어가는 값. 해당 없으면 null
  - pickupSpotSeq (integer): 해당 없으면 null
  - spotName (string): 해당 없으면 null
  - blockReason (string): 해당 없으면 null
  - blockStartTime (string): `HH:mm`. 해당 없으면 null
  - blockEndTime (string): `HH:mm`. 해당 없으면 null

값이 없는 필드도 키는 생략하지 않고 null로 나간다. `pickup.lat`·`lng`는 DB 값 그대로라 `37.4995000`처럼 끝자리 0이 붙을 수 있다(숫자 값은 같다).

**계산식**

```text
아이 도착 = 오늘 날짜 + 하원 시각 + 정리 시간 1분 + 도보 시간(walkMin)
추천 출발 = 아이 도착 − 차량 소요 시간(driveMin) − 여유 시간 1분
남은 여유 = (추천 출발 − currentDt)를 분 단위로 내림
```

- 정리 시간과 여유 시간은 둘 다 서버 설정값(지금 1분)이다.
- 예: 하원 22:00, 도보 3분, 차량 34분, 현재 21:14이면 아이 도착은 22:04, 추천 출발은 21:29, 남은 여유는 15분이다.

**walkSource**

| 값 | 뜻 |
|---|---|
| `STORED` | 기본 지점에 저장된 도보 시간이다 |
| `ROUTE` | 부모가 보낸 좌표로 카카오맵 도보 경로 API에서 구한 시간이다(큰길 우선 경로, 초를 분으로 올림) |
| `ESTIMATE` | 부모가 보낸 좌표인데 카카오맵 도보 경로를 얻지 못해 직선 거리를 아이 도보 속도(서버 설정값, 지금 시속 4km)로 나눠 올린 추정 시간이다 |

**driveSource**

| 값 | 뜻 |
|---|---|
| `NAVI` | 앱이 보낸 `driveMin`을 그대로 썼다 |
| `ESTIMATE` | `driveMin`이 없어 서버가 추정했다. 현재 위치 → 픽업 장소 직선 거리에 도로 보정 계수(서버 설정값, 지금 1.3)를 곱하고 평균 속도(서버 설정값, 지금 시속 20km)로 나눠 올린 분이다 |

**enforcement.status**

| 값 | 뜻 |
|---|---|
| `RESTRICTED` | 아이 도착 시각에 단속 구역에 걸린다. 걸린 유형이 `zoneTypes`에 들어간다 |
| `CLEAR` | 픽업 장소가 학원가 안이고, 그 학원가의 구역 데이터와 올해 공휴일 데이터가 있는데 걸리는 구역이 없다 |
| `UNKNOWN` | 판정할 수 없다. 픽업 장소가 학원가 밖이거나, 그 학원가에 구역 데이터가 없거나, 올해 공휴일 데이터가 없어 걸린 구역이 없다고 단정할 수 없을 때다. 앱은 `UNKNOWN`을 "단속 구역 아님"으로 보여주지 않는다 |

`zoneTypes` 값은 `SCHOOL_ZONE`(어린이보호구역), `NO_PARKING`(주정차 금지 구역), `INTERSECTION`(교차로 모퉁이), `CROSSWALK`(횡단보도), `BUS_STOP`(버스정류장), `FIRE_HYDRANT`(소화전), `YELLOW_DOUBLE`(황색 복선), `YELLOW_SINGLE`(황색 실선), `CCTV`(단속 카메라), `ACCIDENT_SPOT`(보행어린이 사고 다발지), `RESIDENT_PARKING`(거주자 우선 주차구역)이다. 정차하면 안 되는 곳으로 모두 같게 다룬다.

**reasons**

| code | 언제 | 들어가는 값 |
|---|---|---|
| `ACADEMY_FRONT_RESTRICTED` | 기본 지점을 골랐고, 학원 앞(학원 좌표)이 하원 시각 + 정리 시간에 단속 구역에 걸릴 때. 항상 맨 앞이다 | `zoneTypes` |
| `HIGHER_SPOT_BLOCKED` | 우선순위가 더 높은 기본 지점을 아이 도착 시각의 금지 시간 때문에 건너뛰었을 때. 건너뛴 지점마다 하나씩 우선순위 순으로 나온다 | `pickupSpotSeq`, `spotName`, `blockReason`(없을 수 있다), `blockStartTime`, `blockEndTime` |
| `NO_SPOT_AVAILABLE` | 쓸 수 있는 기본 지점이 하나도 없을 때(후보가 없거나 모두 금지 시간). `pickup`과 계산 필드가 모두 null이다 | 없음 |
| `CUSTOM_LOCATION` | 부모가 보낸 좌표로 계산했을 때. 이때는 앞의 세 코드가 나오지 않는다 | 없음 |

- 금지 시간은 `blockStartTime` 이상 `blockEndTime` 미만이다. 그 안에 아이 도착 시각이 들면 그 지점을 건너뛴다.
- 나중에 코드가 더해질 수 있으므로 앱은 모르는 `code`를 무시한다. 문구는 앱이 `code`와 값으로 만든다.

**\<Success>**

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": {
    "academyName": "수학학원",
    "endTime": "22:00",
    "pickup": {
      "pickupSpotSeq": 1,
      "spotName": "학원 정문 앞",
      "lat": 37.4995000,
      "lng": 127.0605000,
      "walkMin": 3,
      "walkSource": "STORED",
      "walkPath": [
        { "lat": 37.499, "lng": 127.06 },
        { "lat": 37.4993, "lng": 127.0603 },
        { "lat": 37.4995, "lng": 127.0605 }
      ]
    },
    "childArrivalDt": "2026-11-11T22:04:00",
    "driveMin": 34,
    "driveSource": "NAVI",
    "departureDt": "2026-11-11T21:29:00",
    "slackMin": 15,
    "enforcement": {
      "status": "CLEAR",
      "zoneTypes": []
    },
    "reasons": []
  },
  "code": 200
}
```

픽업 지점을 하나도 쓸 수 없을 때(`pickup`이 null)는 HTTP 200이고 data는 아래와 같다.

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": {
    "academyName": "수학학원",
    "endTime": "22:00",
    "pickup": null,
    "childArrivalDt": null,
    "driveMin": null,
    "driveSource": null,
    "departureDt": null,
    "slackMin": null,
    "enforcement": null,
    "reasons": [
      {
        "code": "HIGHER_SPOT_BLOCKED",
        "zoneTypes": null,
        "pickupSpotSeq": 1,
        "spotName": "학원 정문 앞",
        "blockReason": "황색 실선 안내판",
        "blockStartTime": "22:00",
        "blockEndTime": "22:10"
      },
      {
        "code": "NO_SPOT_AVAILABLE",
        "zoneTypes": null,
        "pickupSpotSeq": null,
        "spotName": null,
        "blockReason": null,
        "blockStartTime": null,
        "blockEndTime": null
      }
    ]
  },
  "code": 200
}
```

## Error

요청 검증 실패(400), 인증 실패(401), 서버 에러(500)는 [공통 오류 형식](../README.md#공통-오류-형식)이다. 오늘 남은 픽업이 없는 경우(101)는 오류가 아니라 정상 상태라서 HTTP 200이고 `message`도 정상 문구다. 앱은 `code`로 나눈다.

- message (string): `요청이 정상 처리되었습니다.`
- data (object): null
- code (integer): 101

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": null,
  "code": 101
}
```

## 예외

1. 오늘 남은 픽업 없음: 오늘 픽업하는 시간표가 없거나, 모두 하원한 지 1시간이 지났거나, 이미 마쳤거나, 오늘 건너뛰기로 했다. HTTP 200, code 101, `data`는 null
2. 쓸 수 있는 기본 지점 없음: HTTP 200, code 200. `pickup`·`childArrivalDt`·`driveMin`·`driveSource`·`departureDt`·`slackMin`·`enforcement`가 null이고 `reasons`에 `NO_SPOT_AVAILABLE`이 있다
3. 필수 값 없음·범위 밖 값·좌표 한쪽만 보냄(`pickupLat`와 `pickupLng` 중 하나만): 요청 검증 실패. HTTP 400 (공통 오류 형식)
4. 토큰 없음·위조·만료: HTTP 401, code 401 (공통 오류 형식)
5. 서버 에러: 500 Internal Server Error (공통 오류 형식)

- 첫 호출은 `driveMin` 없이 보낸다. 응답의 `pickup.lat`·`pickup.lng`로 NaviHelper에서 차량 소요 시간을 구하고, 다음 폴링(1분 간격)부터 `driveMin`에 넣어 보낸다. 그 전까지의 `driveMin`·`departureDt`·`slackMin`은 직선 거리 추정(`driveSource`가 `ESTIMATE`)이다.
- 부모가 보낸 좌표로 계산하는 호출은 매번 카카오맵 도보 경로 API를 부르고 결과를 저장하지 않는다. 호출마다 카카오의 일일 무료 한도를 쓰므로 폴링 간격을 1분보다 줄이지 않는다. 카카오 호출이 실패하거나 3초 안에 응답이 없으면 오류 없이 `walkSource`가 `ESTIMATE`인 결과가 나온다.
- 하원 시간대가 자정을 넘는 픽업은 지원하지 않는다. 하원 시각 + 1시간이 다음 날(예: 하원 23:30)이어도 00:00이 지나면 그 픽업은 오늘 남은 픽업으로 나오지 않는다(code 101). 아이 도착 시각이 자정을 넘으면 금지 시간·단속 시각 판정에는 자정 이후의 시각(예: 00:04)을 쓰면서 요일 유형(평일·토요일·공휴일)은 오늘 것을 쓴다.
- 그 해 공휴일 데이터가 없으면 서버는 공휴일을 평일처럼 다루고, 걸린 구역이 없을 때는 `enforcement.status`를 `CLEAR`가 아니라 `UNKNOWN`으로 낸다.
- `currentDt`는 오프셋 없는 KST 로컬 시각이어야 한다(예: `2026-11-11T21:14:00`). `Z`나 `+09:00`을 붙이면 서버가 그 시각을 그대로 읽는다고 보장하지 않는다.
- 응답의 시각(`childArrivalDt`, `departureDt`)도 오프셋이 없는 KST 로컬 시각이다.
