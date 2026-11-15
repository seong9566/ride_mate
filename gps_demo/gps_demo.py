#!/usr/bin/env python3
"""
WAY:ME 시연용 GPS 시뮬레이터 (DEMO 전용 — 실제 차량 위치 데이터가 아님)

Android Emulator 콘솔 명령(adb emu geo fix / geo nmea)으로 에뮬레이터 GPS를 일정 간격으로 바꿔
차량이 경로를 따라 이동하는 것처럼 보이게 합니다. 표준 라이브러리만 사용합니다(Python 3.8+).

사용 예
  # 1) 검증: 한 점을 보내고 Android 위치 서비스가 받았는지 확인
  python3 gps_demo.py check --lat 37.4946 --lng 127.0635

  # 2) 도로를 따르는 경로 만들기(OSRM 공개 데모 서버, 인터넷 필요) + GPX 저장
  python3 gps_demo.py build daechi_scenario.json --osrm --out route_cache.json --gpx-dir gpx

  # 3) 시연 재생(구간마다 안내 문구 출력, 픽업 장소에서는 Enter 대기)
  python3 gps_demo.py play route_cache.json --interval 1 --speedup 2

  # 경로 없이 직선 보간으로 바로 재생(도로를 따르지 않음)
  python3 gps_demo.py play daechi_scenario.json

옵션
  --serial emulator-5554   여러 기기가 연결된 경우 대상 지정
  --mode fix|nmea          fix: geo fix(기본) / nmea: $GPGGA+$GPRMC로 속도·진행방향까지 전달
"""
import argparse
import json
import math
import os
import subprocess
import sys
import time
import urllib.request

EARTH_R = 6_371_000.0


