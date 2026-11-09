#!/bin/sh
# 이 레포의 git 훅을 설치한다. 클론한 뒤 한 번 실행한다.
# .git/hooks/pre-commit 을 backend/githooks/pre-commit 을 가리키는 심볼릭 링크로 만든다.
# core.hooksPath 는 바꾸지 않는다. 이미 다른 훅이 있으면 덮어쓰지 않고 멈춘다.

usage() {
  echo "사용법: sh backend/scripts/install-hooks.sh"
  echo "  인자는 없다. 레포 안 어디서 실행해도 된다."
}
case "${1-}" in
  -h|--help) usage; exit 0 ;;
esac
if [ $# -gt 0 ]; then
  usage >&2
  exit 64
fi

here="$(cd "$(dirname "$0")" && pwd)"
git_dir="$(git -C "$here" rev-parse --path-format=absolute --git-common-dir 2>/dev/null)" || {
  echo "[install-hooks] git 레포 안이 아니다." >&2
  exit 1
}
target="../../backend/githooks/pre-commit"
link="$git_dir/hooks/pre-commit"

mkdir -p "$git_dir/hooks"
if [ -L "$link" ] && [ "$(readlink "$link")" = "$target" ]; then
  echo "[install-hooks] 이미 설치돼 있다: .git/hooks/pre-commit -> $target"
  exit 0
fi
if [ -e "$link" ] || [ -L "$link" ]; then
  echo "[install-hooks] .git/hooks/pre-commit 이 이미 있고 이 레포의 훅을 가리키지 않는다. 내용을 확인해 옮기거나 지운 뒤 다시 실행한다." >&2
  exit 1
fi
ln -s "$target" "$link" || exit 1
if [ ! -x "$link" ]; then
  echo "[install-hooks] 링크는 만들었지만 대상을 실행할 수 없다: $link" >&2
  exit 1
fi
echo "[install-hooks] 설치: .git/hooks/pre-commit -> $target"
exit 0
