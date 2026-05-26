# Codeanalyzer Report — fix-stepvartypes-incremental v2 (post-merge)

**Re-baseline:** `origin/runtime2` merged into the branch at `434604399`, plus coder fix `be0ebf18a`. `dotnet build PlangConsole` is green (0 errors, 456 warnings — pre-existing).

## Scope

Not a v1-review-response. v1 PASSed and no review was filed. v2 re-validates the eight v1 files against the post-merge state — the merge brought in the full `purge-systemio-from-actions` body of work, which had already cleared codeanalyzer/security/auditor PASS on its source branch (not re-litigated here).

---

## Status of v1 findings

### v1 "branch-wide" finding: pre-existing System.IO.* in `report.cs`

**CLOSED via merge.** All hits at lines 40/41/49–50/53–55/298 are gone. The merge replaced them with `path.@this.Resolve("/.test/results.json", ctx).WriteText(content)` and pure-string suffix math for the JUnit grouping. Exactly the route the v1 report recommended. No further action.

### v1 verdicts on individual files — all still CLEAN

The merge didn't introduce OBP violations or behavioral regressions in any v1-reviewed file. Cost math, event-binding lifetime, `%var%` description, `Timings` collection shape — all intact.

---

## Post-merge re-review of changed files

### PLang/app/modules/test/report.cs

1. **Line 40: `var app = Context.App;` is dead.** Never referenced inside `Run()`. Surviving local from the pre-merge code (`app.AbsolutePath` was the consumer; that line was deleted, the assignment wasn't). Trivially fixable.
   - **Finding type:** simplification / deletion test.
   - **Fix:** delete line 40.

2. **Line 49 / 53: `Resolve("/.test/junit.xml", ctx)` / `Resolve("/.test/results.json", ctx)`** — leading slash is the root-relative form per `path.@this.Resolve` convention. Matches the canonical migration pattern used elsewhere on the merged branch. ✓

3. **Lines 302–308: `LastIndexOfAny(new[] { '/', '\\' })` inside a `GroupBy` selector** — the `new[] { '/', '\\' }` allocates a 2-char array per row. Micro, but easy: extract `private static readonly char[] PathSeparators = { '/', '\\' };` and reuse.
   - **Finding type:** simplification (low priority).

### PLang/app/modules/test/run.cs

1. **Line 163 (coder fix `be0ebf18a`): `step.Goal?.Path?.ToString().TrimStart('/')`**
   - Mechanically correct. `path.@this.ToString()` is overridden (line 172 of `path/this.cs`) to never return null, so the unguarded `.TrimStart('/')` after `.ToString()` is safe. ✓
   - Idiomatic — matches `GoalCall.cs:75` and `cache/wrap.cs:52`.
   - **Verdict: CLEAN.**

2. **Line 106: `goal?.Path?.ToString() ?? goal?.Name ?? "?"`** — same shape as line 163, applied to the coverage site-key. ✓

3. **Line 77: `childApp.Parent = parentApp;`** — new line from merge. Inherits parent scope so child test apps see the parent's actor authorization context. This was the F1 follow-up from purge-systemio-from-actions, already reviewed there.

4. **Line 206: `PrPath = path.@this.Resolve(test.PrPath, childApp.User.Context)`** — `test.PrPath` is still a `string`, resolved into a `path.@this` at use. Correct.

### PLang/app/modules/llm/code/OpenAi.cs

1. **Line 711–722: `ResolveImage` calls `imgPath.ReadAsDataUri().GetAwaiter().GetResult()`** — sync-over-async inside `ToApiMessages`, which itself runs inside async `Query()`. The comment explains "message formatting is sync and the bytes-then-encode pipeline is cheap."
   - This is a known footgun pattern (sync-over-async can deadlock under custom `SynchronizationContext`). Console / ASP.NET-Core / xUnit contexts don't have one, so practical risk here is low.
   - Already accepted on the source branch (`purge-systemio-from-actions`) where it was reviewed.
   - **Not raising as a v2 finding** — re-litigates a decision already made elsewhere.

2. **v1 cost-math, `unknownModelLogged`, `Truncated` exit findings** — all unchanged, all still correct.

### PLang/app/modules/this.cs

1. **`Describe()` is now `async Task<StepActions>`.** Forced by `MarkdownTeaching.Load` becoming async (path verbs are async). Propagates correctly to all call sites the merge updated.

2. **`ResolveMarkdownTeachingRoot()` returns `path.@this?`** instead of `string?`. Cleaner — the value flows directly into `path` consumers without re-resolution. The new comment explains *why* (route through `AuthGate` even when the override points outside app root). Good.

3. **v1 `%var%` slot description fix** — intact at lines 318–324 with its comment.

### Unchanged files (`Run.cs`, `Timing.cs`, `Timings.cs`, `BuildResponse.cs`)

v1 verdicts hold without re-examination.

---

## Summary

| File | v2 verdict | Notes |
|------|------------|-------|
| `PLang/app/modules/builder/BuildResponse.cs` | CLEAN | unchanged since v1 |
| `PLang/app/modules/llm/code/OpenAi.cs` | CLEAN | `ResolveImage` rerouted through `path.ReadAsDataUri()` (merge content, already reviewed on source branch) |
| `PLang/app/modules/test/report.cs` | NEEDS WORK (LOW) | **Dead local `var app = Context.App;` at line 40** + minor `PathSeparators` array allocation in GroupBy |
| `PLang/app/modules/test/run.cs` | CLEAN | coder fix is mechanically correct and idiomatic |
| `PLang/app/modules/this.cs` | CLEAN | `Describe()`/markdown-root went async (merge content) |
| `PLang/app/tester/Run.cs` | CLEAN | unchanged |
| `PLang/app/tester/Timing.cs` | CLEAN | unchanged |
| `PLang/app/tester/Timings.cs` | CLEAN | unchanged |

**Overall verdict: PASS (with one LOW finding).** The merge cleanly resolved the v1 pre-existing System.IO debt. One dead local was left behind in `report.cs` line 40 — trivial to remove. Coder's `.ToString()` fix is correct. No OBP violations introduced.

### One LOW finding for coder pickup

- `PLang/app/modules/test/report.cs:40` — delete `var app = Context.App;` (unused after merge cleanup).
- (Optional) `PLang/app/modules/test/report.cs:302` — hoist `new[] { '/', '\\' }` to a `private static readonly char[]` field.
