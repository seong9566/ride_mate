#!/usr/bin/env python3

import argparse
import json
import re
import shutil
import sys
import tempfile
import time
import urllib.parse
import urllib.request
from datetime import date, datetime
from decimal import Decimal, InvalidOperation
from pathlib import Path

from openpyxl import load_workbook


LIST_URL = "https://topis.seoul.go.kr/refroom/selectRefRoomListASC.do"
DOWNLOAD_URL = "https://topis.seoul.go.kr/downloadFileRefRoom.do"
USER_AGENT = "Mozilla/5.0 TOPIS monthly speed loader"
BATCH_SIZE = 1000
MONTH_PATTERN = re.compile(r"(\d{4})년\s*(\d{1,2})월")
HOUR_PATTERN = re.compile(r"~(\d{2})시")


def post(url, fields):
    data = urllib.parse.urlencode(fields).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=data,
        headers={
            "Content-Type": "application/x-www-form-urlencoded",
            "User-Agent": USER_AGENT,
        },
        method="POST",
    )
    return urllib.request.urlopen(request, timeout=120)


def fetch_files():
    files = {}
    current_year = date.today().year
    for year in (current_year, current_year - 1):
        with post(
            LIST_URL,
            {
                "blbdDivCd": "06",
                "bdwrDivCd": str(year),
                "mainBdwrRowNum": "12",
            },
        ) as response:
            payload = json.loads(response.read().decode("utf-8-sig"))

        for row in payload.get("rows", []):
            if not row.get("bdwrTtlNm"):
                continue
            filename = row.get("apndFileNm") or ""
            match = MONTH_PATTERN.search(filename)
            if not match:
                continue
            month_number = int(match.group(2))
            if not 1 <= month_number <= 12:
                continue
            month = f"{int(match.group(1)):04d}-{month_number:02d}"
            files[month] = row
    return files


def parse_months(value, available):
    if value:
        months = []
        for raw_month in value.split(","):
            month = raw_month.strip()
            if not re.fullmatch(r"\d{4}-\d{2}", month):
                raise ValueError(f"잘못된 --months 값: {month}")
            try:
                datetime.strptime(month, "%Y-%m")
            except ValueError as error:
                raise ValueError(f"잘못된 --months 값: {month}") from error
            if month not in months:
                months.append(month)
        missing = [month for month in months if month not in available]
        if missing:
            raise ValueError(f"TOPIS 목록에 파일이 없는 달: {', '.join(missing)}")
        return months

    return sorted(available, reverse=True)[:3][::-1]


def download_file(month, metadata, destination):
    fields = {
        "apndFileNm": metadata["apndFileNm"],
        "apndFilePathNm": metadata["apndFilePathNm"],
        "bdwrSeq": metadata["bdwrSeq"],
        "blbdDivCd": metadata["blbdDivCd"],
    }
    partial = destination.with_suffix(destination.suffix + ".part")
    for attempt in range(1, 4):
        try:
            with post(DOWNLOAD_URL, fields) as response, partial.open("wb") as output:
                shutil.copyfileobj(response, output, length=1024 * 1024)
            partial.replace(destination)
            return
        except Exception as error:
            partial.unlink(missing_ok=True)
            if attempt == 3:
                raise
            print(
                f"{month}: 다운로드 실패 ({attempt}/3): {error}; 5초 뒤 재시도",
                file=sys.stderr,
            )
            time.sleep(5)


def normalize_link_id(value):
    if value is None or isinstance(value, bool):
        return None
    if isinstance(value, float) and value.is_integer():
        return str(int(value))
    return str(value).strip()


def normalize_date(value):
    if value is None or isinstance(value, bool):
        return None
    if isinstance(value, float):
        if not value.is_integer():
            return None
        raw = str(int(value))
    else:
        raw = str(value).strip()
    if not re.fullmatch(r"\d{8}", raw):
        return None
    try:
        return datetime.strptime(raw, "%Y%m%d").strftime("%Y-%m-%d")
    except ValueError:
        return None


def normalize_speed(value):
    if value is None or isinstance(value, bool):
        return None
    try:
        speed = Decimal(str(value).strip())
    except (InvalidOperation, ValueError):
        return None
    if not speed.is_finite() or speed <= 0:
        return None
    return format(speed, "f")


def quote(value):
    return "'" + value.replace("'", "''") + "'"


