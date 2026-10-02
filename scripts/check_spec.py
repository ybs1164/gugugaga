"""docs/spec 검사: CSV 헤더와 컬럼 표가 맞는지, 문서 링크·앵커가 살아 있는지.

사용: python scripts/check_spec.py   (문제가 있으면 종료 코드 1)

csv/*.md 형식 약속:
  - "파일: `경로`" 줄 다음에 나오는 "| 컬럼 |" 표가 그 CSV의 컬럼 명세다.
  - "컬럼 상속: `파일명`" 줄이 있으면 그 파일의 컬럼 표도 이 파일의 명세로 친다.
  - 반복 컬럼은 `Name{n}`으로 적는다(Name1, Name2 ... 와 맞는다).
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "spec"
LINK_DOCS = [ROOT / "README.md", ROOT / "CLAUDE.md", *SPEC.rglob("*.md")]

errors = []
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")


def slug(heading):
    text = re.sub(r"[`*_]", "", heading).strip().lower()
    text = re.sub(r"[^\w\- ]", "", text)
    return text.replace(" ", "-")


def anchors(md_path):
    found = set()
    for line in md_path.read_text(encoding="utf-8").splitlines():
        m = re.match(r"#{1,6}\s+(.*)", line)
        if m:
            found.add(slug(m.group(1)))
    return found


def check_links():
    for doc in LINK_DOCS:
        text = doc.read_text(encoding="utf-8")
        text = re.sub(r"```.*?```", "", text, flags=re.S)
        for target in re.findall(r"\]\(([^)\s]+)\)", text):
            if re.match(r"[a-z]+://", target):
                continue
            path_part, _, anchor = target.partition("#")
            dest = doc if not path_part else (doc.parent / path_part).resolve()
            if not dest.exists():
                errors.append(f"{doc.relative_to(ROOT)}: 없는 링크 대상 '{target}'")
                continue
            if anchor and dest.suffix == ".md" and anchor not in anchors(dest):
                errors.append(f"{doc.relative_to(ROOT)}: 없는 앵커 '{target}'")


def documented_columns():
    """CSV 경로(파일명) -> 문서에 적힌 컬럼 이름 목록. 상속은 나중에 푼다."""
    columns, inherits = {}, {}
    for md in sorted((SPEC / "csv").glob("*.md")):
        current, in_table = None, False
        for line in md.read_text(encoding="utf-8").splitlines():
            m = re.match(r"파일:\s*`([^`]+)`", line)
            if m:
                current, in_table = m.group(1), False
                columns.setdefault(current, [])
                continue
            m = re.match(r"컬럼 상속:\s*`([^`]+)`", line)
            if m and current:
                inherits[current] = m.group(1)
                continue
            if current is None:
                continue
            if line.startswith("| 컬럼 |"):
                in_table = True
                continue
            if in_table:
                if not line.startswith("|"):
                    in_table = False
                    continue
                first = line.split("|")[1]
                columns[current].extend(re.findall(r"`([^`]+)`", first))
    by_name = {pathlib.PurePosixPath(k).name: k for k in columns}
    for child, parent in inherits.items():
        columns[child] = columns[child] + columns.get(by_name.get(parent, parent), [])
    return columns


def matches(header, names):
    for n in names:
        if n.endswith("{n}"):
            base = n[:-3]
            if re.fullmatch(re.escape(base) + r"\d+", header, re.I):
                return True
        elif n.lower() == header.lower():
            return True
    return False


def check_columns():
    for csv_path, names in documented_columns().items():
        path = ROOT / csv_path
        if not path.exists():
            errors.append(f"spec: 없는 CSV '{csv_path}'")
            continue
        if not names:
            continue  # 컬럼 표가 없는 파일(레거시 등)
        header = path.read_text(encoding="utf-8-sig").splitlines()[0].split(",")
        for h in (h.strip() for h in header):
            if h and not matches(h, names):
                errors.append(f"{csv_path}: 컬럼 '{h}'이(가) spec 컬럼 표에 없음")


def check_unlisted_csvs():
    listed = {pathlib.Path(ROOT / p).resolve() for p in documented_columns()}
    for path in (ROOT / "Assets" / "Resources").rglob("*.csv"):
        if path.resolve() not in listed:
            errors.append(f"{path.relative_to(ROOT)}: 이 CSV를 설명하는 spec이 없음")


check_links()
check_columns()
check_unlisted_csvs()
for e in errors:
    print("FAIL", e)
print(f"check_spec: {'OK' if not errors else str(len(errors)) + ' problem(s)'}")
sys.exit(1 if errors else 0)
