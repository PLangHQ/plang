# app-lowercase — Plan

## What this is

PLang's C# tree currently splits along two casing conventions without an explicit rule for which is which. `App.Goal`, `App.Step`, `App.Path`, `App.Data` — all PascalCase — sit next to `Builder/`, `Services/`, `Attributes/`, also PascalCase. The reader can't tell from the case whether `Goal` is **PLang vocabulary** (a concept the developer thinks in) or **C# infrastructure** (a host concept the developer never sees). They look identical and they aren't.

This branch fixes that by lowercasing everything that is **PLang vocabulary** and keeping PascalCase for everything that is **C# infrastructure**. After the rename, a reader can tell from the namespace alone which side of the line a type is on — `app.goal.Goal` is vocabulary, `Services.Foo` is infrastructure, no ambiguity.

A second benefit falls out for free: lowercasing `Data`, `Type`, `Path` kills the long-running shadowing battle with `System.Type` and `System.IO.Path`. `PLang.Tests/GlobalUsings.cs` currently routes around this with aliases; the aliases stop being load-bearing.

## What lowercases, what doesn't

**Lowercase (PLang vocabulary):**
- Root: `App` → `app`
- Under `app/`: `Data`, `Type`, `Path`, `Goal`, `Step`, `Variable`, `Actor`, `Error`, `Event`, `Channels`, `Callback`, `Cache`, `Settings`, `Snapshot`, `Tester`, `Code`, `Config`, `Debug`, `FileSystem`, `Formats`, `KeepAlive`, `Info`, `View`, `Types`, `CallStack`
- Top-level: `Builder` → `builder`

