# path
Where a file, a folder or a web resource is: a path in the app, an absolute path, or a URL.

# Paths

A `path` is where a file, a folder, or a web resource is — `'data.json'`, `'inbox/'`, or a URL like `'https://api.example.com/x.json'`. The same path works whether it points at disk or the web. Its members read with a dot.

## Parts

- `%file.name%` — the file name with extension (`data.json`); `%file.stem%` — without it (`data`); `%file.extension%` — the extension (`.json`).
- `%file.parent%` — the containing folder, a `path`; `%file.relative%` — the path relative to the app root.
- `%file.mime%` — the MIME type for the extension (`application/json`).

## Existence

- `%file.exists%` — whether something is there (a filesystem stat, or an HTTP HEAD for a URL).
- A path owns its truthiness as existence, so `if %file%` asks the same thing.

## relative
The path written relative to the app's root.

`%file.relative%`

**Returns:** a path.

## extension
The file extension, including the dot (.json).

`%file.extension%`

**Returns:** a text.

## name
The file name with its extension (data.json).

`%file.name%`

**Returns:** a text.

## stem
The file name without its extension (data).

`%file.stem%`

**Returns:** a text.

## mime
The MIME type for the path extension (application/json).

`%file.mime%`

**Returns:** a text.

## parent
The folder that contains it, as a path.

`%file.parent%`

**Returns:** a path.

## exists
Whether something is there at the path (a filesystem check, or an HTTP HEAD for a URL).

`%file.exists%`

**Returns:** true or false (a bool).