def write_batch(values):
    rows = ", ".join(
        f"ROW({quote(link_id)},{quote(speed_date)},{hour},{speed})"
        for link_id, speed_date, hour, speed in values
    )
    print(
        "INSERT INTO TRAFFIC_HOURLY_SPEED "
        "(TRAFFIC_LINK_ID, SPEED_DATE, HOUR_NO, SPEED_KMH, CREATE_DT, "
        "CREATE_USER, UPDATE_DT, UPDATE_USER, DELETE_YN) "
        "SELECT l.TRAFFIC_LINK_ID, v.d, v.h, v.s, NOW(), 'SYSTEM', NOW(), "
        "'SYSTEM', 0 FROM (VALUES "
        f"{rows}"
        ") AS v(link_id, d, h, s) JOIN TRAFFIC_LINK l ON l.LINK_ID = v.link_id "
        "AND l.SOURCE = 'TOPIS' AND l.AREA_CODE = 'DAECHI' AND l.DELETE_YN = 0 "
        "ON DUPLICATE KEY UPDATE SPEED_KMH = VALUES(SPEED_KMH), "
        "UPDATE_DT = NOW(), UPDATE_USER = 'SYSTEM', DELETE_YN = 0;"
    )


def load_links(path):
    with path.open(encoding="utf-8-sig") as source:
        return {line.strip() for line in source if line.strip()}


def emit_month(month, path, links):
    workbook = load_workbook(path, read_only=True, data_only=True)
    try:
        sheet = workbook.worksheets[0]
        rows = sheet.iter_rows(values_only=True)
        header = next(rows, None)
        if header is None:
            raise ValueError(f"{month}: XLSX 첫 시트가 비어 있음")

        columns = {
            str(name).strip(): index
            for index, name in enumerate(header)
            if name is not None
        }
        for required in ("일자", "링크아이디"):
            if required not in columns:
                raise ValueError(f"{month}: 필수 헤더가 없음: {required}")
        hour_columns = []
        for name, index in columns.items():
            match = HOUR_PATTERN.fullmatch(name)
            if match:
                hour = int(match.group(1))
                if 1 <= hour <= 24:
                    hour_columns.append((hour, index))
        if not hour_columns:
            raise ValueError(f"{month}: 시간 헤더가 없음")
        hour_columns.sort()

        batch = []
        source_rows = 0
        matched_rows = 0
        speed_rows = 0
        for row in rows:
            source_rows += 1
            link_id = normalize_link_id(row[columns["링크아이디"]])
            if not link_id or link_id not in links:
                continue
            speed_date = normalize_date(row[columns["일자"]])
            if not speed_date:
                continue
            matched_rows += 1
            for hour, index in hour_columns:
                speed = normalize_speed(row[index])
                if speed is None:
                    continue
                batch.append((link_id, speed_date, hour, speed))
                speed_rows += 1
                if len(batch) == BATCH_SIZE:
                    write_batch(batch)
                    batch.clear()
        if batch:
            write_batch(batch)
        print(
            f"{month}: 원본 {source_rows}행, 대상 {matched_rows}행, 속도 {speed_rows}행",
            file=sys.stderr,
        )
    finally:
        workbook.close()


def run(args):
    links = load_links(args.links)
    if not links:
        raise ValueError("--links 파일에 링크 ID가 없음")

    available = fetch_files()
    months = parse_months(args.months, available)
    if not months:
        raise ValueError("TOPIS 목록에서 월별 파일을 찾지 못함")

    if args.download_dir:
        download_dir = args.download_dir
        download_dir.mkdir(parents=True, exist_ok=True)
        temporary_directory = None
    else:
        temporary_directory = tempfile.TemporaryDirectory(prefix="topis-monthly-")
        download_dir = Path(temporary_directory.name)

    try:
        downloads = []
        for index, month in enumerate(months):
            if index:
                time.sleep(3)
            destination = download_dir / f"topis-{month}.xlsx"
            print(f"{month}: 다운로드 시작", file=sys.stderr)
            download_file(month, available[month], destination)
            print(f"{month}: 다운로드 완료", file=sys.stderr)
            downloads.append((month, destination))

        print("SET NAMES utf8mb4;")
        for month, path in downloads:
            emit_month(month, path, links)
    finally:
        if temporary_directory is not None:
            temporary_directory.cleanup()


def parse_args():
    parser = argparse.ArgumentParser(description="TOPIS 월별 통행속도 SQL 생성")
    parser.add_argument("--links", type=Path, required=True)
    parser.add_argument("--months", help="쉼표로 구분한 YYYY-MM 목록")
    parser.add_argument("--download-dir", type=Path)
    return parser.parse_args()


def main():
    try:
        run(parse_args())
    except Exception as error:
        print(f"오류: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
