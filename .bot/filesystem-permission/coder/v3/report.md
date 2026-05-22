# Coder v3 — filesystem-permission

## Version
v3 (post-codeanalyzer-v2 — closes the branch)

## What this is

Codeanalyzer v2 (verdict: NEEDS WORK) flagged **two regressions**:

| v2 # | Finding | Status |
|---|---|---|
| 1 | Handler-layer authorize preamble copy-paste in `modules/file/*.cs` (v1 #1 regression) | **Deferred to new branch — plan handed off** |
| 2 | `PLangFileSystem.ValidatePath:227` Linux case-comparison (v1 #4 regression at sibling site) | **Fixed** (commit cad… below) |

## v2 #2 — fixed

Hoisted a single `Path.RootComparison` static helper into `Path.cs` (the main partial), used at both gate sites:

- `PLang/App/FileSystem/Path.Authorize.cs:IsInRoot` — was already OS-aware locally; now reads from the shared helper.
- `PLang/App/FileSystem/Default/PLangFileSystem.cs:227` — was `OrdinalIgnoreCase` unconditionally; now `Path.RootComparison`.

One home for the comparison rule, no drift possible.

Verification: `dotnet build PlangConsole` clean; 2846/2846 C# tests green.

## v2 #1 — deliberately deferred

Ingi's call after discussion: do not collapse the handler preamble on this branch. The real fix is bigger than codeanalyzer's (a) or (b) options — `Path` should be **polymorphic across schemes** (`file://`, `http://`, `s3://`, …) so handlers degenerate to one-liners (`Path.Value!.ReadText()`) and the legacy `IFile.Read/Save/...` surface dies entirely.

**Plan:** `Documentation/v0.2/path-polymorphism-plan.md`. Handed to architect for a **new branch** — not this one.

**Note for codeanalyzer:** v2 #1 is **not forgotten and not punted to a vague follow-up** — it has a written plan with phasing (Phase 1 closes the regression; Phase 2 adds scheme registry + `HttpPath`), explicit open questions for the architect, and a `todos.md` entry pointing back at the plan. The branch closure here is deliberate scope discipline: this branch was about permissions, not about restructuring the `Path` type hierarchy. Phase 1 of the plan will close v2 #1 in its proper home.

## What's left on this branch

Nothing. With v2 #2 fixed and v2 #1 deferred-with-plan, this branch is ready to merge.

Deferred items inherited from v1 (still tracked, not on this branch):
- Scenario 4 (process-restart persistence) — deserialiser bug in `SmallObjectWithParameterizedConstructorConverter`.
- `App.RunAction` deletion (needs source-gen surface for handler `RunAsync`).

These are documented in v1's summary and remain there.

## Commits this session

| Hash | Summary |
|---|---|
| `91a7999` | drop stale Cause references from doc-comments (closes v1 #7/#8 comment debt) |
| `09f73ab` | coder v2 report (v1 closure) |
| *(this session continues)* | v2 #2 fix + plan handoff + this v3 report |

## Verification

- `dotnet build PlangConsole`: 0 errors.
- `dotnet run --project PLang.Tests`: **2846 / 2846 green**.
- PLang test suite: unchanged shape (no source code change to action handlers).
