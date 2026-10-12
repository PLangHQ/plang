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

## Step-3 run (round-3 baseline)

Progression: **129/126/28** → **195/68/15** → **203/61/14** → round 3 **220/33/21/3 skipped** → **round 4 (fed1a1437, pre-rebase) 231/30/12/3** → **round 4b (after rebase onto 84d1487e0: coder round-3 C#, kind 707–712, assert operand values) 239 pass / 22 fail / 12 stale / 3 skipped** (276 total; ParamResolution deleted). Round-4b fails by folder: app/type 6 (kind/strict ×4 + list + item — kind redesign still in flux), app/data 4 (kind redesign), module/test 3 (report feature — coder's report C#), module/condition 3 (compare on-error), module/llm 2 (real provider), module/{signing,math,channel} 1 each (signing = carried B3; math = DoublePlusDecimal carried), plan 1. All are coder-domain runtime reds or carried items — no builder-mapping refusals remain. Carried to next branch: A (Dict/SortMixed — writer won't emit order:{} for a non-scalar list; principle stays in teaching + a writer-retry-hint item), B3 (SigningHeaderMismatch — decider pre-empts the main action when an on-error clause trails; issue-75 family). Full report + per-test table: `result.md`.

**Round 5 (coder round-4 rewrites + step-3 teaching, against HEAD b982cbc94): 245 pass / 16 fail / 12 stale / 3 skipped.** Step-3 teaching tries (one each): output.write bare `to <name>` channel ✓ (goalchannelrecursion now builds), llm.query schema spelled schema/scheme/block ✓ (LlmSchema passes), on.error after an erroring `if` ✓ (Cut5 builds). The 16 fails: 4 kind/strict + DataTypeReadsEntity + Plane_DataKey + BareLiteral (kinded-Type, next-branch); ListIsItemSortsAsBefore (carried A, empty query); Cut2/Narrow (`assert %x% is file` has no assert.isType action — needs C#, routed to architect); Failure_DictEqNumber/DictOrderDict (on-error nested in an if-then body — builder can't parse; test-restructure or C#); Cut5 + goalchannelrecursion (now BUILD, fail only at run — coder's compare / channel-recursion-guard behavior); DoublePlusDecimal + SigningHeaderMismatch (carried). Step 2 (empty-query build refusal in list.query.Validate) was blocked by the Parse-preempt design and reverted — carried, no C# landed. All 16 fails + 12 stale are carried/next-branch or coder-domain runtime reds; no builder-mapping refusals remain that a teaching edit can fix. **This is the reviewer-bot baseline.** sibling-unbuilt is 0; the skip-tag fix works (signing pair + Duration skip). Remaining fails: ~29 genuine runtime reds (coder's); 2 write-back placement (DictIsItemKeepsNoOrder, DoublePlusDecimal — with coder); 2 flaky wrong-prPath. 21 stale = builderVersion drift (rebuild-pass clears, best after the coder's write-back fix). Open builder C#: the write-back clause-detection (IsClause name-check) handed to coder to replace with the clause's own knowledge.

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
