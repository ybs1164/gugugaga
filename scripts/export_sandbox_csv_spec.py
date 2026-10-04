"""Sandbox 입력 명세의 CSV 스냅샷 생성. 실행: python scripts/export_sandbox_csv_spec.py"""
import csv
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'docs/spec/sandbox-input-csv'
OUT.mkdir(parents=True, exist_ok=True)

def clean(s):
    return re.sub(r'\[([^]]+)\]\([^)]+\)', r'\1', s).replace('`', '').replace('**', '').strip()

def write(name, header, rows):
    with (OUT / name).open('w', encoding='utf-8-sig', newline='') as f:
        w = csv.writer(f)
        w.writerow(header)
        w.writerows(rows)

specs = {}
inherits = {}
values = []
for path in sorted((ROOT / 'docs/spec/csv').glob('*.md')):
    if path.name == 'models.md':
        continue
    current = None
    mode = None
    heading = ''
    for line in path.read_text(encoding='utf-8').splitlines():
        if line.startswith('#'):
            heading = clean(line.lstrip('#'))
        m = re.match(r'파일:\s*`([^`]+)`', line)
        if m:
            current = m[1]
            specs.setdefault(current, [])
        m = re.match(r'컬럼 상속:\s*`([^`]+)`', line)
        if m and current:
            inherits[current] = m[1]
        if line.startswith('| 컬럼 |'):
            mode = 'column'
            continue
        if line.startswith('|') and not re.match(r'^\|[- ]+\|', line):
            cells = [c.strip() for c in line.split('|')[1:-1]]
            if mode == 'column' and current:
                names = re.findall(r'`([^`]+)`', cells[0])
                for name in names:
                    if path.name == 'biomes.md' and len(cells) == 5:
                        typ, default, applies, desc = cells[1:]
                        required = '필수' if '(필수)' in default else '선택'
                    elif len(cells) == 5:
                        typ, required, default, desc = cells[1:]
                        applies = ''
                    elif len(cells) == 4:
                        typ, default, applies, desc = cells[1:]
                        required = '필수' if '(필수)' in default else ''
                    else:
                        continue
                    specs[current].append([name, clean(typ), clean(required), clean(default), clean(applies), clean(desc), path.relative_to(ROOT).as_posix()])
            elif cells and re.fullmatch(r'`[^`]+`', cells[0]):
                values.append([path.relative_to(ROOT).as_posix(), heading, clean(cells[0]), ' / '.join(clean(c) for c in cells[1:])])
        elif not line.startswith('|'):
            mode = None

by_name = {Path(p).name: p for p in specs}
for child, parent in inherits.items():
    specs[child].extend(r.copy() for r in specs[by_name[parent]])

units = specs['Assets/Resources/Tables/Units.csv']
boat_path = 'Assets/Resources/Tables/Boats.csv'
unit_by_name = {r[0]: r for r in units}
specs[boat_path] = [([r[0], *unit_by_name[r[0]][1:]] if r[0] in unit_by_name and not r[1] else r) for r in specs[boat_path]]
tables = [p for p in specs if p.startswith('Assets/Resources/') and '/Models/' not in p]
assert len(tables) == 18, tables
manifest = []
columns = []
for path in tables:
    key = 'Table.' + Path(path).stem
    relative = path.removeprefix('Assets/Resources/')
    route = 'Sandbox CSV 목록: 해당 항목의 파일 선택'
    detect = '선택한 UI 항목으로 종류 지정; 파일명과 폴더 구조 무관'
    manifest.append([key,relative,path,route,detect,'한 행 = 바이옴/타일/구조물' if Path(path).stem == 'Biomes' else '한 행 = 데이터 항목','미선택 시 기본 파일'])
    seen = set()
    for row in specs[path]:
        if row[0] in seen: continue
        seen.add(row[0])
        if 'structure' in row[0].lower() or 'Structure' in row[4] or '구조물' in row[5]:
            row = row.copy()
            row[5] = ''
        columns.append([key,*row])
    if key == 'Table.Strings':
        columns.append([key,'<언어코드>','문자열','선택','ko','','Key와 Note 이외의 열은 언어; Language.Name 행으로 언어 이름 지정','docs/spec/csv/strings.md'])

write('formats.csv',['Format','ResourceRelativePath','DefaultCsvPath','InputRoute','Detection','RowStructure','Notes'],manifest)
# 생성 폴더 안의 더 이상 지원하지 않는 명세 파일만 정리한다.
for name in ('LegacyBiomes', 'sample_units', 'TechTree', 'ModelPalette', 'BuildingModels', 'TerrainModels', 'UnitModels'):
    stale = (OUT / (name + '.spec.csv')).resolve()
    assert stale.parent == OUT.resolve()
    if stale.exists():
        stale.unlink()
