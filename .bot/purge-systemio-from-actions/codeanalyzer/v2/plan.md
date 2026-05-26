# codeanalyzer v2 — plan

**Branch:** `purge-systemio-from-actions`
**Trigger:** Ingi asked me to look at coder's recent work since v1.

## What landed since v1

Five coder commits (v1 → HEAD), in landing order:

1. `012b1d74c` — addresses v1 N1/N5 (Json.cs alloc; AppGoals indexing comments).
2. `9160af1ec` — tester v2 hardening (out of my scope; tests).
3. `064724fda` — **security F1**: canonicalize FilePath in ctor + introduce `PathHelper`. Plus PLNG002 carve-out narrowing.
4. `987a5148e` — **security F2**: lift MarkdownTeaching disk reads to `path.@this` verbs.
5. `bfb34bca4` — **auditor F1 follow-up**: `App.Parent` field + parent-chain walk in `IsInRoot` so test child apps inherit parent scope.

## Scope

Review the *delta* (5 commits, ~470/-300 lines under `PLang/` + `PLang.Generators/`). I am NOT re-reviewing v1's accepted clean code. I AM looking at:

- `PLang/app/Utils/PathHelper.cs` (new)
- `PLang/app/types/path/file/this.cs` — `Canonicalize`
- `PLang/app/types/path/file/this.Derivation.cs`, `this.Operations.cs`, `this.Validate.cs` — `PathHelper` routing
- `PLang/app/types/path/this.Authorize.cs` — parent-chain walk
- `PLang/app/this.cs` — `Parent` property + `OsAbsolutePath` PathHelper route
- `PLang/app/modules/MarkdownTeaching.cs` — path-verb lift
- `PLang/app/modules/this.cs` — `ResolveMarkdownTeachingRoot` / async Describe
- `PLang/app/modules/builder/code/Default.cs` + IBuilder + builder/types.cs — async ripple
- `PLang.Generators/Diagnostics/Plng002.cs` — carve-out logic

## Method

Five-pass review on the delta only. Verify v1 findings closed; produce new findings only on code that landed since v1.

## Verdict format

PASS / NEEDS WORK / FAIL via `verdict.json`.
