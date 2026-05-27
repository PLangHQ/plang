# Auditor v1 — fix-stepvartypes-incremental

## Verdict: PASS

Three reviewers said yes; I looked at the seams between them. The codeanalyzer v3 HIGH closure (cargo `TrimStart` drop) landed clean. The two late commits that arrived **after** codeanalyzer v3's last full read — `1b1b226bb` (File.cs slim) and `463339c90` (Step prop drop) — are consistent across all C# consumers, PLang `.goal` sources, and serializer round-trips. No new critical/major findings.

## What I verified beyond what the bots covered

### 1. File.cs slim — every consumer migrated (tester said "grep is clean", I re-grepped)

`PLang/app/tester/File.cs` dropped 6 mirrored properties (`Path`, `PrPath`, `EntryGoalName`, `Directory`, `GoalHash`, `BuilderVersion`) and the `[PlangType("testfile")]`/`[LlmBuilder]` attribute facade. Only `Goal` (required), `Status`, `StatusReason`, `Tags` remain.

Consumers now correctly read through `file.Goal.*`:
- `test/report.cs:107,109,263` — `run.File.Goal.BuilderVersion`, `run.File.Goal.Hash` ✓
- `test/report.cs:111` — `run.File.Goal.Path` ✓
- `test/run.cs:163` — `test.Goal.Path` ✓
- `PLang.Tests/App/Testing/TestMetadataTests.cs:65` — `run.File.Goal.BuilderVersion` ✓

Zero stale `file.Path` / `file.PrPath` / `file.EntryGoalName` / `file.Directory` / `file.GoalHash` / `file.BuilderVersion` references in production C# or test code (grep clean). The `[PlangType("testfile")]` removal is also consistent — `File` is no longer a PLang-vocabulary type, only an internal discovery record. `tester/Run.cs` still references `File` directly (correct — Run owns a File reference).

### 2. discover.cs rewrite — null-safety preserved across all 6 paths

Every path now constructs a `File { Goal = ... }` with `required Goal`:
- L82-91 (goal read fail) → `new Goal { Path = goalFile }` — Goal.Path set, Goal.Hash null, comparison with prGoal.Hash falls through to rebuild logic on subsequent reach (but this path returns early). ✓
- L94 fallback chain `goalRead.Value as Goal ?? Goal.Parse(...) ?? new Goal { Path = goalFile }` — three layers, last is non-null. ✓
- L102-106 ("no PrPath derivable") — **likely dead code in practice**: `Goal.PrPath` (this.cs:104-117) always derives from non-null `Path`, and Path is set in every prior construction. Reachable only if a deserializer produced a Goal with Path == null AND non-FilePath. Not a bug, just unreachable. *Same untestability tester flagged as "edge-case theoretical" — agree, non-blocking.*
- L111, L122, L131 (no .pr / pr read fail / pr corrupt) — use sourceGoal ✓
- L143 (hash mismatch) — sourceGoal.Hash null is fine because `string.Equals(null, prGoal.Hash, …)` returns false → "rebuild needed". This was the original semantic too. ✓
- L160 (happy path) — uses `prGoal`, the deserialized typed Goal ✓

### 3. condition/code/Default.cs `EvaluateOperator` extract — pure refactor

Three formerly-async bodies collapsed to one shared async helper plus three expression-bodied forwarders. Side-by-side check against `0943e5fda`'s pre-image:
- Null/Success guard preserved (`!operatorData.Success || operatorData.Value == null`) ✓
- `try` body preserved (await + Ok wrap) ✓
- `catch` filter unchanged (`ArgumentException | OverflowException | InvalidCastException`) ✓
- `EvaluationError` helper untouched ✓

`Evaluate(If/Elseif/Compare)` are now non-async returning Task — equivalent to the prior `async Task<...>` from the caller's perspective. Interface contract preserved.

### 4. Step prop drop — no straggler references

`step.@this.Guidance/Level/Confidence` removed alongside MergeFrom backfill and `enrichResponse` keep:true backfill. Grep across `PLang/`, `PLang.Tests/`, `os/system/builder/` (excluding `.build/.pr`): zero stale references. Existing `.pr` JSON with `"guidance": null` etc. deserialize silently via STJ "ignore unknown" — verified by tester v6 against `testdiscoverhandlesicelandicgoalnames.test.pr`. ✓

### 5. test/run.cs additions (Output capture + per-step Timings) — concurrency safe

- `outputBuf` is local to `RunSingleAsync`; each test run has its own; cross-test isolation guaranteed.
- BeforeWrite filtered to `Output` channel only (skips `Debug`, `Error`) — verified the channel name match (`app.channels.@this.Output`).
- `testRun.Output = outputBuf.ToString()` snapshot happens in `finally` *before* `childApp` async-disposes. Late writes after disposal cannot mutate the captured string.
- Per-channel write serialization within a single childApp (security confirmed) means StringBuilder.Append isn't racing.

### 6. Cross-reviewer concord

| Reviewer | Verdict | My take |
|---|---|---|
| codeanalyzer v3 | FAIL → closed by `0943e5fda` | Agree — HIGH (cargo TrimStart) genuinely fixed, helper extract is clean. Late File.cs slim arrived after this review but doesn't introduce new OBP smells. |
| tester v6 | PASS, 208/208 + 3036/3036, with one minor (4-of-6 Stale branches untested) | Agree — and the slim restructure didn't make this worse. Two of the four are theoretically reachable (goal read fail, pr corrupt JSON); two are essentially dead (no PrPath derivable, pr read fail on a file we just confirmed exists). |
| security v1 | PASS | Agree — `ReferenceHandler.IgnoreCycles` is acknowledged tradeoff for results.json, standing-Medium Variables-snapshot track unchanged on this branch. |

## Non-blocking observation (not gating)

**"no PrPath derivable" branch in discover.cs is effectively unreachable for FilePath sources.** `Goal.PrPath` returns null only when `Path == null` or `Path.Absolute` is empty — but every construction path through discover.cs sets `Path = goalFile`. The check is defensive against a future refactor where `Goal.PrPath` might return null for an HttpPath-typed Goal. Leave it — the cost is one branch, the benefit is no NullReferenceException if Path typing changes.

## Files reviewed

- `PLang/app/tester/File.cs`
- `PLang/app/modules/test/discover.cs`
- `PLang/app/modules/test/run.cs`
- `PLang/app/modules/test/report.cs`
- `PLang/app/modules/condition/code/Default.cs`
- `PLang/app/goals/goal/this.cs` (Goal contract surface)
- `PLang/app/goals/goal/steps/step/this.cs`
- `PLang/app/modules/builder/code/Default.cs` (enrichResponse delta)
- `PLang/app/formats/this.cs` (MIME registrations)
- `PLang.Tests/App/Testing/DiscoverActionTests.cs`, `RunActionTests.cs`, `TestMetadataTests.cs`

Plus grep across all of PLang/, PLang.Tests/, os/system/ for the dropped property/attribute names.
