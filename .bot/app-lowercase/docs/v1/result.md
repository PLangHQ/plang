# docs v1 — result

## Summary

Final docs gate for `app-lowercase`. All upstream verdicts PASS. Applied the
coder's CLAUDE.md proposal, scrubbed leftover `App.X` drift in C# docstrings
and Documentation/v0.2 + Runtime2 docs, and rewrote `app-tree.md` to reflect
the seven OBP merges. Build still clean.

## Changes

### Repo CLAUDE.md — coder v3 proposal applied

- Line 18 (Console.* rule): `global::App.Channels.@this.Output` → `global::app.channels.@this.Output`.
- Line 39 (PLNG001 gate): `Data<App.Variables.Variable>` → `Data<app.variables.Variable>`.
- Line 41 (test alias clash): source-side path lowercased; clarified that test folders under `PLang.Tests/App/` stay PascalCase.
- New first bullet in "Runtime2 Conventions" documenting: lowercase vocab vs PascalCase infra, seven engine concepts merged under `app/modules/`, property-name PascalCase carve-out (the most error-prone aspect of the rename), `default` C# keyword carve-out at `app/filesystem/Default/`, and the two pending PLang action renames (`environment.run`, `builder.load`).

### R1 + R2 docstring scrub (10 sites)

`PLang/app/data/this.cs:554`, `PLang/app/GlobalUsings.cs:64` + comment-block lines 19/20/56/68-79, `PLang/app/channels/channel/events/this.cs:10`, `PLang/app/types/Registry.cs:39`, `PLang/app/callstack/call/Position.cs:8`, `PLang/app/modules/settings/IStore.cs:63`, `PLang/app/errors/CallbackGoalErrors.cs:27`, `PLang.Generators/Discovery/this.cs:41`, `PLang.Tests/GlobalUsings.cs:58-59`.

Comments only — no code semantics changed.

### Documentation sweep

| File | Change |
|---|---|
| `Documentation/v0.2/app-tree.md` | Rewrote: lowercased all paths; remapped Cache/Builder/Callback/Settings/Modules/Code/Debug rows to `app/modules/<name>/`; added Case-Convention section with the property-vs-type rule; renamed module `app` → `environment`; noted `builder.app` → `builder.load` |
| `Documentation/v0.2/build.md` | `PLang/App/Builder/this.cs` → `PLang/app/modules/builder/this.cs` |
| `Documentation/v0.2/build_process.md` | Three goal/builder paths lowercased |
| `Documentation/v0.2/builder-data-t-roadmap.md` | Three Modules/Utils paths lowercased |
| `Documentation/v0.2/todos.md` | All 20 `PLang/App/...` references lowercased; `Build/` and `Debug/` remapped to merged module locations |
| `Documentation/Runtime2/todos.md` | Type-position `App.X.Y` refs lowercased; file paths remapped (incl. merged Debug → `app/modules/debug/`) |

### Property-access references — **deliberately preserved**

Anywhere a doc reads `ctx.App.FileSystem`, `context.App.Debug.Write(...)`, `App.Variables.Set`, `App.Builder.IsEnabled`, `App.Tester`, etc. — these are property access on the `app.@this` instance. Per the convention (now documented in `/CLAUDE.md`), property names stay PascalCase. Not drift.

## Outstanding (non-blocking, flagged for coder)

These are tracked by codeanalyzer's report — recording them here for the merge gate:

1. **S1** — `app/data/Code/` should lowercase to `app/data/code/` (~6 references). One-folder coder fix.
2. **S3** — `environment.run` and `builder.load` are pressure-chosen names. Wants a deliberate naming pass before next release (Ingi's call).
3. ~~`Documentation/v0.2/todos.md:422` providers/ path drift~~ — fixed mid-session after Ingi flagged it: remapped to `PLang/app/modules/builder/code/Default.cs:18` (real post-merge location of `_buildTimer`). Also noted the field is already `private readonly` there, so the underlying multi-App concern at that site is resolved.

## CHANGELOG entry

```
### Changed
- `app/` namespace is now lowercase for PLang vocabulary (`actor`, `goals`,
  `variables`, `channels`, `errors`, `events`, `filesystem`, `formats`,
  `keepalive`, `snapshot`, `tester`, `types`, `config`, `callstack`, `data`).
  C# infrastructure (`Attributes`, `Diagnostics`, `Services`, `Statics`,
  `Utils`) keeps PascalCase. Seven engine concepts merged with their action
  module counterparts under `app/modules/<name>/` — no separate top-level
  folder remains for Cache, Builder, Callback, Settings, Modules, Code,
  Debug. Properties on `app.@this` (`.Cache`, `.Builder`, …) stay PascalCase
  — only the types live in lowercase namespaces.

### Renamed (PLang surface — breaking)
- PLang action `app.run` → `environment.run` (root `app` namespace shadowed
  the action module).
- PLang action `builder.app` → `builder.load` (class-name collision with
  the lowercased root).
```

## Verdict

**PASS.** All gaps filled. Branch is ready to merge into `runtime2`.
