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

## API 키

`.env`의 `TOPIS_API_KEY`와 `GITS_API_KEY`에 각 제공처에서 발급받은 키를 넣는다. 빈 문자열이면 해당 소스의 키가 없는 것으로 처리한다. 키를 바꾼 뒤에는 `backend/`에서 컨테이너를 다시 생성해 환경변수를 반영한다.

```sh
docker compose up -d n8n
```

워크플로는 키를 각각 `$env.TOPIS_API_KEY`, `$env.GITS_API_KEY`로 읽는다. 키는 워크플로 JSON, 명령줄 인자, 로그, 실행 이력이나 `ERROR_MESSAGE`에 넣지 않는다.

## 출처와 이용 조건

- 서울 TOPIS 데이터는 공공누리 제1유형으로, 출처를 표시하면 상업적 이용과 변경이 가능하다.
- 경기도 GITS 데이터의 이용 조건은 API 키를 발급받을 때 확인한다.
