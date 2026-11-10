#!/usr/bin/env python3
"""기능 명세서 xlsx를 시트별 Markdown으로 바꿔 backend/docs/spec/에 쓴다.

정본은 xlsx다. 생성된 .md는 손으로 고치지 않고, 명세가 바뀌면 이 스크립트를 다시 돌린다.
표준 라이브러리만 쓴다(openpyxl 불필요).

사용법: python3 backend/scripts/spec-to-md.py <명세서.xlsx>
  xlsx를 backend/docs/spec/feature-spec.xlsx로 복사하고, 시트마다 <번호>-<시트 이름>.md를 만든다.
"""
import os
import re
import shutil
import sys
import zipfile
import xml.etree.ElementTree as ET

NS = {
    "m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
    "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
    "rel": "http://schemas.openxmlformats.org/package/2006/relationships",
}
SPEC_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "docs", "spec")
XLSX_NAME = "feature-spec.xlsx"


def text_of(node):
    """<si>·<is> 아래 모든 <t>를 이어 붙인다(서식이 나뉜 rich text 포함)."""
    return "".join(t.text or "" for t in node.iter(f"{{{NS['m']}}}t"))


def col_index(ref):
    letters = re.match(r"[A-Z]+", ref).group(0)
    n = 0
    for ch in letters:
        n = n * 26 + (ord(ch) - 64)
    return n - 1


def read_sheets(path):
    with zipfile.ZipFile(path) as z:
        shared = []
        if "xl/sharedStrings.xml" in z.namelist():
            root = ET.fromstring(z.read("xl/sharedStrings.xml"))
            shared = [text_of(si) for si in root.findall("m:si", NS)]
        rels = ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
        targets = {r.get("Id"): r.get("Target") for r in rels.findall("rel:Relationship", NS)}
        wb = ET.fromstring(z.read("xl/workbook.xml"))
        for sheet in wb.find("m:sheets", NS):
            name = sheet.get("name")
            target = targets[sheet.get(f"{{{NS['r']}}}id")].lstrip("/")
            if not target.startswith("xl/"):
                target = "xl/" + target
            root = ET.fromstring(z.read(target))
            rows = []
            for row in root.iter(f"{{{NS['m']}}}row"):
                cells = {}
                for c in row.findall("m:c", NS):
                    t = c.get("t")
                    if t == "inlineStr":
                        val = text_of(c.find("m:is", NS))
                    else:
                        v = c.find("m:v", NS)
                        if v is None:
                            continue
                        val = shared[int(v.text)] if t == "s" else v.text
                    if val and val.strip():
                        cells[col_index(c.get("r"))] = val.strip()
                if cells:
                    rows.append((int(row.get("r")), cells))
            yield name, rows


def cell_md(s):
    return s.replace("|", "\\|").replace("\r\n", "\n").replace("\n", "<br>")


def to_markdown(name, rows, source):
    out = [f"# {name}", "", f"> 생성 파일 — 손으로 고치지 않는다. 정본: `{source}`", ""]
    if not rows:
        out.append("(빈 시트)")
        return "\n".join(out) + "\n"
    width = max(max(c) for _, c in rows) + 1
    # 빈 행에서 표를 나눈다. 셀이 하나뿐인 행은 제목·구분 문구로 보고 표 밖에 문단으로 쓴다.
    table, prev = [], None
    for num, r in rows:
        if prev is not None and num != prev + 1:
            out += table_md(table, width)
            table = []
        prev = num
        if len(r) == 1:
            out += table_md(table, width)
            table = []
            out += [cell_md(next(iter(r.values()))), ""]
            continue
        table.append([cell_md(r.get(i, "")) for i in range(width)])
    out += table_md(table, width)
    return "\n".join(out).rstrip() + "\n"


def table_md(table, width):
    if not table:
        return []
    used = [i for i in range(width) if any(row[i] for row in table)]
    lines = ["| " + " | ".join(table[0][i] for i in used) + " |",
             "|" + "---|" * len(used)]
    lines += ["| " + " | ".join(row[i] for i in used) + " |" for row in table[1:]]
    return lines + [""]


def main():
    if len(sys.argv) != 2 or sys.argv[1] in ("-h", "--help"):
        print(__doc__.strip().split("\n\n")[-1], file=sys.stderr)
        sys.exit(64)
    src = sys.argv[1]
    os.makedirs(SPEC_DIR, exist_ok=True)
    dest = os.path.join(SPEC_DIR, XLSX_NAME)
    if os.path.abspath(src) != os.path.abspath(dest):
        shutil.copyfile(src, dest)
    for f in os.listdir(SPEC_DIR):  # 시트가 줄거나 이름이 바뀐 경우를 위해 이전 생성물을 지운다
        if re.match(r"\d{2}-.*\.md$", f):
            os.remove(os.path.join(SPEC_DIR, f))
    for i, (name, rows) in enumerate(read_sheets(dest), 1):
        slug = re.sub(r"[\s/·]+", "-", name).strip("-")
        fn = f"{i:02d}-{slug}.md"
        with open(os.path.join(SPEC_DIR, fn), "w", encoding="utf-8") as fh:
            fh.write(to_markdown(name, rows, f"docs/spec/{XLSX_NAME}"))
        print(fn)


if __name__ == "__main__":
    main()
