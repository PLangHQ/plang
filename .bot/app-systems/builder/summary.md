# builder — app-systems summary

**What this is:** the builder's pass over the kept test tree (decision 671 cleanup) — build every kept `.test.goal`, fix builder mapping bugs the builds surfaced, run the suite, report. Plus mid-branch fixes (issue 71, on.error, channel events, condition, issue-75) relayed through the architect.

## What was done

Built the kept tree folder-by-folder (batches, start/end pings to architect). Built (committed): app/data, module/variable, task, channel, callback, loop, condition, ui, build, debug, test, small-app folders, signing (6/8), identity (8/8), llm (4/6), app/type (35/38), stragglers.

Builder fixes landed (each with decider-fixture + page-golden re-pins):
- **Issue 71** — `type=json` → Type=json text (teaching, set.notes/examples); **inverse-Cover** reads the step's quoted literals decoded, not raw (`PLang/app/goal/step/this.Validate.cs:82`), so an escaped-quote json literal is held.
- **on.error** — recovery nests in Recovery (teaching); **on.error-does-nothing structural check** (`PLang/app/module/on/error.cs` `OnError.Validate`) refuses an empty clause — kills the hoisted-recovery wrong-save (red-first MatchTests pin, mutation-checked).
- **channel events** — `%!channel%` shortcut (decision 500) in `os/system/modules/on/event.*`.
- **condition** — `and`=nested ifs, `or`=if+elseif, `if <file> exists … else …`, `!%b%`→`==false` (if.notes/examples).
- **issue-75** refusal wording (`formal/reader.cs`); `as list of <type>` + llm `maxTokens`→Limit.token teaching.

## Step-3 run (final)

Progression across rebuild passes: baseline **129/126/28** → sibling+stale+wrong-prPath **195/68/15** → coder-rewrites+wrong-saves+E1 **203/61/14** (full report + per-test table: `result.md`). 3 more wrong-prPath fixed after the final run (→ ~206/58 next run).

Rebuild passes done (siblings — now 0 unbuilt; stale/wrong-prPath rebuilt from test/; coder-triage wrong-saves + 39 coder stale-rewrites built; E1 sort). The remaining 61 fails are genuine RUNTIME reds (build clean, fail at run from the session's coder runtime changes — kind-redesign, signing, on.error) = coder's domain, matching plang-triage.md. The 14 stale are the known next-branch build items + 3 snapshot-by-design.

The baseline is ready for the reviewer bots. Builder's build-side work on the kept tree is done.

## Left unbuilt by reason (next branch)

issue 74 (`%s%.length%` nav mangle: TextLengthOp, MissingVarIsNotNullValue, ThrowAttachesData); kinded Type choices (TypedListHoldsItsType — 10b closes on C# pins); llm Limit-padding (LlmProperties); issue 75 (signing RoundTrip + corruptedsignature). Deleted: DownloadSkip (stale, http.download has no IfExists).

## Code example

The on.error structural check (behaviour on the element, reads presence not words):
```csharp
// OnError.Validate — an on.error must retry, recover, or carry on
var recovery = Recovery == null ? null : await Recovery.Value();
bool hasRecovery = recovery is { CountRaw: > 0 };
var retries = RetryCount == null ? null : await RetryCount.Value();
bool hasRetry = retries != null && retries.ToInt32() > 0;
if (!hasRecovery && !hasRetry && !await Ignore.ToBooleanAsync())
    return new Error("on.error does nothing: its recovery goes inside Recovery=[...], not as an action after it", "OnErrorDoesNothing", 400);
```
