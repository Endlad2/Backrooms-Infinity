#!/usr/bin/env python3
from __future__ import annotations
import io, json, os, re, sqlite3, sys, tempfile, zipfile
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
errors: list[str] = []
infos: list[str] = []

def fail(msg: str) -> None: errors.append(msg)
def ok(msg: str) -> None: infos.append(msg)

required = [
    'project.godot', 'Main.tscn', 'Backrooms.Infinity.csproj', 'levels.db.zip',
    'data/catalog.json', 'src/App/MainController.cs', 'src/App/DebuggerOverlay.cs',
    'src/App/GameWorld.cs', 'src/App/GdScriptHost.cs', 'src/App/GdScriptContext.cs',
    'src/App/LevelScriptRuntime.cs', 'src/App/SqliteCatalogReader.cs', 'docs/GDSCRIPT_API.md', 'shaders/vhs.gdshader',
    'assets/common/vcrosdmonorus_vhsicons.ttf', 'assets/common/FONT-COPYRIGHT.txt',
    'assets/menu/level4.png', 'assets/menu/level8.png', 'assets/menu/level10.png',
    'assets/menu/Backrooms - Level 4.mp3', 'assets/menu/Backrooms - Level 8.mp3',
    'assets/menu/Backrooms - Level 10.mp3',
]
for rel in required:
    if not (ROOT / rel).is_file(): fail(f'missing required file: {rel}')

# One project / one application architecture.
csprojs = [p for p in ROOT.rglob('*.csproj') if '.godot' not in p.parts and 'obj' not in p.parts and 'bin' not in p.parts]
if [p.name for p in csprojs] != ['Backrooms.Infinity.csproj']:
    fail('expected exactly one csproj: Backrooms.Infinity.csproj; got: ' + ', '.join(str(p.relative_to(ROOT)) for p in csprojs))
else:
    try:
        ET.parse(csprojs[0]); ok('csproj XML parses')
    except Exception as e: fail(f'csproj XML invalid: {e}')

project = (ROOT / 'project.godot').read_text('utf-8', errors='replace') if (ROOT/'project.godot').exists() else ''
if 'run/main_scene="res://Main.tscn"' not in project: fail('project.godot main scene is not Main.tscn')
if 'project/assembly_name="Backrooms.Infinity"' not in project: fail('project.godot assembly name mismatch')

# No old process split or game headless implementation in source.
for p in (ROOT / 'src').rglob('*.cs'):
    text = p.read_text('utf-8', errors='replace')
    for token in ('Backrooms.Launcher', 'Backrooms.Client', 'Silk.NET', 'SDL3', 'JoltPhysics', 'ImGui.NET'):
        if token in text: fail(f'legacy runtime token {token!r} in {p.relative_to(ROOT)}')
    if '--headless' in text: fail(f'game-specific --headless remains in {p.relative_to(ROOT)}')

# C# regression checks for known compile blockers.
for p in (ROOT / 'src').rglob('*.cs'):
    text = p.read_text('utf-8', errors='replace')
    if 'namespace Backrooms.Engine.Math;' in text or 'using Backrooms.Engine.Math;' in text:
        fail(f'legacy Backrooms.Engine.Math namespace shadows System.Math in {p.relative_to(ROOT)}')
    if 'Toggled += changed;' in text:
        fail(f'Action<bool> connected directly to Godot Toggled delegate in {p.relative_to(ROOT)}')
    if 'Pressed += close ??' in text:
        fail(f'Action connected directly to Godot Pressed delegate in {p.relative_to(ROOT)}')
    if '.AoEnabled' in text or '.AoTexture' in text:
        fail(f'Godot C# AO properties must use AOEnabled/AOTexture casing in {p.relative_to(ROOT)}')
    if 'Engine.GetFramesPerSecond()' in text and 'Godot.Engine.GetFramesPerSecond()' not in text:
        fail(f'unqualified Engine.GetFramesPerSecond() can resolve to Backrooms.Engine namespace in {p.relative_to(ROOT)}')

