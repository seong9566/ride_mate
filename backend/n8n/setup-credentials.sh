#!/bin/sh

set -eu

script_dir=$(dirname "$0")
cd "$script_dir/.."

if [ ! -f .env ]; then
  printf '%s\n' '오류: backend/.env가 없어 MySQL 자격 증명을 만들 수 없습니다.' >&2
  exit 1
fi

mysql_password=
while IFS= read -r line || [ -n "$line" ]; do
  case "$line" in
    MYSQL_PASSWORD=*) mysql_password=${line#MYSQL_PASSWORD=} ;;
  esac
done < .env

if [ -z "$mysql_password" ]; then
  printf '%s\n' '오류: backend/.env의 MYSQL_PASSWORD 값이 비어 있습니다.' >&2
  exit 1
fi

escaped_password=$(printf '%s' "$mysql_password" | sed 's/\\/\\\\/g; s/"/\\"/g')

printf '[{"id":"rmMysqlCred00001","name":"ridemate-mysql","type":"mySql","data":{"host":"mysql","port":3306,"database":"ridemate","user":"ridemate","password":"%s"}}]\n' "$escaped_password" |
  docker compose exec -T n8n sh -c 'cat > /tmp/cred.json && n8n import:credentials --input=/tmp/cred.json; rc=$?; rm -f /tmp/cred.json; exit $rc'