write('columns.csv',['Format','Column','Type','Required','Default','AppliesTo','Description'],[r[:-1] for r in columns])
for key, _, source, *_ in manifest:
    write(Path(source).stem + '.spec.csv', ['Column','Type','Required','Default','AppliesTo','Description'], [r[1:-1] for r in columns if r[0] == key])
for source in ('docs/spec/game/unit.md', 'docs/spec/csv-common.md'):
    section = ''
    for line in (ROOT / source).read_text(encoding='utf-8').splitlines():
        if line.startswith('#'): section = clean(line.lstrip('#'))
        if line.startswith('| `'):
            cells = [clean(c) for c in line.split('|')[1:-1]]
            values.append([source,section,cells[0],' / '.join(cells[1:])])
write('values.csv',['Section','Value','Description'],[r[1:] for r in values if 'Structure' not in r[2] and '구조물' not in r[3]])

rules = []
for section in ('docs/spec/csv-common.md','docs/spec/csv/tech.md','docs/spec/csv/units.md','docs/spec/csv/biomes.md','docs/spec/csv/strings.md'):
    for line in (ROOT / section).read_text(encoding='utf-8').splitlines():
        if line.startswith('- ') or line.startswith('배열형 규칙:'):
            rules.append(['공통/관련 표',clean(line.removeprefix('- ')),section])
        if section == 'docs/spec/csv-common.md' and line.startswith('|') and not re.match(r'^\|[- ]+\|', line):
            cells = [clean(c) for c in line.split('|')[1:-1]]
            if cells[0] not in ('항목', '형식', '이름'):
                rules.append([cells[0], ' / '.join(cells[1:]),section])
rules += [
    ['Sandbox','각 항목의 파일 선택으로 해당 CSV만 교체; 기본 파일 버튼으로 해당 항목 복원; 다시 읽기는 선택한 모든 파일 재로드','Assets/Scripts/TacticsECS/BattleController.cs'],
    ['Sandbox','Pixel2D/SpriteCatalog.csv를 개별 선택 가능','Assets/Scripts/TacticsECS/Systems/Csv/GameDataLoader.cs'],
    ['명세','Required는 문서상의 작성 요구이며 모든 파서가 누락을 거부한다는 의미는 아님; 공란 기본값은 미기재','docs/spec/csv'],
    ['생성','本 파일들은 기존 명세와 코드에서 생성한 스냅샷; python scripts/export_sandbox_csv_spec.py로 갱신'.replace('本','이'),'scripts/export_sandbox_csv_spec.py'],
]
write('rules.csv',['AppliesTo','Rule'],[r[:-1] for r in rules if 'Structure' not in r[0] and '구조물' not in r[1]])

field_types = {}
domain = ''
for line in (ROOT / 'Assets/Scripts/TacticsECS/Data/GameRules.cs').read_text(encoding='utf-8').splitlines():
    m = re.search(r'public static class (\w+)', line)
    if m: domain = m[1]
    m = re.search(r'public static (\w+) (\w+)\s*=',line)
    if m: field_types[domain + '.' + m[2]] = m[1]
with (ROOT / 'Assets/Resources/GameRules.csv').open(encoding='utf-8-sig',newline='') as f:
    keys = [[r['Key'],field_types[r['Key']],r['Description'],'Assets/Resources/GameRules.csv'] for r in csv.DictReader(f) if r['Key'] and not r['Key'].startswith('#')]
write('game-rule-keys.csv',['Key','ValueType','Description'],[r[:-1] for r in keys])
print(f'Saved {len(manifest)} formats, {len(columns)} columns, {len(keys)} rule keys to {OUT}')

# 현재 입력 예제의 모든 헤더가 명세에 포함되는지 확인한다.
for key, _, source, *_ in manifest:
    path = ROOT / source
    if not path.exists():
        continue
    with path.open(encoding='utf-8-sig', newline='') as f:
        header = next(csv.reader(f))
    documented = [r[1] for r in columns if r[0] == key]
    for column in header:
        assert any(column == name or ('{n}' in name and re.fullmatch(re.escape(name).replace(r'\{n\}', r'\d+'), column)) for name in documented), (key, column)
for path in OUT.glob('*.csv'):
    with path.open(encoding='utf-8-sig', newline='') as f:
        data = list(csv.reader(f, strict=True))
    assert all(len(r) == len(data[0]) for r in data), path
assert next(r for r in columns if r[:2] == ['Table.Biomes', 'Kind'])[5] == '전부'
print('CSV specification validation: OK; all existing input headers covered')
