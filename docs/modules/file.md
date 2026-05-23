# File Module

Read, write, copy, move, and delete files. Local paths are relative to the project root unless absolute. **URLs work too** — see *Paths can be URLs* below.

## Paths can be URLs

The `Path` parameter on every action in this module is polymorphic. Anything that looks like a URL is routed to the matching scheme handler; bare paths and `file://` go to the local filesystem.

```plang
/ Local file
- read 'config.json' into %config%

/ HTTPS URL — same action, GET request under the covers
- read 'https://api.example.com/users.json' into %users%

/ Variable holding either — the program doesn't care which
- read %source%, write to %content%
```

Today the registered schemes are `file://` (and bare paths) and `http(s)://`. The mapping per scheme:

| Action | `file://` (local) | `http(s)://` |
|--------|-------------------|--------------|
| `read` | open + read | GET |
| `save` | write to disk | POST (server decides; 405 → `on error`) |
| `exists` | stat | HEAD, 2xx = true |
| `delete` | unlink | DELETE |
| `list` | directory entries | server-defined (usually unsupported) |
| `copy` / `move` | filesystem rename / copy | base default: read source → write destination |

**Consent prompts apply to any path.** The first time your program touches an HTTPS URL the runtime asks for permission the same way it does for a local file — `Allow worker to read https://api.example.com/users.json? (y/n/a)`. Grants are scoped per `(actor, canonical-path, verb)`. For HTTP that means scheme + lowercased host + default-port-stripped + normalized path + sorted query — so `HTTPS://API.example.com/users.json?b=2&a=1` and `https://api.example.com/users.json?a=1&b=2` count as the same resource.

**Errors come back as data.** A non-2xx HTTP response is not an exception — it surfaces the same way a permission-denied or disk-full does, and you handle it via `on error`. See the [HTTP module](http.md) for the status-to-error-key mapping (`NotFound`, `MethodNotAllowed`, `NetworkError`, …).

**When to use which.** `read %url%` is the shorthand for "GET this and give me the body." Reach for the explicit [HTTP module](http.md) (`- get %url%, write to %x%`) when you need to set method, headers, or a request body — the HTTP module exposes the full verb surface; this one is the one-liner.

## Actions

### read

Read a file's contents.

```plang
- read 'config.json' into %config%
- write out %config%

/ Read a text file
- read file.txt into %content%

/ Read a template and resolve %var% references in the content
- read 'greeting.txt', load vars, write to %greeting%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Path | string | yes | — | Path to the file |
| ResolveVariables | bool | no | false | Resolve `%var%` references inside the file content before returning |

**Returns:** The file contents. JSON files are parsed into objects automatically.

#### Resolving variables in file content

Setting `ResolveVariables` (natural form: `load vars`) treats the file as a small template — any `%name%` token in the content is replaced with the variable's current value before the result is returned:

```plang
- set %name% = 'World'
- read 'greeting.txt', load vars, write to %greeting%
/ greeting.txt = "Hello, %name%!" → %greeting% becomes "Hello, World!"
```

**Security:** infrastructure variables (the `%!app%`, `%!fileSystem%`, `%!callStack%`, `%!trace%` family — anything starting with `!`) are deliberately **not** resolved when reading file content, because the file contents may be untrusted. Only ordinary user variables resolve. If you need to expand `%!` variables in a string, build the string explicitly in `.goal` code rather than reading it from disk.

### save

Write content to a file.

```plang
/ Write text
- save 'Hello world' to file 'output.txt'

/ Write a variable
- save %data% to file 'result.json'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Path | string | yes | Path to write to |
| Value | object | yes | Content to write |

### copy

Copy a file or directory.

```plang
- copy 'source.txt' to 'backup.txt'
- copy 'source.txt' to 'backup.txt', overwrite
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Source | string | yes | — | Source path |
| Destination | string | yes | — | Destination path |
| Overwrite | bool | no | false | Overwrite if destination exists |
| IncludeSubfolders | bool | no | true | Include subfolders when copying directories |

### move

Move (rename) a file or directory.

```plang
- move 'old-name.txt' to 'new-name.txt'
- move 'old-name.txt' to 'new-name.txt', overwrite
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Source | string | yes | — | Source path |
| Destination | string | yes | — | Destination path |
| Overwrite | bool | no | false | Overwrite if destination exists |

### delete

Delete a file or directory.

```plang
- delete file 'temp.txt'
- delete file 'temp-folder', recursive
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Path | string | yes | — | Path to delete |
| IgnoreIfNotFound | bool | no | false | Don't error if the file doesn't exist |
| Recursive | bool | no | false | Delete directory contents recursively |

### exists

Check if a file or directory exists.

```plang
- check if 'config.json' exists, write to %configExists%
- if %configExists% is true then call LoadConfig
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Path | string | yes | Path to check |

**Returns:** File metadata including `Exists` (bool), `Path`, `Size`, `Type` (MIME type).

### list

List files in a directory.

```plang
- list files in 'data/', write to %files%
- list files in 'logs/' matching '*.txt', recursive, write to %logFiles%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Path | string | yes | — | Directory to list |
| Pattern | string | no | "*" | Glob pattern to filter files |
| Recursive | bool | no | false | Include subdirectories |

**Returns:** A list of file objects with `Path`, `AbsolutePath`, `Size`, `Type`, and `Exists` properties.

## Examples

### Read, Modify, Save

```plang
Start
- read 'data.json' into %data%
- set %data.processed% = true
- save %data% to file 'data.json'
```

### Copy with Backup

```plang
BackupAndUpdate
- copy 'config.json' to 'config.backup.json', overwrite
- save %newConfig% to file 'config.json'
```

### List and Process Files

```plang
ProcessAll
- list files in 'inbox/' matching '*.csv', write to %files%
- foreach %files%, call ProcessFile item=%file%

ProcessFile
- read %file.Path% into %content%
- write out 'Processing: %file.Path% (%file.Size% bytes)'
```
