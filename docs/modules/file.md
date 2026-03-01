# File Module

Read, write, copy, move, and delete files. All paths are relative to the project root unless absolute.

## Actions

### read

Read a file's contents.

```plang
- read 'config.json' into %config%
- write out %config%

/ Read a text file
- read file.txt into %content%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Path | string | yes | Path to the file |

**Returns:** The file contents. JSON files are parsed into objects automatically.

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
