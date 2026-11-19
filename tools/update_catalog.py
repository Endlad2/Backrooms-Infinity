#!/usr/bin/env python3
from __future__ import annotations
import io, json, os, sqlite3, sys, tempfile, urllib.parse, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def valid_url(value: str) -> bool:
    try:
        u=urllib.parse.urlparse(value)
        return u.scheme in {'http','https'} and bool(u.netloc)
    except Exception:
        return False

if len(sys.argv) != 4:
    raise SystemExit('usage: update_catalog.py NAME PREVIEW_URL DOWNLOAD_URL')
name, preview, download = (x.strip() for x in sys.argv[1:])
if not name: raise SystemExit('name is empty')
if not valid_url(preview): raise SystemExit('preview must be an absolute HTTP(S) URL')
if not valid_url(download): raise SystemExit('download_link must be an absolute HTTP(S) URL')

zip_path = ROOT / 'levels.db.zip'
fd, dbpath = tempfile.mkstemp(suffix='.db'); os.close(fd)
try:
    dbfile=Path(dbpath)
    if zip_path.exists():
        with zipfile.ZipFile(zip_path) as z:
            if 'levels.db' in z.namelist(): dbfile.write_bytes(z.read('levels.db'))
    conn=sqlite3.connect(dbpath)
    conn.execute('CREATE TABLE IF NOT EXISTS levels (download_link TEXT NOT NULL, preview TEXT NOT NULL, name TEXT NOT NULL PRIMARY KEY COLLATE NOCASE)')
    conn.execute('INSERT INTO levels(download_link,preview,name) VALUES(?,?,?) ON CONFLICT(name) DO UPDATE SET download_link=excluded.download_link,preview=excluded.preview', (download,preview,name))
    conn.commit()
    rows=list(conn.execute('SELECT download_link, preview, name FROM levels ORDER BY name COLLATE NOCASE'))
    conn.close()
    tmp=zip_path.with_suffix('.zip.tmp')
    with zipfile.ZipFile(tmp,'w',zipfile.ZIP_DEFLATED) as z: z.write(dbpath,'levels.db')
    tmp.replace(zip_path)
    bundled=[{'DownloadLink':dl,'Preview':pv,'Name':nm} for dl,pv,nm in rows]
    (ROOT/'data').mkdir(exist_ok=True)
    (ROOT/'data/catalog.json').write_text(json.dumps(bundled, ensure_ascii=False, indent=2)+'\n','utf-8')
    print(f'Updated {name}; catalog now contains {len(rows)} row(s).')
finally:
    Path(dbpath).unlink(missing_ok=True)
