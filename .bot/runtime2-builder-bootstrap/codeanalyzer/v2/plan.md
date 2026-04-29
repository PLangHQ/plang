# codeanalyzer v2 plan — runtime2-builder-bootstrap

Branch: `runtime2-builder-bootstrap`
Date: 2026-04-28
Trigger: Coder pushed `80200746` ("Address codeanalyzer v1 findings"). User asked me to re-review.

## Scope

Verify v1 fixes were applied correctly AND the fixes themselves don't introduce new defects. **Pass 4 behavioral reasoning is the primary lens** — every fix is itself new code in already-touched files.

NOT in scope: re-reviewing untouched files from v1 or expanding to files I deferred. v1's verdict on those stands.

## Files in commit `80200746`

| File                                                          | v1 finding(s) addressed |
|---------------------------------------------------------------|-------------------------|
| `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` | #1 (DiagGoal delete) |
| `PLang/App/Utils/TypeConverter.cs`                              | #2 (throws→Error), #3 (bare-catch) |
| `PLang/App/Variables/this.cs`                                   | #3 (bare-catch + log) |
| `PLang/App/modules/ui/providers/FluidProvider.cs`               | #3 (bare-catch) |
| `PLang/App/Errors/Error.cs`                                     | #3 (bare-catch) |
| `PLang.Generators/LazyParamsGenerator.cs`                       | #3 (generated-catch filter) |
| `PLang/App/Data/this.cs`                                        | #4 (key-name → CLR identity) |
| `PLang/App/Actor/Context/this.cs`                               | #6 (doc as test fixture) |
| `PLang/App/Utils/PlangTypeIndex.cs`                             | #7 (Reset removed, volatile) |
| `PLang/App/modules/error/handle.cs`                             | #8 (symmetric recovery return) |
| `Documentation/Runtime2/todos.md`                               | test-gap follow-up |

Deferred (own pass): #9 three-renderer consolidation, #10 culture-sensitive ToString.

## Method

For each fix, two checks:

1. **Did the fix correctly close the v1 finding?** — read the new code; confirm the original concern is addressed, not just paved over.
2. **Pass 4 — what could the fix break silently?** — the fix is new code. Does it introduce a regression, an under-broad/over-broad behavior change, or a fresh anti-pattern?

Then a cross-cutting check:
- Anything in the v1 priority list that the coder *thought* they fixed but actually didn't?
- Any of v1's lower-priority sub-findings re-surfaced by the new code?

## Specific things I'm watching for

- **Fix #2 (TypeConverter throws→Error)**: trace callers of `TryConvertTo` to confirm none rely on the `InvalidOperationException` propagating as a control-flow signal.
- **Fix #3 catches**: the filter pattern is `not (NullRef|OOM|StackOverflow)` in some sites and `JsonException|NotSupportedException[|ArgumentException]` in others. Are both shapes appropriate at each site? Is `ArgumentException` an over-catch?
- **Fix #4 (CLR identity)**: the new check matches `IEnumerable<Action.@this>.IsAssignableFrom(clr)` — broader than the old name match. Does this widen the deferred set in a way that breaks any non-template Action collection?
- **Fix #5 (Variables.Set log)**: `_ = _context?.App?.Debug?.Write(...)` — fire-and-forget Task. Acceptable for diagnostic, but worth confirming Debug.Write has no synchronous-throw path that would itself bubble.
- **Fix #7 (volatile)**: confirm .NET memory model semantics — does volatile-write release on the flag actually fence the HashSet writes? (Yes, in CLR — volatile writes have release semantics — but worth stating.)
- **Fix #8 (symmetric recovery)**: confirm the test-gap todo really is a test gap, not a behavior gap. What does `recoveryResult.Value` look like when `Actions` has multiple actions?

## Output

- `v2/result.md` — per-fix verdict, regression notes
- `v2/summary.md` — narrative
- `v2/verdict.json` — pass/fail
- `v2/changes.patch` — `git diff runtime2..HEAD -- ':(exclude).bot'`
- update bot-root `summary.md`
- update `.bot/runtime2-builder-bootstrap/report.json`

## Time estimate

Smaller scope than v1 (12 files, all small diffs). 30-45 minutes including writeups.
