---
name: update-api-spec
description: RideMate 백엔드의 라우트·DTO를 바꾼 뒤 앱↔서버 API 명세(루트 docs/api/openapi.json)를 서버의 Swagger 출력으로 갱신할 때 쓴다. "API 명세 갱신", "openapi 갱신", "swagger 내보내기"에 쓴다.
---

# update-api-spec

명령은 `backend/RideMateService/`에서 실행한다. 결과 파일은 레포 루트의 `docs/api/openapi.json` 하나다.

## 하지 말 것

- `docs/api/openapi.json`을 손으로 고치지 않는다. 항상 서버 출력으로 덮어쓴다.
- 5167 포트를 다른 프로세스가 쓰고 있으면 그 프로세스를 종료하지 않는다. 멈추고 사용자에게 알린다.
- 띄운 서버를 `pkill`처럼 이름으로 종료하지 않는다. 기록한 PID로만 종료한다.

## 절차

1. `dotnet build RideMateService.sln -warnaserror`로 빌드한다.
2. `lsof -ti :5167`로 포트가 비어 있는지 본다.
3. 서버를 띄우고, 명세를 저장하고, 서버를 종료한다. PID가 이어지도록 한 번의 셸 호출로 실행한다. 기동은 1초 간격으로 최대 30번 기다린다.

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet RideMateService/bin/Debug/net10.0/RideMateService.dll --urls http://127.0.0.1:5167 &
PID=$!
for i in $(seq 1 30); do
  curl -sf http://127.0.0.1:5167/swagger/v1/swagger.json -o ../../docs/api/openapi.json && break
  sleep 1
done
kill $PID
```

4. 30번 안에 저장하지 못했으면 멈추고 사용자에게 알린다. 서버는 위 명령의 `kill`로 이미 종료돼 있다.
5. `docs/api/.gitkeep`이 남아 있으면 지운다.
6. 변경한 코드와 같은 커밋 또는 같은 PR에 명세를 넣는다.

## 완료 조건

- [ ] `python3 -m json.tool ../../docs/api/openapi.json > /dev/null`이 성공한다.
- [ ] `git diff --stat -- ../../docs/api/`에 의도한 라우트·DTO 변경만 있다.
- [ ] `lsof -ti :5167`의 출력이 없다(띄운 서버가 종료됐다).
- [ ] 명세가 코드 변경과 같은 커밋 또는 같은 PR에 들어 있다.
