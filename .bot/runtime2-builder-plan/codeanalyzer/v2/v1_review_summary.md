# v1 Review Summary

v1 found 23 findings, 2 critical. The coder addressed findings on branch `fix-plang-tests` in commit `db09e0f4`.

## What the coder addressed

| v1 Finding | Status | Approach |
|---|---|---|
| #1 Foreach dict support dropped | Fixed | `Data.EnumerateItems()` yields (key,value) pairs; foreach uses it |
| #2 Condition guard on Variables | Fixed | Guard moved to `Context._data` with step-scoped key |
| #4 Data.Value unstable (NeedsResolution) | Fixed | Resolve once, cache result, clear flag |
| #5 ResolveDeep mutates shared objects | Fixed | Clone via MemberwiseClone before mutation |
| #8 PromoteGroups.SetValue no-op for JsonElement | Fixed | Added warning on JsonElement |
| #9 list.any/group don't handle POCOs | Fixed | Rewritten to use `Data.EnumerateItems()` + `GetChild()` |
| #17 As<T>() uncached reflection | Fixed | ConcurrentDictionary cache added |
| #18 Console.ReadLine hangs headless/CI | Fixed | Headless defaults to "no", --app={"create":true} opt-in |
| #20 Handled flag broadened | Verified | All 3 setters audited, all correct |
| #19 Action.Return removal | Cleaned up | Return property removed entirely, all references cleaned |

## What was NOT addressed

| v1 Finding | Status |
|---|---|
| #6 Implicit operator @this<T>(T) | Not addressed — still exists |
| #7 Static _buildTimer | Not addressed (LOW) |
| #23 PromoteGroups no tests | Not addressed (MEDIUM) |
