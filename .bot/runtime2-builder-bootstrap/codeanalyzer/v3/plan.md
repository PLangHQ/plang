# codeanalyzer v3 plan — runtime2-builder-bootstrap (fresh eyes)

Branch: `runtime2-builder-bootstrap`
Date: 2026-04-28
Trigger: User asked for a fresh look at the branch — not a reaction to coder's fix, not a rerun of v1.

## What "fresh eyes" means here

v1 was a triaged file-by-file pass on ~22 of the highest-risk files (Tier 1 deep, Tier 2 light). v2 only re-checked the 11 files the coder touched in `80200746`. Lots of the branch was never deeply examined: **167 C# files changed**, ~30k insertions.

For v3, I'm setting aside what I already concluded and reapproaching the branch:
- **From above** — pattern sweeps across all 167 files, not file-by-file
- **From below** — deep reads on what v1 explicitly deferred

I will *not* re-derive v1's findings; the verdict on those files stands. v3's job is to find what v1 didn't cover.

## Scope

Two halves:

### Half A — Pattern sweeps (across all changed C# files)

For each pattern, grep the full diff and triage every match:

1. **`catch` discipline** — every `catch (` in changed files. v1+v2 fixed 6 sites; how many remain? Are the survivors filtered or bare? Does any new catch swallow without a filter?
2. **`throw` from `Try*` / `Async*` methods** — methods whose contract is `(value, error)` or `Data` should never throw. Look beyond TypeConverter.
3. **Clone/CreateChild/CopyTo/With family** — find all copy-shaped methods in changed files. Map property-by-property. Audit for divergence (the standing project pattern).
4. **System.IO usage** — CLAUDE.md hard-bans `System.IO.*` outside the FileSystem abstraction. Grep for direct File/Directory/Path usage.
5. **Generic `(T)(object)` casts** — JSON numeric boxing trap (project memory). Any new cast like that?
6. **Static state additions** — new static fields/dictionaries with thread-safety implications.
7. **OBP outside-iteration** — `foreach (var x in obj.Collection)` from outside the owner. Pattern grep on the diff.

### Half B — Files v1 deferred or under-examined

Read in full, run the 5-pass analysis fresh:

1. **`PLang/App/Debug/this.cs`** — 282 changed lines, explicitly deferred in v1. Diagnostic critical path.
2. **`PLang/App/Utils/TypeMapping.cs`** — 708 changed lines (largest single-file change). v1 only flagged the forwarders + generic-list dispatch.
3. **`PLang/App/modules/builder/providers/DefaultBuilderProvider.cs`** — 310 changed lines. v1 fixated on DiagGoal; the rest of the file (LLM call, response binding, save path) was lightly scanned.
4. **`PLang/App/Catalog/this.cs` + Catalog/ExampleRenderer.cs** — read together as the catalog system. v1 spot-checked.
5. **Test infrastructure** — `App/Test/TestFile.cs`, `App/Test/Results.cs`, `modules/test/{discover,run,report,tag}.cs`. Never looked at in v1.
6. **Builder modules I never opened** — `builder/{actions,app,goals,merge,promoteGroups,goalsSave,appSave}.cs`. The builder pipeline, beyond just BuildGoal/validateResponse.
7. **Build/this.cs** + **Actor/this.cs** + **App/this.cs** — root objects that gained properties. Audit constructors, lifecycles.

### What's out of scope

- Files v1 already passed cleanly (Trace/this.cs, ParamSnapshot.cs, PlangTypeAttribute.cs, BuildResponse, ExampleSpec, ActionSpec, TypeEntry, ExampleHelpers, MimeTypes).
- The 100+ leaf-action modules (assert/*, math/*, list/*, mock/*, signing/*, settings/*, variable/*, timer/*, timeout/*, output/*, signing/*, etc.). Pattern sweep will surface anything systemic; per-file analysis would be 60h of work for a low-yield surface.
- `.goal`, `.pr`, `.test.goal`, `.build/` files (per CLAUDE.md, builder is non-deterministic — reviewing .pr files is wasted effort).

## Method

For Half A, I'll use single-pass greps and triage in a table. Findings only when something looks wrong.

For Half B, the standard 5-pass: OBP / Simplifications / Readability / Behavioral reasoning / Deletion test.

## Output

- `v3/result.md` — Half A pattern findings + Half B per-file analyses
- `v3/summary.md`, `v3/verdict.json`, `v3/changes.patch`
- update bot-root `summary.md`, append session to `report.json`

## Time

Significant — Half B alone is 5-7 large files. Estimate 60-90 minutes. If I find a systemic issue in Half A I'll report and stop rather than power through.
