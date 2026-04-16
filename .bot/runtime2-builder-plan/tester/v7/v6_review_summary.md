# v6 Review Summary — What Changed

## Merged: fix-plang-tests branch (3 commits)
- `db09e0f4` — Fix code analyzer findings: OBP list modules, condition guard, Data stability
- `51937470` — Rebuild test .pr files, add path support to --build files filter
- `d29a9307` — Restructure Tests/: flatten Runtime2, merge similar, remove v1

## v6 Finding Resolution

### RESOLVED (7 findings)
1. **#5 (critical) Foreach dict FALSE GREEN** — FIXED. `Data.EnumerateItems()` yields `(key, value)` pairs. New test `Foreach_Dictionary_KeyIsStringNotIndex` asserts `key=="greeting"` and `val=="hello"`. The core bug is gone.
2. **#6 (critical) Condition orchestration barely tested** — IMPROVED. 18 new IfHandlerTests: truthy/falsy, true/false branching, if/else, inner goal independence, negation, error handling, type mismatch. Orchestrate coverage is now 92.9%.
3. **#18 (major) Data.ToBoolean / As<T> / ShallowClone 0%** — PARTIALLY FIXED. IfHandlerTests exercise `IsTruthy_*` and `Run_EqualsTrueWithToBoolean`. Data.this coverage rose from ~70% to 78%. But: ToBoolean numeric paths (double, float, decimal, short, byte) still at 0%.
4. **#20 (major) Return removal backward compat** — RESOLVED BY DESIGN. Action.Return removed entirely. Old .pr files will need a rebuild, which is the intended migration path.
5. **#21 (major) List tests lack value assertions** — IMPROVED. List modules rewritten to use Data.EnumerateItems/GetChild (OBP). Coverage: first=100%, get=100%, join=100%, contains=100%, indexof=90%, last=80%, count=66.7%.
6. **#3 (critical) list.any 0%** — Still 0% directly, but code was rewritten significantly (30+ lines removed, now uses Data navigation).
7. **#4 (critical) list.group 0%** — Still 0% directly, but code was rewritten (22+ lines removed).

### UNCHANGED (14 findings)
- **#1 validateResponse.cs** — Still 0% (138 lines)
- **#2 promoteGroups.cs** — Still 0% (2 lines, thin delegation)
- **#5 timer module** — Still 0% (20 lines)
- **#6 cache.store** — Still 0% (44 lines)
- **#7 LLM retry test broken** — Same failure
- **#8 LLM tool call flaky** — Same failure  
- **#9 Actor settings leak** — Not observed in this run (may be intermittent)
- **#10 ReservedKeywords** — Still 0% (118 lines)
- **#11 Data.Compare weak edges** — Unchanged
- **#12 JsonStringNavigator** — Still 14.3%
- **#13 MemoryStepCache** — Still 40.9%
- **#14 UI render** — Still 0%
- **#15 Data.Compare edge cases** — Unchanged
- **#19 IBuildValidatable** — Still 0%
