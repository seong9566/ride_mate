#!/bin/sh
# backend/githooks/pre-commit 테스트.
# 임시 폴더에 레포를 복제해 그 안에서만 훅을 실행한다. 실제 레포·원격·실제 로그는 건드리지 않는다.
# 실행(어디서든): sh backend/githooks/tests/test-pre-commit.sh
# 빌드하는 경우가 2개 있어 30초쯤 걸린다. NuGet 패키지가 캐시에 없으면 복원에 네트워크가 필요하다.

if [ $# -gt 0 ]; then
  echo "사용법: sh backend/githooks/tests/test-pre-commit.sh (인자 없음)" >&2
  exit 64
fi

here="$(cd "$(dirname "$0")" && pwd)"
hook="$here/../pre-commit"
src="$(git -C "$here" rev-parse --show-toplevel)" || exit 1

tmp="$(mktemp -d)" || exit 1
trap 'rm -rf "$tmp"' EXIT
HARNESS_HOOK_LOG="$tmp/log/hooks.log"; export HARNESS_HOOK_LOG
mkdir -p "$tmp/log"

repo="$tmp/repo"
git clone --quiet --no-hardlinks "$src" "$repo" || exit 1
cp "$hook" "$repo/.git/hooks/pre-commit"   # 검사 대상은 작업 트리의 훅이다
cd "$repo" || exit 1

fail=0
# check <이름> <기대 종료 코드> <출력에 있어야 할 문구(없으면 "")> <로그의 기대 결정>
check() {
  name=$1; want_rc=$2; want_text=$3; want_log=$4
  : > "$HARNESS_HOOK_LOG"
  out="$(sh .git/hooks/pre-commit 2>&1)"; rc=$?
  ok=1
  [ "$rc" = "$want_rc" ] || ok=0
  if [ -n "$want_text" ]; then printf '%s' "$out" | grep -q "$want_text" || ok=0; fi
  grep -q "repo-pre-commit	$want_log	" "$HARNESS_HOOK_LOG" || ok=0
  if [ "$ok" = 1 ]; then
    echo "통과: $name"
  else
    echo "실패: $name (종료 코드 $rc, 기대 $want_rc)"; printf '%s\n' "$out" | tail -5
    echo "  로그: $(cat "$HARNESS_HOOK_LOG")"
    fail=$((fail + 1))
  fi
}
reset() { git reset --quiet --hard HEAD && git clean --quiet -fd; }

program=backend/RideMateService/RideMateService/Program.cs

# 0. 인자를 주면 사용법을 내고 64로 끝난다
out="$(sh .git/hooks/pre-commit --bogus 2>&1)"; rc=$?
if [ "$rc" = 64 ] && printf '%s' "$out" | grep -q "사용법"; then
  echo "통과: 인자를 주면 사용법 출력"
else
  echo "실패: 인자를 주면 사용법 출력 (종료 코드 $rc)"; fail=$((fail + 1))
fi

# 1. backend 밖 변경만 있으면 빌드 없이 통과한다 (허용)
git checkout --quiet -B main
echo "x" >> README.md && git add README.md
check "backend 밖 변경은 main에서도 통과" 0 "" allow
reset

# 2. main에서 backend 변경은 막는다 (차단)
echo "// test" >> "$program" && git add "$program"
check "main에서 backend 변경 차단" 1 "main 브랜치에는 backend/ 변경을 커밋하지 않는다" block
reset

# 2-1. 한글 이름 파일만 바꿔도 backend 변경으로 본다 (차단)
echo "x" > "backend/docs/한글-파일.md" && git add "backend/docs/한글-파일.md"
check "main에서 한글 이름 backend 파일 차단" 1 "main 브랜치에는 backend/ 변경을 커밋하지 않는다" block
reset

# 2-2. 배포 브랜치 backend에서도 backend 변경은 막는다 (차단)
git checkout --quiet -B backend
echo "// test" >> "$program" && git add "$program"
check "backend 브랜치에서 backend 변경 차단" 1 "backend 브랜치에는 backend/ 변경을 커밋하지 않는다" block
reset

# 2-3. 통합 브랜치 backend-dev에서도 backend 변경은 막는다 (차단)
git checkout --quiet -B backend-dev
echo "// test" >> "$program" && git add "$program"
check "backend-dev 브랜치에서 backend 변경 차단" 1 "backend-dev 브랜치에는 backend/ 변경을 커밋하지 않는다" block
reset

# 3. dotnet이 없으면 막는다 (차단)
git checkout --quiet -B test/hook
echo "// test" >> "$program" && git add "$program"
real_path=$PATH
PATH="$(dirname "$(command -v git)"):/usr/bin:/bin"
if command -v dotnet >/dev/null 2>&1; then
  echo "건너뜀: dotnet 없음 차단 (기본 경로에 dotnet이 있어 재현할 수 없다)"
else
  check "dotnet이 없으면 차단" 1 "dotnet을 찾을 수 없다" block
fi
PATH=$real_path

# 4. 브랜치에서 경고 없는 backend 변경은 통과한다 (허용)
check "브랜치에서 깨끗한 변경 통과" 0 "빌드 통과" allow
reset

# 5. 경고가 있으면 막는다 (차단)
cat > backend/RideMateService/RideMateService/WarnProbe.cs <<'CS'
namespace RideMateService;

public class WarnProbe
{
    public void Run()
    {
        int unused = 1;
    }
}
CS
git add backend/RideMateService/RideMateService/WarnProbe.cs
check "경고가 있으면 차단" 1 "빌드 실패" block
reset

if [ "$fail" = 0 ]; then
  echo "실패: 0"
  exit 0
fi
echo "실패: $fail"
exit 1
