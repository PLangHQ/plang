# codeanalyzer v1 — filesystem-permission

## Scope

Coder landed all 5 stages of the filesystem-permission branch
(2830/2830 tests pass). Diff vs `runtime2`:

- 129 files changed, +6195 / -1822
- 43 C# files under `PLang/` touched (28 modified, 11 added, 4 deleted)

## Approach

Five passes per the character file.

### Pass 1 — OBP

1a (rules) + 1b (shape smells) on every new/modified file under `PLang/App/`.
Highest-risk files for OBP shape smells (collections, locks, dual storage):

- `PLang/App/FileSystem/Permission/this.cs` + `Verb/*` (new family)
- `PLang/App/Actor/Permission/this.cs` (Find/Add/Revoke surface)
- `PLang/App/FileSystem/Path.Authorize.cs` + `Path.Operations.cs`
- `PLang/App/Snapshot/this.Resume.cs` (cross-boundary continuation)
- `PLang/App/Data/this.Snapshot.cs` + `ShouldExit.cs`
- `PLang/App/Goals/Goal/this.RunFrom.cs` + `Step/this.RunFrom.cs`
- `PLang/App/Channels/Channel/*` (Ask signature change)
- `PLang/App/modules/output/ask.cs` + `modules/callback/run.cs`
- `PLang/App/Errors/PermissionDenied.cs`
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs`
  (PreboundHandler, DispatchAsync, Synthetic)

### Pass 2 — Simplification

Dead abstractions, redundant null checks, copy-paste, premature generalization.
Particular attention to:

- The `App.Run` / `App.RunAction` cleanup — coder reports it stayed as a thin
  redirect; check if it earns its place.
- `PreboundHandler` property on Action — what's the actual reuse pattern?
- Old callback files deleted — are there orphan call sites or aliases left?

### Pass 3 — Readability

Naming, method length, flow clarity. The Snapshot.Resume recursion and the
ask round-trip are the parts a fresh reader will hit first.

### Pass 4 — Behavioral

Trace data origins through Snapshot.Resume, PreboundHandler, Authorize. Look
for catch sites that swallow `IExitsGoal`, generic catches that mask
`PermissionDenied`, copy/clone methods that need to carry Snapshot.

### Pass 5 — Deletion

Anything left over from the deferred work — v1 surface, App.RunAction,
Cause field, `!ask` sentinel — that could be deleted now.

## Output

- `v1/report.md` — findings per file, OBP/Simplification/Readability sections
- `v1/verdict.json` — pass/fail + one-line summary
- `summary.md` (overwrite) — version, what this is, what was done

## Reminders to self

- Do **not** edit source files. Report only.
- The four-item shape-smell checklist is mandatory per file in scope.
- Save findings with exact `file:line` cites so the coder can act on them.
- Note any tests or fixtures that would be made redundant by the findings.
