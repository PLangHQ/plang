# app-lowercase — Plan (v1)

## Goal

Lowercase the PLang vocabulary classes/namespaces in C#:
- `App` → `app` (root namespace + folder)
- `Data` → `data`, `Type` → `type` (kills `System.Type` clash)
- `Path` → `path` (kills `System.IO.Path` clash)
- `Goal` → `goal`, `Step` → `step`, `Variable` → `variable`, `Actor` → `actor`, `Error` → `error`, `Event` → `event`, plus the rest of the App/* vocabulary tree (Channels, Callback, Cache, Settings, Snapshot, Tester, Code, Config, Debug, FileSystem, Formats, KeepAlive, Info, View, Types, CallStack)
- Merge `App/Modules/` (registry) into `app/modules/` (catalog) — they were never two things, casing accident.

Infrastructure stays PascalCase: `Attributes/`, `Services/`, `Statics/`, `Utils/`, `Diagnostics/`, `Builder/`, `Runtime2/Engine/`, `Runtime2/Engine/Context/`, `PLang.Generators/`.

## Scope (counted)

- 358 C# files under `PLang/App/`
- 377 files declaring `namespace App.X` somewhere in the repo
- 306 files with `using App.X` / `global using ... App.X`
- 8 generator files in `PLang.Generators/` with hardcoded `"App.X"` discovery strings and emission templates

Plus: `PLang.Tests/` aliases, `PlangConsole/` references, `PLang/Runtime2/GlobalUsings.cs`.

## Strategy — one branch, phased commits

C# is case-sensitive: `namespace App.X` and `namespace app.X` are different. Any phase must leave the tree compiling, so each phase is atomic across all references for the symbol it renames.

**Phase 1 — `App` → `app` root namespace only.**
- Rewrite every `namespace App` → `namespace app`, every `using App.` → `using app.`, every `global::App.` → `global::app.`, every `App.X` qualified reference → `app.X`.
- Update source generator: `Discovery/this.cs` string literals (`"App.modules"`, `"App.Data"`, `"App.Variables"`) and `Emission/Action/this.cs` template strings.
- Folder rename: `PLang/App/` → `PLang/app/` via `git mv` (two-step for case-insensitive FS safety: `App` → `_App_tmp` → `app`).
- Sub-vocabulary still PascalCase (`Data`, `Type`, `Path`, `Goal`...). Just the root lowercased.
- Build green, both test suites green.
- Commit.

**Phase 2 — `Data` → `data` and `Type` → `type`.**
- Rewrite `App.Data` → `app.data`, `Data.@this` references to use new path, etc.
- Folder rename: `app/Data/` → `app/data/`.
- Source generator references update (`"App.Data"` already updated in Phase 1; now becomes `"app.data"`).
- `PLang.Tests/GlobalUsings.cs` `Data` / `Type` aliases re-point.
- Build + tests.
- Commit.

**Phase 3 — `Path` → `path`.**
- Same pattern. Kills the `System.IO.Path` clash.

**Phase 4 — remaining vocabulary subfolders.**
- `Goal` → `goal`, `Step` → `step`, `Variable` → `variable`, `Actor` → `actor`, `Error` → `error`, `Event` → `event`, `Channels` → `channels`, `Callback` → `callback`, `Cache` → `cache`, `Settings` → `settings`, `Snapshot` → `snapshot`, `Tester` → `tester`, `Code` → `code`, `Config` → `config`, `Debug` → `debug`, `FileSystem` → `filesystem`, `Formats` → `formats`, `KeepAlive` → `keepalive`, `Info` → `info`, `View` → `view`, `Types` → `types`, `CallStack` → `callstack`.
- Can be one commit per subfolder, or grouped into 3-4 commits by area. Prefer grouped for fewer commits but still bisectable.

**Phase 5 — `Modules` registry merge.**
- `app/Modules/this.cs` (registry) moves into `app/modules/this.cs`.
- `app/Modules/Schema/` moves into `app/modules/Schema/` (Schema stays PascalCase — it's internal infra of the registry).
- Update all `app.Modules` call sites → `app.modules`.
- Delete the old `app/Modules/` folder.
- Build + tests.
- Commit.

**Phase 6 — sweep + close.**
- `update-templates` in generator templates: any remaining capitalized references in emitted code.
- CLAUDE.md proposals (the existing `App.X` references in `/CLAUDE.md` and `/PLang/App/CLAUDE.md` need updating — but per policy, file a proposal in `.bot/app-lowercase/claude-md-proposals.md`).
- `summary.md`, commit, push.

## Baseline (recorded in `baseline-tests.md`)

Need to capture before Phase 1 starts:
- C# test suite: pass/fail counts
- PLang test suite: pass/fail counts
- Clean build green

## Risks

1. **Source generator out of sync.** Emission templates hardcode `global::App.X` strings. If a phase updates folder names but forgets a template, generated code references the old namespace. Mitigation: update generator templates in the same commit as the folder rename.
2. **`PLang.Tests/GlobalUsings.cs` aliases.** Already complex (`Data`, `Variables`). Aliases re-point to new namespaces — should work, but verify.
3. **Case-insensitive filesystem.** WSL2/ext4 case-sensitive — fine for me. Two-step `git mv` (`App` → `_App_tmp` → `app`) makes the rename portable.
4. **Hidden string-typed references.** Reflection, attribute names, JSON discriminators, `.pr.json` files. Ingi confirmed `.pr.json` doesn't carry C# type names. Still need to grep for stringly-typed `"App."` references beyond the generator.

## Out of scope

- Builder (`Builder/`) — stays PascalCase per Ingi's call.
- Project files (`PLang.csproj` etc.) — stay PascalCase.
- Renaming primitives that are C# keywords (no `tstring` rename — `TString` already follows the dialect convention; just lowercasing it to `tstring` is part of Phase 2 sweep).

## Order of work this session

1. Capture baseline (C# + PLang test counts).
2. Get Ingi's nod on phase strategy.
3. Execute Phase 1 (`App` → `app` root).
4. Commit if green.
5. Continue through phases as time permits.

This is multi-session work. Each phase is committed independently so progress survives session breaks.