# ---------------------------------------------------------------- geometry
def haversine(a, b):
    lat1, lng1, lat2, lng2 = map(math.radians, (a[0], a[1], b[0], b[1]))
    d = math.sin((lat2 - lat1) / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin((lng2 - lng1) / 2) ** 2
    return 2 * EARTH_R * math.asin(math.sqrt(d))


def bearing(a, b):
    lat1, lat2 = math.radians(a[0]), math.radians(b[0])
    dlng = math.radians(b[1] - a[1])
    x = math.sin(dlng) * math.cos(lat2)
    y = math.cos(lat1) * math.sin(lat2) - math.sin(lat1) * math.cos(lat2) * math.cos(dlng)
    return (math.degrees(math.atan2(x, y)) + 360) % 360


def resample(path, step_m):
    """경로를 step_m 간격 점으로 다시 나눈다(차량 이동을 일정 속도로 보이게)."""
    out = [path[0]]
    carry = 0.0
    for a, b in zip(path, path[1:]):
        seg = haversine(a, b)
        if seg == 0:
            continue
        d = step_m - carry
        while d <= seg:
            t = d / seg
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
            d += step_m
        carry = seg - (d - step_m)
    if out[-1] != path[-1]:
        out.append(path[-1])
    return out


# ---------------------------------------------------------------- adb
class Emulator:
    def __init__(self, serial=None, mode="fix", dry_run=False):
        self.base = ["adb"] + (["-s", serial] if serial else [])
        self.mode = mode
        self.dry = dry_run

    def emu(self, *args):
        cmd = self.base + ["emu"] + list(args)
        if self.dry:
            print("  [dry-run]", " ".join(cmd))
            return ""
        r = subprocess.run(cmd, capture_output=True, text=True)
        if r.returncode != 0 or "KO" in r.stdout:
            print("  ! adb emu 실패:", (r.stdout + r.stderr).strip(), file=sys.stderr)
        return r.stdout

    def send(self, lat, lng, speed_mps=0.0, course=0.0):
        if self.mode == "nmea":
            for s in nmea_sentences(lat, lng, speed_mps, course):
                self.emu("geo", "nmea", s)
        else:
            # 공식 문서 순서: geo fix <longitude> <latitude> [altitude] [satellites] [velocity(knots)]
            self.emu("geo", "fix", f"{lng:.6f}", f"{lat:.6f}", "30", "8", f"{speed_mps * 1.943844:.1f}")

    def shell(self, *args):
        r = subprocess.run(self.base + ["shell"] + list(args), capture_output=True, text=True)
        return r.stdout


def _nmea(body):
    cs = 0
    for ch in body:
        cs ^= ord(ch)
    return f"${body}*{cs:02X}"


def _dm(value, is_lat):
    v = abs(value)
    deg = int(v)
    minutes = (v - deg) * 60
    hemi = ("N" if value >= 0 else "S") if is_lat else ("E" if value >= 0 else "W")
    return (f"{deg:02d}{minutes:07.4f}" if is_lat else f"{deg:03d}{minutes:07.4f}"), hemi


def nmea_sentences(lat, lng, speed_mps, course):
    t = time.gmtime()
    hms = time.strftime("%H%M%S.00", t)
    dmy = time.strftime("%d%m%y", t)
    la, ns = _dm(lat, True)
    lo, ew = _dm(lng, False)
    gga = _nmea(f"GPGGA,{hms},{la},{ns},{lo},{ew},1,08,0.9,30.0,M,0.0,M,,")
    rmc = _nmea(f"GPRMC,{hms},A,{la},{ns},{lo},{ew},{speed_mps * 1.943844:.1f},{course:.1f},{dmy},,,A")
    return [gga, rmc]


# ---------------------------------------------------------------- scenario
def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def osrm_route(points):
    """OSRM 공개 데모 서버로 도로를 따르는 경로 좌표를 받는다(시연 준비용, 상용 사용 금지 · © OpenStreetMap contributors)."""
    coords = ";".join(f"{p['lng']},{p['lat']}" for p in points)
    url = f"https://router.project-osrm.org/route/v1/driving/{coords}?overview=full&geometries=geojson"
    with urllib.request.urlopen(url, timeout=20) as r:
        data = json.load(r)
    if data.get("code") != "Ok":
        raise RuntimeError(f"OSRM 오류: {data.get('code')}")
    route = data["routes"][0]
    return [(c[1], c[0]) for c in route["geometry"]["coordinates"]], route["distance"], route["duration"]


def write_gpx(path, name, coords):
    with open(path, "w", encoding="utf-8") as f:
        f.write('<?xml version="1.0" encoding="UTF-8"?>\n')
        f.write('<gpx version="1.1" creator="WAY:ME gps_demo (DEMO)" xmlns="http://www.topografix.com/GPX/1/1">\n')
        f.write(f"  <trk><name>{name}</name><trkseg>\n")
        for lat, lng in coords:
            f.write(f'    <trkpt lat="{lat:.6f}" lon="{lng:.6f}"></trkpt>\n')
        f.write("  </trkseg></trk>\n</gpx>\n")


def cmd_build(args):
    sc = load(args.scenario)
    if args.gpx_dir:
        os.makedirs(args.gpx_dir, exist_ok=True)
    for leg in sc["legs"]:
        if args.osrm:
            path, dist, dur = osrm_route(leg["points"])
            leg["path"] = [[a, b] for a, b in path]
            print(f"- {leg['label']}: 도로 경로 {len(path)}점, {dist / 1000:.2f}km, OSRM 예상 {dur / 60:.1f}분")
        else:
            leg["path"] = [[p["lat"], p["lng"]] for p in leg["points"]]
            print(f"- {leg['label']}: 직선 보간(도로를 따르지 않음)")
        if args.gpx_dir:
            g = os.path.join(args.gpx_dir, f"{leg['id']}.gpx")
            write_gpx(g, leg["label"], [tuple(x) for x in leg["path"]])
            print(f"  GPX 저장: {g}  (Extended Controls > Location > Load GPX/KML 로도 재생 가능)")
    sc["_comment"] = sc.get("_comment", "") + " | path 포함(" + ("OSRM" if args.osrm else "직선") + ")"
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(sc, f, ensure_ascii=False, indent=2)
    print(f"저장: {args.out}")


def cmd_play(args):
    sc = load(args.scenario)
    emu = Emulator(args.serial, args.mode, args.dry_run)
    print(f"▶ {sc.get('name', '시연')}  [DEMO 위치 시뮬레이션]  interval={args.interval}s speedup=x{args.speedup}")
    legs = sc["legs"]
    if args.leg:
        legs = [l for l in legs if l["id"] == args.leg]
    for leg in legs:
        path = [tuple(x) for x in leg.get("path") or [[p["lat"], p["lng"]] for p in leg["points"]]]
        speed = leg.get("speed_kmh", 30) / 3.6
        step = speed * args.interval * args.speedup
        pts = resample(path, max(step, 1.0))
        total = sum(haversine(a, b) for a, b in zip(path, path[1:]))
        print(f"\n■ {leg['label']}  {total / 1000:.2f}km · {leg.get('speed_kmh', 30)}km/h · {len(pts)}스텝")
        done = 0.0
        for i, p in enumerate(pts):
            course = bearing(p, pts[i + 1]) if i + 1 < len(pts) else (bearing(pts[i - 1], p) if i else 0.0)
            v = speed if i + 1 < len(pts) else 0.0
            emu.send(p[0], p[1], v, course)
            if i:
                done += haversine(pts[i - 1], p)
            remain = max(total - done, 0)
            eta_min = remain / speed / 60 if speed else 0
            sys.stdout.write(f"\r  {p[0]:.5f},{p[1]:.5f}  남은 {remain:6.0f}m  약 {eta_min:4.1f}분   ")
            sys.stdout.flush()
            time.sleep(args.interval)
        arrive = leg.get("arrive", {})
        print(f"\n  ✔ 도착: {leg['points'][-1]['name']}")
        if arrive.get("prompt"):
            print(f"  → {arrive['prompt']}")
        if arrive.get("wait_enter") and not args.auto:
            input("  (준비되면 Enter) ")
        elif arrive.get("dwell_sec"):
            # 정차 중에도 같은 위치를 주기적으로 보내 위치가 끊기지 않게 한다
            for _ in range(int(arrive["dwell_sec"] / max(args.interval, 0.5))):
                emu.send(path[-1][0], path[-1][1], 0.0, 0.0)
                time.sleep(args.interval)
    print("\n■ 시연 경로 종료")


def cmd_check(args):
    emu = Emulator(args.serial, args.mode, False)
    print("1) 에뮬레이터로 위치 전송 (geo fix 순서: 경도 위도)")
    emu.send(args.lat, args.lng)
    time.sleep(2)
    print("2) Android 위치 서비스가 받은 마지막 위치:")
    out = emu.shell("dumpsys", "location")
    lines = [l.strip() for l in out.splitlines() if "last location" in l.lower() or "gps" in l.lower() and "Location[" in l]
    print("\n".join(lines[:10]) or "  (dumpsys에서 위치를 찾지 못함 — 출력 전체를 확인하세요: adb shell dumpsys location)")
    print("3) 이제 앱 SDK 검증 화면(FusedLocation 칸)의 위도·경도가 같은 값으로 바뀌는지 눈으로 확인하세요.")
    print("   adb shell am start -n com.example.pickup_hyundai/.MainActivity --ez sdk_check true")


def main():
    ap = argparse.ArgumentParser(description="WAY:ME 시연용 GPS 시뮬레이터(DEMO)")
    ap.add_argument("--serial")
    ap.add_argument("--mode", choices=["fix", "nmea"], default="fix")
    sub = ap.add_subparsers(dest="cmd", required=True)

    c = sub.add_parser("check")
    c.add_argument("--lat", type=float, default=37.4946)
    c.add_argument("--lng", type=float, default=127.0635)
    c.set_defaults(func=cmd_check)

    b = sub.add_parser("build")
    b.add_argument("scenario")
    b.add_argument("--osrm", action="store_true")
    b.add_argument("--out", default="route_cache.json")
    b.add_argument("--gpx-dir")
    b.set_defaults(func=cmd_build)

    p = sub.add_parser("play")
    p.add_argument("scenario")
    p.add_argument("--interval", type=float, default=1.0, help="위치 전송 간격(초)")
    p.add_argument("--speedup", type=float, default=1.0, help="시연 배속(영상 길이 단축용)")
    p.add_argument("--leg", help="특정 구간만 재생(to_waiting / to_pickup / to_home)")
    p.add_argument("--auto", action="store_true", help="Enter 대기 없이 진행")
    p.add_argument("--dry-run", action="store_true", help="adb 없이 명령만 출력")
    p.set_defaults(func=cmd_play)

    args = ap.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