# Script runtime / SQLite fallback regression checks.
model = (ROOT/'src/Core/Levels/LevelModel.cs').read_text('utf-8', errors='replace')
parser = (ROOT/'src/Core/Levels/LevelParser.cs').read_text('utf-8', errors='replace')
gdhost = (ROOT/'src/App/GdScriptHost.cs').read_text('utf-8', errors='replace')
catalog = (ROOT/'src/App/LevelCatalogService.cs').read_text('utf-8', errors='replace')
csproj = (ROOT/'Backrooms.Infinity.csproj').read_text('utf-8', errors='replace')
if 'LevelScriptType' not in model: fail('LevelScriptType enum missing')
if 'Every <script> must declare type=' not in parser: fail('parser does not require explicit script type')
if 'new GDScript' not in gdhost or '.Reload()' not in gdhost or '.New()' not in gdhost:
    fail('runtime GDScript compile/instantiate path missing')
if 'CatalogJsonUri' not in catalog or 'IsSqliteRuntimeFailure' not in catalog:
    fail('SQLite runtime fallback to JSON catalog missing')
if '<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>' not in csproj:
    fail('CopyLocalLockFileAssemblies must be true for Godot NuGet runtime dependencies')
if '<EnableDynamicLoading>true</EnableDynamicLoading>' not in csproj:
    fail('EnableDynamicLoading must be true for Godot .NET runtime dependencies')

# UI/input regression checks.
settings_panel = (ROOT/'src/App/SettingsPanel.cs').read_text('utf-8', errors='replace')
game_world = (ROOT/'src/App/GameWorld.cs').read_text('utf-8', errors='replace')
if 'CustomMinimumSize = new Vector2(720, 470)' not in settings_panel or 'SizeFlagsVertical = SizeFlags.ExpandFill' not in settings_panel:
    fail('settings panel is not constrained to a scrollable height suitable for the ESC menu')
if 'MouseButton.WheelUp or MouseButton.WheelDown' not in game_world or 'hotbarSlots = 4' not in game_world:
    fail('mouse-wheel hotbar cycling is missing')

# Debugger must be wired globally.
dbg = (ROOT/'src/App/DebuggerOverlay.cs').read_text('utf-8', errors='replace')
main = (ROOT/'src/App/MainController.cs').read_text('utf-8', errors='replace')
if 'F3' not in (ROOT/'src/Core/Input/InputBindings.cs').read_text('utf-8', errors='replace'): fail('F3 binding missing')
if 'DebugOverlayState' not in dbg or 'DiagnosticSeverity.Error' not in (ROOT/'src/Core/Diagnostics/Diagnostics.cs').read_text('utf-8', errors='replace'):
    fail('debugger/diagnostic error wiring missing')
if 'new DebuggerOverlay' not in main: fail('debugger is not created by MainController')

# Verify the 50 bundled level archives and XML parseability.
levels = ROOT / 'levels'
level_zips = sorted(levels.glob('Level-*.zip'), key=lambda p: int(re.search(r'(\d+)', p.stem).group(1)))
nums = [int(re.search(r'(\d+)', p.stem).group(1)) for p in level_zips]
if nums != list(range(50)):
    fail(f'expected Level-0..49.zip, got {nums[:8]}... count={len(nums)}')
else:
    ok('found Level-0..49.zip')

