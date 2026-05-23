# Coder v2 — lowercase path aliases + Stage 8 (System.IO.Abstractions removal)

Follow-up to v1 (stages 1–7), driven by two Ingi requests.

## 1. Lowercase `path` aliases (done, committed 4923f6a1)

PascalCase `Path` collided with `System.IO.Path`. Lowercase `path` does not
(C# is case-sensitive) and matches the PLang concept name. Added global aliases
`path` / `filepath` / `httppath`; production call sites cleaned.

## 2. Stage 8 — remove System.IO.Abstractions (done)

Ingi's insight, verified in code: the root-jail (`FileAccessControl`) was already
dead — replaced by `Path.Authorize` / `Actor.Permission`. `IPLangFileSystem` was
pure double-wrapping. `ValidatePath` is now just path normalization, not security.

Removed: `IPLangFileSystem`, `PLangFileSystem` + the 9 `Default/*` System.IO.Abstractions
wrappers, the `System.IO.Abstractions` NuGet package, the dead `FileAccessControl`
machinery, the `App.FileSystem` property, the `%!fileSystem%` context variable.

`ValidatePath` relocated as a static on `filepath` (`filepath.ValidatePath(raw, app)`)
— `Resolve` calls it; bootstrap callers (no Goal) call it directly.

~18 production files + ~20 test files migrated from `App.FileSystem.{File,Directory,Path}`
to `System.IO.*`.

## Status

- C# tests: 2875 / 2875 pass.
- PLang `--test`: 202 pass, 0 fail, 1 stale.
  - The 1 stale (`ContextVars2.test.goal`) — its `%!fileSystem%` assertion was
    removed (correct); the `.pr` rebuild is blocked by a **pre-existing** breakage
    (`plang build` fails on a `builder.app`→`builder.load` staleness in
    `os/system/builder/`, untouched on this branch).
