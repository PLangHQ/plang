# Stage 8 — coder plan (`read-file-off-channels`)

Pure dead-code deletion. `Channels.@this.ReadAsync<T>(string filePath, ...)`
read a file from disk and deserialised — never touched a channel — and had
zero callers anywhere in PLang/, PLang.Tests/, Tests/. Plan one-liner
anticipated relocating to `app.Serializers` or FileSystem; both findings
make relocation moot:

1. Zero callers (verified).
2. `app.Serializers` no longer exists (deleted in stage 1).

## Files

`PLang/App/Channels/this.cs` only — delete the 8-line method + doc-comment.

## Verification

- `grep -n "ReadAsync<T>" PLang/App/Channels/this.cs` → 0
- `grep -rn "Channels\.ReadAsync<" PLang/ PLang.Tests/ Tests/ --include='*.cs'` → 0
- C# 2755/2755; PLang 199/199; build clean.
