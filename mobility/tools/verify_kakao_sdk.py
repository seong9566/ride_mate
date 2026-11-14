#!/usr/bin/env python3
"""
카카오맵 Android SDK(AAR) 안의 클래스·메서드 시그니처를 확인한다(문서에 없는 API를 추측해 쓰지 않기 위함).
사용: python3 verify_kakao_sdk.py ~/.gradle/caches/modules-2/files-2.1/com.kakao.maps.open
"""
import glob
import io
import os
import struct
import sys
import zipfile


def parse(data):
    i = 10
    n = struct.unpack(">H", data[8:10])[0]
    cp = [None] * n
    k = 1
    while k < n:
        t = data[i]
        if t == 1:
            l = struct.unpack(">H", data[i + 1:i + 3])[0]
            cp[k] = data[i + 3:i + 3 + l].decode("utf8", "replace"); i += 3 + l
        elif t in (3, 4): i += 5
        elif t in (5, 6): i += 9; k += 1
        elif t in (7, 8, 16, 19, 20): cp[k] = ("ref", struct.unpack(">H", data[i + 1:i + 3])[0]); i += 3
        elif t in (9, 10, 11, 12, 17, 18): i += 5
        elif t == 15: i += 4
        else: raise ValueError(t)
        k += 1
    acc, this, sup = struct.unpack(">HHH", data[i:i + 6]); i += 6
    ic = struct.unpack(">H", data[i:i + 2])[0]; i += 2 + 2 * ic

    def members():
        nonlocal i
        c = struct.unpack(">H", data[i:i + 2])[0]; i += 2
        out = []
        for _ in range(c):
            a, ni, di, ac = struct.unpack(">HHHH", data[i:i + 8]); i += 8
            for _ in range(ac):
                l = struct.unpack(">I", data[i + 2:i + 6])[0]; i += 6 + l
            out.append((a, cp[ni], cp[di]))
        return out

    members()
    methods = members()
    return acc, methods


CHECKS = [
    ("com/kakao/vectormap/MapLifeCycleCallback", None, "interface?"),
    ("com/kakao/vectormap/KakaoMapReadyCallback", None, "interface?"),
    ("com/kakao/vectormap/MapView", "finish", None),
    ("com/kakao/vectormap/MapView", "start", None),
    ("com/kakao/vectormap/label/Label", "moveTo", None),
    ("com/kakao/vectormap/label/LabelLayer", "remove", None),
    ("com/kakao/vectormap/label/LabelLayer", "removeAll", None),
    ("com/kakao/vectormap/label/LabelOptions", "setTexts", None),
    ("com/kakao/vectormap/shape/ShapeLayer", "removeAll", None),
    ("com/kakao/vectormap/shape/PolygonOptions", "from", None),
    ("com/kakao/vectormap/route/RouteLineLayer", "removeAll", None),
    ("com/kakao/vectormap/route/RouteLineStyle", "from", None),
    ("com/kakao/vectormap/route/RouteLineStylesSet", "from", None),
    ("com/kakao/vectormap/route/RouteLineSegment", "from", None),
]


def main():
    root = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/.gradle/caches/modules-2/files-2.1/com.kakao.maps.open")
    aars = sorted(glob.glob(os.path.join(root, "**", "*.aar"), recursive=True))
    if not aars:
        print("AAR 없음:", root); sys.exit(1)
    aar = aars[-1]
    print("AAR:", aar)
    with zipfile.ZipFile(aar) as z:
        jar = zipfile.ZipFile(io.BytesIO(z.read("classes.jar")))
        names = set(jar.namelist())
        for cls, method, note in CHECKS:
            path = cls + ".class"
            if path not in names:
                print(f"[없음] {cls}")
                continue
            acc, methods = parse(jar.read(path))
            if method is None:
                kind = "interface" if acc & 0x200 else ("abstract class" if acc & 0x400 else "class")
                print(f"[OK] {cls} → {kind}")
                continue
            found = [(n, d) for a, n, d in methods if n == method and a & 1]
            print(("[OK] " if found else "[없음] ") + f"{cls}.{method}" + ("  " + " | ".join(d for _, d in found) if found else ""))


if __name__ == "__main__":
    main()
