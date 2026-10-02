# File Module
Read, write, copy, move, delete, list, and check whether files exist through the configured filesystem abstraction. A `%!x.setting%` is a setting, never a file: `save %!llm.setting%` saves a setting, which is the setting module.

## Paths can be URLs

The `Path` on every action in this module is polymorphic. Anything that looks like a URL is routed to the matching scheme handler; a bare path or `file://` goes to the local filesystem.

- read 'config.json', write to %config%
- read 'https://api.example.com/users.json', write to %users%
- read %source%, write to %content%

The last one works whether `%source%` holds a local path or a URL — the program doesn't care which. The registered schemes are `file://` (and bare paths) and `http(s)://`:

| Action | `file://` (local) | `http(s)://` |
|--------|-------------------|-------------|
| read | open and read | GET |
| save | write to disk | POST (the server decides; 405 surfaces as `MethodNotAllowed`) |
| exists | stat | HEAD (2xx means it exists) |
| delete | remove | DELETE |
| list | directory entries | server-defined, usually unsupported |
| copy / move | filesystem copy / rename | read the source, then write the destination |

**Consent applies to any path.** The first time your program touches an HTTPS URL, the runtime asks the same way it does for a local file — `Allow worker to read https://api.example.com/users.json? (y/n/a)`; answering `a` remembers the grant. Grants are scoped per actor, path, and verb, so the same URL read by two actors is asked of each. For an HTTP URL the path is canonicalized first — scheme and host lowercased, the default port (80 for http, 443 for https) dropped, query parameters sorted by key — so `HTTPS://API.example.com/u.json?b=2&a=1` and `https://api.example.com/u.json?a=1&b=2` count as the same resource.

**Errors come back as data.** A non-2xx response is not an exception — it arrives the way a permission denial or a disk-full error does, and you handle it with `on error`. The status maps to an error key: 404 is `NotFound`, 405 is `MethodNotAllowed`, and a network failure is `NetworkError`. See the [http](http.md) module for the full mapping.

**When to use which.** `read %url%` is the shorthand for "GET this and give me the body." Reach for the [http](http.md) module (`- get %url%, write to %x%`) when you need to set the method, headers, or a request body — it exposes the full verb surface; this module is the one-liner.

## Examples

### Read, modify, save

```plang
Start
- read 'data.json', write to %data%
- set %data.processed% = true
- save %data% to file 'data.json'
- write out "processed: %data.processed%"
```

`data.json` was `{"name": "orders", "processed": false}`. Printed `processed: true`; the file is now `{"name":"orders","processed":true}`.

### Copy with backup

```plang
Start
- copy 'config.json' to 'config.backup.json', overwrite
- set %newConfig% = {"version": 2}
- save %newConfig% to file 'config.json'
- read 'config.backup.json', write to %old%
- write out "backup is version %old.version%, config is now version %newConfig.version%"
```

`config.json` was `{"version": 1}`. Printed `backup is version 1, config is now version 2`.

### List and process files

```plang
Start
- list files in 'inbox' matching "*.csv", write to %files%
- foreach %files%, call ProcessFile file=%item%

ProcessFile
- read %file%, write to %content%
- write out "Processing: %file%"
```

`inbox/` held `a.csv`, `b.csv` and `notes.txt`. Printed `Processing: inbox/a.csv`, `Processing: inbox/b.csv` — `notes.txt` is skipped, so you can see `matching "*.csv"` doing its work.

## delete
Delete a file or directory at Path, optionally recursively; nothing there is NotFound

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Path | `file '<path>'` | path | yes | — | the file or folder to delete |
| Recursive | `recursive` | bool | no | false | delete a folder's contents too |

**Returns:** the deleted path.

## copy
Copy a file or folder from Source to Destination, optionally overwriting and including subfolders

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Source | the first path, inline | path | yes | — | the file or folder to copy from |
| Destination | `to '<path>'` | path | yes | — | where the copy goes |
| Overwrite | `overwrite` | bool | no | false | replace the destination if it already exists |
| Subfolder | (on by default) | choice<subfolder> | no | include | when copying a folder, copy its sub-folders too |

**Returns:** the destination path.

## read
Read a file's content; optionally resolve %var% patterns in the text before returning

- read file.txt, write to %content%
- read 'config/settings.json'
- read 'receipt.txt', load vars, write to %receipt%

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Path | the path, inline | path | yes | — | the file to read |
| Template | load vars, fill in the variables, with variables | choice<template> | no | — | the kind of template the file's text is: plang fills its %variables% from memory |

**Returns:** the file's content. A JSON file is navigable; it is parsed when first navigated.

### Reading as a template

With `load vars`, the read treats the file's text as a template: each `%variable%` in it is filled with that variable's current value. While the text still holds `%variables%` to fill, the read stays lazy — re-read and re-filled at each use, so it reflects the latest values; text with no variables left to fill is kept once opened.

Infrastructure variables are never filled. A `%!…%` name — engine internals like `%!app%`, `%!trace%`, `%!fileSystem%` — is left exactly as written, because a file's content may be untrusted; only the program's own `%variables%` resolve. To put an `%!…%` value into a string, build the string in `.goal` code rather than reading it from a file.

For example, a `greeting.txt` of:

```text
Hello, %name%!
Trace: %!trace.id%
```

read with `load vars`:

```plang
Start
- set %name% = "World"
- read 'greeting.txt', load vars, write to %greeting%
- write out %greeting%
```

prints:

```text
Hello, World!
Trace: %!trace.id%
```

`%name%` is filled; `%!trace.id%` is left exactly as written.

## exists
Check whether a file or directory exists at Path — what "if '<file>' exists" or "when the file is there" asks — returning the path, whose truthiness is its existence, to feed a condition

- check if file.txt exists, write to %fileInfo%
- if 'list.json' exists, write out "there"

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Path | `check if '<path>' exists` | path | yes | — | the file or folder to check for |

**Returns:** the path itself; whether it exists is the value's truthiness, so `if %x% is true` probes it (a filesystem stat, or an HTTP HEAD for a URL) at the moment you test it.

## save
Write Value to a file at Path, creating directories as needed. Saving a `%!x.setting%` setting (`save %!llm.setting%`) is not this: that is setting.save.

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Path | `to file '<path>'` | path | yes | — | the file to write |
| Value | the content, inline | item | yes | — | what to write |

**Returns:** the path that was written.

## list
List files in a directory matching an optional glob pattern, optionally recursing into subdirectories

- list files in docs/ recursive, write to %files%
- list files in %folder%

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Path | the folder, inline | path | yes | — | the folder to list |
| Pattern | `matching '<glob>'` | text | no | * | which files come back, as a glob |
| Recursive | `recursive` | bool | no | false | whether sub-folders are searched too |

**Returns:** a list of `path` values.

## move
Move or rename a file from Source to Destination, optionally overwriting the target

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Source | the first path, inline | path | yes | — | the file or folder to move from |
| Destination | `to '<path>'` | path | yes | — | where it moves to (this renames it) |
| Overwrite | `overwrite` | bool | no | false | replace the destination if it already exists |

**Returns:** the destination path.
