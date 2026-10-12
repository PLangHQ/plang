# app-systems — plang test reds, by root cause (no fixes yet)

Run: fresh PlangConsole at 8e44fedac + builder commits to 566ddf45e, `cd test && plang --test={"exclude":["llm","network"],"timeout":"60s","parallel":4}`.
**127 pass, 123 fail, 28 stale, 5 skipped** (llm/network excluded). Per-test first lines: `test/.test/results.json` (not committed).

Verdicts: **builder** = the .pr is wrong or missing (rebuild/teaching, not a runtime change) · **stale** = the test asserts a
shape a decision changed (rewrite or delete) · **runtime** = the runtime is wrong or a behaviour was never finished.

## A. builder — the goals a test calls were never built (36 `GoalNotBuilt` + fixtures)
"Goal …/X.goal exists but isn't built". The builder's class 1. Also every fixture under module/test (`_fixtures_pass`,
`_fixtures_fail`, `_fixtures_sensitive`, `run/_isolation`, `run/_timeout`, `run/_fail`, `assert/_fixtures`,
`edgecase/_fixtures`) has no `.build` — so these fail as a consequence, not on their own:
module/test/report ×5 (summaryFail/summaryPass ≠ 1: the fixture never ran), module/test/run Isolates +
ReportsAssertionFailure, module/test/assert SnapshotsVariables.

## B. builder — 13 `.pr` built from the repo root, not `test/`
Each records `"/test/app/…/x.test.goal"` and `"/test/…/.build/x.pr"`; under the `test/` root that path doesn't exist →
"Not found: /test/…/.build/x.pr". Rebuild from `test/`. IfBoolTruthy, ChannelIndexMissThrows, AsJson_ResolvesTypeUnknown,
Cut2, NavigationOnTypeUnknown_AsksForAsType, NullsSortLast, Failure_DictOrderNumber, ConditionCompoundOr,
ConditionFileNotExists, IdentityExport, ForeachDictionary, SigningExpired, SigningHeaderMismatch,
TestConditionElseIfMatchesRecordsBranchIndex1 (the last also stale, see D3).

## C. builder — wrong saves: a step's clause dropped or invented (the issue-75 family)
| test | step | saved as |
|---|---|---|
| module/on/error ×6 (GoalFirstReturnsRecoveryValue, MultiActionRecoveryLastActionPropagates, OnErrorRetry, PerActionErrorScope, RetryFirstReturnsRecoveryValue, retry/Retry) | `throw …, on error set …` | `on.error` with no Recovery, the recovery hoisted as a sibling action. Built in f4e2cf34e, **before** 7bdfbd105 (the structural check that now refuses it) — rebuild. |
| condition/compare Cut5, Failure_DictEqNumber, Failure_DictOrderDict | `if … then\n on error call Caught` | `condition.if` only — the on error dropped; the EvaluationError itself is right |
| condition/operator/not ConditionNot | `if not %flag%, call WhenFalse` | `condition.if(Left)` — both `not` and the call dropped |
| channel/writetounknownchannel, channel/goalchannelrecursion | `write 'hi' to dbg / to echo, on error …` | `output.write(Data)` — the channel dropped, so it writes to the default and nothing errors |
| app/actor/switch ActorSwitch | `write out "…" actor="system"` | `output.write(channel="system")` → ChannelNotFound |
| app/type/dict DictIsItemKeepsNoOrder | `sort %people%, on error call Caught` | `order: "name"` invented — the sort succeeds, nothing is caught |
| app/data/Plane_DataKeyVsProperty | `type=json` | Type json → "Unknown type 'json'" — built before issue 71's teaching (json is a text of kind json) |

