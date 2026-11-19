# Level catalog (`levels.db.zip`)

The repository root contains `levels.db.zip`. It contains one file named `levels.db`, a SQLite database with table:

```sql
CREATE TABLE levels (
    download_link TEXT NOT NULL,
    preview TEXT NOT NULL,
    name TEXT NOT NULL PRIMARY KEY COLLATE NOCASE
);
```

Fields:

- `name` — displayed name and base filename used in the local `levels/` directory;
- `preview` — absolute HTTP(S) image URL used by the level card;
- `download_link` — absolute HTTP(S) URL of the level ZIP.

The game menu reads the public catalog from:

```text
https://raw.githubusercontent.com/Endlad2/Backrooms-Infinity/main/levels.db.zip
```

If the network is unavailable it tries the cached catalog, then the bundled `data/catalog.json` fallback.

## Add/update a level

Run GitHub Actions → **Update levels database** → **Run workflow** and enter `name`, `preview`, `download_link`. The workflow updates `levels.db.zip` and the bundled JSON fallback.
