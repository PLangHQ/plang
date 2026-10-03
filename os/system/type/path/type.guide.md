# Paths

A `path` is where a file, a folder, or a web resource is — `'data.json'`, `'inbox/'`, or a URL like `'https://api.example.com/x.json'`. The same path works whether it points at disk or the web. Its members read with a dot.

## Parts

- `%file.name%` — the file name with extension (`data.json`); `%file.stem%` — without it (`data`); `%file.extension%` — the extension (`.json`).
- `%file.parent%` — the containing folder, a `path`; `%file.relative%` — the path relative to the app root.
- `%file.mime%` — the MIME type for the extension (`application/json`).

## Existence

- `%file.exists%` — whether something is there (a filesystem stat, or an HTTP HEAD for a URL).
- A path owns its truthiness as existence, so `if %file%` asks the same thing.