## D. stale — the test asserts a shape a decision changed
1. **`.` navigates the value, `!` the facts about it (decision 572)** — ~25 tests read a value's type as `%x.Type.Name%`:
   a text refuses (`CantNavigateText`), a number answers nothing. Today's door is `%x!type.name%` (probed: "text",
   "number"). app/type bool ×2, date ×3, datetime ×2, item/BareLiteralJudgedByForm, kind/strict ×9, null, text ×2,
   time ×2; app/data ReadConfigJson_UntouchedIsJsonString + ReadCsv_LandsAsTable (`.Type.Name` = "file"); and the same
   family: path PhotoPathExists ×2 (`%photo.Path.Exists%`), app/data Narrow_ChainWideBangBothBranches
   (`%config!file!path%`, 568's form that 572 superseded). Rewrite each to `!` and rebuild; a test may then show a
   real answer under it (DataTypeReadsEntity asks `%name!Type%` equals "text/plain" — the type is {text, plain}; how
   a type compares to that text needs a look after the rewrite).
2. **condition.if publishes no `branchIndex`** — removed by design in 72d72f438 (condition.if evaluate-only; coverage
   derives the branch). module/test/condition IfRecordsBranchIndex True/Else (+ ElseIf…Index1). Delete, or rewrite
   against the coverage the report derives.
3. **`timeoutSeconds` is `timeout`, a duration** (f845b2c8a). module/test/run TestRunEnforcesTimeout
   (`%!app.test.setting.timeoutSeconds%` → CannotSetChild). Rewrite to `timeout = 1s`.
4. **A test's members are its `[Out]` ones** — `Goal` isn't one, `Name` is. module/test/edgecase
   TestDiscoverHandlesIcelandicGoalNames (`where Goal.Name equals 'Próf'`). Rewrite to `Name` (its fixture is unbuilt too).
5. **`cache` is a choice, not a bool** — module/setting/settingscrud sets `%!llm.setting.cache% = false` →
   "choice<cache> cannot be created from a bool". Rewrite to a cache option (needs the decision that made it a choice).
6. **The old test tree** — module/build/compilellmnotes ×2 read `../../Simple/.build/start.test.pr` (the old
   `Tests/Simple`); no such folder under `test/`. Delete, or point at a fixture of their own.

## E. runtime — wrong, or never finished (to you)
1. **Sorting by the values themselves has no form.** `sort %nums%` (scalars, no field) is written as `order: "%nums%"` or
   `order: " "` → FieldNotFound. list.query's `order` takes a field only (os/system/modules/list/query.notes.md:4);
   the teaching can't express "the elements themselves". app/type/list ListIsItemSortsAsBefore, module/list ListOps.
   A behaviour never finished — design (Ingi): an order with no field sorts by the element?
2. **A missing goal's GoalNotFound isn't caught by its own `on error`.** module/goal/missing GoalCallMissing: `.pr` is
   right (`goal.call(Name) | on.error(Order,Recovery)`), the error escapes. Likely raised before the action runs (the
   name resolved at load); to trace.
3. **double + decimal no longer fails** (or fails uncaught). module/math DoublePlusDecimal_Errors: `.pr` is right
   (`math.add | on.error(Recovery)`), `%caught%` never set. Candidate: the math seam (955b1446b). To trace.
4. **`%!error.callback%` with no current error now fails** ("no error is current") where the test expects null.
   module/callback/errorcallbackoutsidehandler. Runtime or stale — needs the decision on `%!error%` outside a handler.

## F. not yet traced past the goal (one each)
module/signing SignAndVerifyRoundTrip + SignedDataSurvivesInList ("Data has no signature" — sign → call → verify);
module/test/tag TestTagAccumulatesUserTagsOnRun (count ≠ 3); module/file/list Cut3 + module/list/query Cut4 (a
directory's entries render as "a.txt" where the test expects "cut3dir/a.txt"); module/identity/switchdefault
(`%MyIdentity%` unset); module/crypto/providerswap ('sha256-only' provider not registered — a fixture provider?);
module/channel/remove/rolechannelrefused (ChannelInvariantViolation surfaces where the test expects to catch it);
app/type/duration DurationPartsAndCompare (`%long.Minutes%`); plan/app-systems SetChildOnAValueIsAnError.

## Not from this branch's runtime changes
Nothing above traces to 681 (sensitive), 10b, rows-Holds or 228. The kind redesign and the math seam are the only
runtime suspects (E3, maybe parts of D1 after the rewrite).