**Stays PascalCase (C# infrastructure):**
- `Attributes/`, `Services/`, `Statics/`, `Utils/`, `Diagnostics/`
- `Runtime2/Engine/` and everything below it
- `PLang.Generators/`
- Project files: `PLang.csproj`, `PlangConsole.csproj`, `PLang.Tests.csproj`

The rule: **if a PLang developer would recognise the word, lowercase it. If only a C# contributor would, keep PascalCase.**

## Why phased commits

C# is case-sensitive at the namespace level. `namespace App.X` and `namespace app.X` are different namespaces — the tree must compile after each commit, which means every reference to a renamed symbol moves in the same commit that renames it.

Each phase below is **atomic across all references for the symbols it renames** and leaves the tree green on both test suites (C# TUnit ~2752 tests; PLang `--test` ~203/6-known-fail). Phases are independently bisectable: if a regression surfaces months from now, `git bisect` lands on exactly one phase and points at one slice of the vocabulary.

This is purely cosmetic work. The semantics of every type are unchanged. Anything that is *not* pure rename (e.g. merging two folders, moving code) is **out of scope** and goes on a follow-up branch — keeps bisection clean.

## Phase index

| # | Phase | What moves |
|---|-------|------------|
| 1 | [Root: `App` → `app`](#phase-1--app--app-root) | Root namespace only; sub-vocabulary stays PascalCase one more commit |
| 2 | [`Data` + `Type`](#phase-2--data--data-type--type) | Kills `System.Type` clash; re-points `GlobalUsings` aliases |
| 3 | [`Path`](#phase-3--path--path) | Kills `System.IO.Path` clash |
| 4 | [Vocabulary sweep (4a–4d)](#phase-4--vocabulary-sweep) | 22 subfolders, grouped by blast radius |
| 5 | [`Builder` → `builder`](#phase-5--builder--builder) | The other top-level vocabulary tree |
| 6 | [Sweep + close](#phase-6--sweep--close) | Generator templates, CLAUDE.md proposals, summary |

## Phase 1 — `App` → `app` (root)

Root namespace only. Sub-vocabulary (`Data`, `Goal`, `Step`, …) stays PascalCase for this commit — the intermediate state `app.Goal.Goal` is ugly but valid, and isolating the root rename limits Phase 1's blast radius to "every file in the repo, but only one segment of the namespace path."

Touches:
- 377 files declaring `namespace App.X` → `namespace app.X`
- 306 files with `using App.X` or `global using ... App.X`
- 8 generator files in `PLang.Generators/` with hardcoded `"App.X"` discovery strings
- `PLang.Tests/GlobalUsings.cs` aliases re-point at `app.X`
- `PLang/Runtime2/GlobalUsings.cs` aliases re-point
- `PlangConsole/` references
- Folder rename: `PLang/App/` → `PLang/app/` via two-step `git mv` (`App` → `_App_tmp` → `app`) for case-insensitive FS portability

Green on both suites → commit.

## Phase 2 — `Data` → `data`, `Type` → `type`

Targets the `System.Type` clash directly. After this commit, `PLang.Tests/GlobalUsings.cs` `Data` and `Type` aliases stop being structural — they become legacy convenience aliases that could be dropped later.

Touches the same surface as Phase 1 but only for `Data`/`Type` references. Generator discovery strings: `"app.Data"` → `"app.data"`.

## Phase 3 — `Path` → `path`

Same shape as Phase 2. Kills `System.IO.Path` shadowing in any file that uses both.

## Phase 4 — vocabulary sweep

22 subfolders, grouped into **four commits by blast radius**. Grouping intent: if a regression surfaces, it points you at ~5 folders, not 22. Each group small enough to review in one sitting, large enough that we aren't drowning in commits.

**4a — Execution core** (touched by nearly every handler): `Goal`, `Step`, `Variable`, `Actor`, `CallStack`.

**4b — I/O surface** (the actor→channel→callback chain + data shape on the wire): `Channels`, `Callback`, `FileSystem`, `Formats`, `View`.

**4c — State & lifecycle** (things that persist or pin lifetimes): `Cache`, `Settings`, `Snapshot`, `KeepAlive`, `Types`.

**4d — Control flow & diagnostics** (sideband concerns): `Error`, `Event`, `Code`, `Config`, `Debug`, `Tester`, `Info`.

Each sub-phase: rename folder, rename namespaces, update references, update generator strings if any, green on both suites, commit.

## Phase 5 — `Builder` → `builder`

Builder is a separate top-level tree under `PLang/Builder/`, not under `PLang/App/`. Its own phase because:
- The surface area is large enough to warrant its own bisection point
- Builder's contract with the runtime (it emits `.pr.json` that the runtime consumes) means a regression here breaks the build pipeline, not just runtime execution
- Keeping it separate makes the rule visible: every tree of PLang vocabulary is lowercase, regardless of where it lives in the repo

Same atomic-commit pattern.

## Phase 6 — sweep + close

- Generator emission templates: any remaining capitalized `App.X` strings in code the generator *emits* (vs code it *reads*)
- CLAUDE.md updates: the repo `/CLAUDE.md`, `/PLang/App/CLAUDE.md`, and any other CLAUDE.md referencing `App.X` need proposals filed under `.bot/app-lowercase/claude-md-proposals.md` (per policy — docs bot applies them at merge)
- `summary.md`, commit, push

## Cross-cutting decisions

**Source generator templates are part of every phase**, not a separate concern. The generator's `Discovery/this.cs` reads `"App.modules"`, `"App.Data"`, `"App.Variables"` as string literals to find types in the consumer assembly. Those strings have to move in the same commit as the folder they reference, or the generator silently emits nothing for the renamed type and the consumer doesn't compile.

**Case-insensitive filesystems** (Windows, macOS-default): `git mv App app` is a no-op. Every folder rename uses the two-step `App → _App_tmp → app` pattern. CI on Linux passes regardless; the two-step keeps the commits applicable on other OSes.

**Test gate is the C# suite.** ~2752 tests pass on `runtime2` baseline; any new failure after a rename is a regression. The PLang `--test` suite (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`) should hold at 203 pass / 6 known-fail intentional fixtures.

**Out of scope, on purpose:**
- `Modules/` registry-vs-catalog merge — semantic change, separate branch after this one lands. Keeps bisection meaning "case rename" only.
- Renaming primitives that look like C# keywords (`TString` → `tstring`, etc.) — touched only as part of the natural folder rename, not as a separate sweep.
- Stderr deserializer warning at test discovery — ignored. C# suite is the gate.

## Risks

1. **Generator template drift** — a phase updates folder names but misses a template string. Mitigation: every phase's commit must include the generator changes for that phase's symbols.
2. **`PLang.Tests/GlobalUsings.cs` aliases** — `Data`/`Variables` aliases re-point twice (Phase 1 root, then Phase 2/4 leaf). Verify aliases compile after each step.
3. **Stringly-typed references outside the generator** — reflection, attribute names, JSON discriminators. Coder needs to grep for `"App."` string literals before Phase 1 to find any beyond the generator.

## Order of work

1. Baseline already captured by coder in `.bot/app-lowercase/coder/v1/baseline-tests.md` (2752 C# pass, 203 PLang pass, 6 known-fail).
2. Pre-Phase-1 grep for stringly-typed `"App."` references outside the generator. Add any finds to Phase 1 scope.
3. Execute phases sequentially. Each phase ends in a green build on both suites and a commit.
4. Multi-session work. Each phase is independent enough to pause between.
