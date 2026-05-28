# Rename `file` module to `path`

## Why

The `file` module name was accurate when the abstraction only handled local disk. It isn't accurate anymore. `path.@this` (with `FilePath`, `HttpPath`, and eventual `S3Path`/`FtpPath` scheme variants) made the underlying type protocol-agnostic — but the developer-facing module name didn't follow. Today `file.read https://example.com/data` works, and that fact is hidden by the module name. A new PLang developer reading the action catalog sees "file" and assumes disk-only. They never discover the HTTP capability, and they won't discover S3/FTP when those land.

The rename signals the scope of the abstraction at the place developers first meet it — the action catalog. It also restores PLang's noun-module convention (module name matches the type it operates on): the type is `path.@this`, so the module is `path`.

This conversation already settled the deferred questions. See `## What's NOT in scope` below.

## Note to coder

The directory layout, command order, and find/replace patterns in `stage-1-rename.md` are **suggestions, not contracts**. You own the final shape. If a tighter sweep pattern catches more references, use it. If a different command order builds cleanly faster, take it. The non-negotiable outcomes are listed under "Deliverables" — everything else is yours.

## What's in scope

1. **C# folder + namespace rename**: `PLang/app/modules/file/` → `PLang/app/modules/path/`. All seven handler files (`copy.cs`, `delete.cs`, `exists.cs`, `list.cs`, `move.cs`, `read.cs`, `save.cs`) get `namespace app.modules.file` → `namespace app.modules.path`.
2. **One concrete consumer**: `PLang/app/goals/goal/GoalCall.cs:123` uses `new modules.file.Read { ... }` — updated to `new modules.path.Read`.
3. **One stale comment**: `PLang/app/modules/code/this.cs:251` references `modules.file.code.IFile` (already-removed code). Update or delete.
4. **Markdown teaching folder rename**: `os/system/modules/file/` → `os/system/modules/path/`. Update `module.description.md` to reflect protocol-agnostic framing. Sweep `*.examples.md` / `*.description.md` for `file.<action>` → `path.<action>`.
5. **`.goal` source sweep in Tests/**: 44 files reference `file.<action>` literals — every match gets `file.X` → `path.X`. Also covers the prose-level case `- before action file.read call ...` in `Tests/Modules/Event/Override/Start.goal`.
6. **`.pr.json` regeneration**: do not edit by hand. After source edits, run `plang build` so every `.pr` file emits `"module": "path"` from the new catalog.
7. **Documentation sweep**: `Documentation/v0.2/*.md` (12 files reference `file.<action>`), `os/system/actions/v2/summary.md`, `os/system/modules/http/download.description.md`, `os/system/modules/variable/set.notes.md`, `os/system/builder/BuildGoal/LlmFixer.goal`.
8. **Test verification**: clean rebuild + `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`, and `dotnet run --project PLang.Tests`. Both must pass.

## What's NOT in scope

Settled in conversation, deferred to a later branch:

- **HTTP-specific configuration on `path.*` actions** (headers, auth, custom method). No real-world pain has surfaced yet; designing for it blind is a guess. The escape hatch (`http.request`) already exists.
- **Collapsing `http.upload` and `http.download` into `path.copy`**. They overlap cross-scheme but the collapse needs a verification pass on whether `path.copy %url%, to %file%` actually works end-to-end. Park it; revisit when someone trips on the redundancy.
- **Response metadata via `Data.Properties`** (status, headers from HTTP reads). Hypothetical until needed.
- **Default HTTP write verb for `path.save`** (POST vs PUT). No HTTP write semantics are changing on this branch.
- **The `http` module** stays untouched. Same actions, same names, same shape.
- **The `app.types.path.file` namespace** (the `FilePath` *type* under `path.@this`) stays. That's the scheme variant of the type, not the module. Do not touch `PLang/app/types/path/file/`.

## Stage index

Single stage. The rename is mechanical and the pieces are tightly coupled — splitting would create artificial seams.

| Stage | File | Status |
|-------|------|--------|
| 1 | [Rename](stage-1-rename.md) | pending |

## Cross-cutting decisions

**Module-name discovery is namespace-driven.** The source generator reads `namespace app.modules.<name>` to derive the module name in the action catalog. Renaming the namespace renames the catalog entry; no separate registration step.

**No backwards compatibility shim.** No `file.X` alias, no deprecation period. The branch is pre-1.0 and the rename is the whole point — a shim would defeat it. A `.goal` author who lands on `file.read` after the merge gets a clean "unknown action" failure, which is correct.

**Build order matters.** C# rename + namespace updates first, then `dotnet build PlangConsole` (catches namespace bugs immediately). Then `.goal` source sweep. Then `plang build` to regenerate `.pr` files from the new catalog. Then test runs. Doing this out of order produces noisy failures that mask real bugs.

**Stale-binary trap.** `plang --test` uses a pre-built executable. After the C# rename, the binary must be rebuilt from clean (`rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj PLang.Tests/bin PLang.Tests/obj PLang.Generators/bin PLang.Generators/obj && dotnet build PlangConsole`) before any `plang --test` run. Skipping this produces phantom failures like "Action 'path.read' not found" that aren't real.

## Open questions

None. Conversation settled the scope. If something surfaces during implementation that wasn't anticipated, the coder should write it up, not invent a design decision mid-sweep.