for p in level_zips:
    try:
        with zipfile.ZipFile(p) as z:
            bad = z.testzip()
            if bad: fail(f'{p.name}: corrupt member {bad}'); continue
            names = z.namelist()
            xml_name = 'level.xml' if 'level.xml' in names else next((n for n in names if n.lower().endswith('/level.xml')), None)
            if not xml_name: fail(f'{p.name}: no level.xml'); continue
            root = ET.fromstring(z.read(xml_name))
            chunks = root.find('chunks')
            if chunks is None: fail(f'{p.name}: <chunks> missing')
            elif not chunks.get('size'): fail(f'{p.name}: chunks size missing')
            ids: set[str] = set()
            duplicates: set[str] = set()
            for elem in root.iter():
                if elem.tag in {'object','model'} and elem.get('id'):
                    eid = elem.get('id')
                    if eid in ids: duplicates.add(eid)
                    ids.add(eid)
            if duplicates: fail(f'{p.name}: duplicate unified entity id(s): {sorted(duplicates)[:5]}')
            for script in root.iter('script'):
                script_type = (script.get('type') or '').strip().lower()
                if script_type not in {'lua', 'gdscript'}:
                    fail(f'{p.name}: every <script> must declare type=\"lua\" or type=\"gdscript\"')
                    continue
                src = (script.get('src') or '').strip().lower()
                if src and script_type == 'lua' and not src.endswith('.lua'):
                    fail(f'{p.name}: type=lua script must use .lua source: {src}')
                if src and script_type == 'gdscript' and not src.endswith('.gd'):
                    fail(f'{p.name}: type=gdscript script must use .gd source: {src}')
    except Exception as e:
        fail(f'{p.name}: {type(e).__name__}: {e}')

# Verify catalog DB and bundled JSON mirror.
rows: list[tuple[str,str,str]] = []
try:
    data = (ROOT/'levels.db.zip').read_bytes()
    with zipfile.ZipFile(io.BytesIO(data)) as z:
        if z.testzip(): fail('levels.db.zip is corrupt')
        if 'levels.db' not in z.namelist(): fail('levels.db.zip does not contain levels.db')
        else:
            fd, dbname = tempfile.mkstemp(suffix='.db'); os.close(fd)
            try:
                Path(dbname).write_bytes(z.read('levels.db'))
                conn = sqlite3.connect(dbname)
                cols = [r[1] for r in conn.execute('pragma table_info(levels)')]
                if cols != ['download_link','preview','name']:
                    fail(f'levels schema columns are {cols!r}')
                rows = list(conn.execute('select download_link, preview, name from levels order by name collate nocase'))
                conn.close()
            finally:
                Path(dbname).unlink(missing_ok=True)
except Exception as e:
    fail(f'levels.db.zip validation failed: {e}')

if len(rows) != 50: fail(f'catalog expected 50 rows, got {len(rows)}')
for dl, preview, name in rows:
    if not dl.startswith('https://raw.githubusercontent.com/Endlad2/Backrooms-Infinity/'):
        fail(f'{name}: download_link is not raw.githubusercontent.com')
    if not preview.startswith(('https://','http://')):
        fail(f'{name}: preview URL is not HTTP(S)')

try:
    bundled = json.loads((ROOT/'data/catalog.json').read_text('utf-8'))
    normalized_db = {(r[2], r[0], r[1]) for r in rows}
    normalized_json = {(r['Name'], r['DownloadLink'], r['Preview']) for r in bundled}
    if normalized_db != normalized_json: fail('data/catalog.json does not mirror levels.db.zip')
    else: ok(f'catalog mirrors {len(rows)} DB rows')
except Exception as e:
    fail(f'catalog.json invalid: {e}')

# Basic YAML parse when PyYAML is available.
try:
    import yaml
    for p in (ROOT/'.github/workflows').glob('*.yml'):
        with p.open('r', encoding='utf-8') as f: yaml.safe_load(f)
    ok('workflow YAML parses')
except ImportError:
    pass
except Exception as e:
    fail(f'workflow YAML invalid: {e}')

for msg in infos: print('[OK]', msg)
if errors:
    for msg in errors: print('[ERROR]', msg, file=sys.stderr)
    print(f'Validation failed: {len(errors)} error(s).', file=sys.stderr)
    raise SystemExit(1)
print(f'Validation passed: {len(level_zips)} level ZIPs, {len(rows)} catalog rows, unified Godot project.')
