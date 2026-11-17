# Level catalog (`levels.db.zip`)

The repository root contains `levels.db.zip`. It must contain one file named `levels.db`, a SQLite database with table:

```sql
CREATE TABLE levels (
    download_link TEXT NOT NULL,
    preview TEXT NOT NULL,
    name TEXT NOT NULL PRIMARY KEY COLLATE NOCASE
);
```

Fields:

- `name` — displayed name and the base filename used in the local `levels/` directory (invalid filesystem characters are replaced with `_`).
- `preview` — absolute HTTP(S) image URL used by the launcher card.
- `download_link` — absolute HTTP(S) URL of the level ZIP.

The launcher always reads the public catalog from:

```text
https://raw.githubusercontent.com/Endlad2/Backrooms-Infinity/main/levels.db.zip
```

## Add/update a level

Run GitHub Actions → **Update levels database** → **Run workflow** and enter `name`, `preview`, `download_link`. The workflow validates both URLs, extracts/creates the SQLite DB, UPSERTs by case-insensitive `name`, repacks it and commits `levels.db.zip` to `main`.

If `main` has branch protection that disallows GitHub Actions pushes, grant the workflow/bot the required exception or change the workflow to open a pull request.
