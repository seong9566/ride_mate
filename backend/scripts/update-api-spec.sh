#!/bin/sh
# 서버의 Swagger 출력으로 앱↔서버 API 명세(레포 루트 docs/api/openapi.json)를 갱신한다.
# 빌드 → 서버 기동 → 명세 저장 → 서버 종료까지 한 번에 한다. 띄운 서버는 성공·실패와 관계없이 종료한다.
# 명세는 임시 파일에 받아 JSON이 온전할 때만 바꿔 넣는다.

usage() {
  echo "사용법: sh backend/scripts/update-api-spec.sh"
  echo "  인자는 없다. 임시 서버를 5168 포트에 띄운다(개발 서버 5167과 겹치지 않는다)."
  echo "  다른 포트를 쓰려면 UPDATE_API_SPEC_PORT=<포트> 를 앞에 붙인다."
}
case "${1-}" in
  -h|--help) usage; exit 0 ;;
esac
if [ $# -gt 0 ]; then
  usage >&2
  exit 64
fi

PORT="${UPDATE_API_SPEC_PORT:-5168}"   # 개발 서버(5167)가 떠 있어도 돌도록 전용 포트를 쓴다
WAIT_SECONDS=30   # 기동 대기 상한

here="$(cd "$(dirname "$0")" && pwd)"
sln_dir="$here/../RideMateService"
spec="$here/../../docs/api/openapi.json"

cd "$sln_dir" || { echo "[update-api-spec] 솔루션 폴더를 찾을 수 없다: $sln_dir" >&2; exit 1; }

if lsof -ti ":$PORT" >/dev/null 2>&1; then
  echo "[update-api-spec] $PORT 포트를 다른 프로세스가 쓰고 있다. 그 프로세스를 직접 확인한 뒤 다시 실행한다(이 스크립트는 종료하지 않는다)." >&2
  exit 1
fi

echo "[update-api-spec] 빌드 중…"
if ! out="$(dotnet build RideMateService.sln -warnaserror -nologo -v q 2>&1)"; then
  echo "$out" | grep -E "error|warning" >&2
  echo "[update-api-spec] 빌드 실패. 고친 뒤 다시 실행한다." >&2
  exit 1
fi

tmp="$(mktemp)" || exit 1
pid=""
cleanup() {
  [ -n "$pid" ] && kill "$pid" 2>/dev/null
  rm -f "$tmp" "$tmp.log"
}
trap cleanup EXIT

ASPNETCORE_ENVIRONMENT=Development dotnet RideMateService/bin/Debug/net10.0/RideMateService.dll --urls "http://127.0.0.1:$PORT" > "$tmp.log" 2>&1 &
pid=$!

saved=0
i=0
while [ "$i" -lt "$WAIT_SECONDS" ]; do
  if curl -sf "http://127.0.0.1:$PORT/swagger/v1/swagger.json" -o "$tmp"; then
    saved=1
    break
  fi
  if ! kill -0 "$pid" 2>/dev/null; then
    break   # 서버가 기동 중에 죽었다
  fi
  i=$((i + 1))
  sleep 1
done

if [ "$saved" != 1 ]; then
  echo "[update-api-spec] ${WAIT_SECONDS}초 안에 명세를 받지 못했다. 서버 로그 마지막 줄:" >&2
  tail -5 "$tmp.log" >&2
  exit 1
fi

if ! python3 -m json.tool "$tmp" > /dev/null 2>&1; then
  echo "[update-api-spec] 받은 명세가 온전한 JSON이 아니다. 기존 명세는 그대로 둔다." >&2
  exit 1
fi

mkdir -p "$(dirname "$spec")"
cp "$tmp" "$spec"
rm -f "$(dirname "$spec")/.gitkeep"
echo "[update-api-spec] 저장: docs/api/openapi.json"
git -C "$(dirname "$spec")" diff --stat -- . 2>/dev/null
exit 0
