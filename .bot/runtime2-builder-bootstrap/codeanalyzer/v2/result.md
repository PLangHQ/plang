# Codeanalyzer v2 — Findings

Branch: `runtime2-builder-bootstrap`
Trigger: coder commit `80200746` ("Address codeanalyzer v1 findings")
Scope: re-review the 11 files touched by `80200746`. Pass 4 — does each fix correctly close v1, AND does the fix itself break anything new?
Date: 2026-04-28

## TL;DR

All 8 priority-list fixes are correctly applied and behaviorally sound. The 2 explicitly-deferred items (#9, #10) remain open for a separate consolidation pass. Nothing in the fix set introduces a regression. **Verdict: CLEAN.**

---

## Per-fix verification

### #1 — DiagGoal probe deleted (`DefaultBuilderProvider.cs`)

**v1 finding**: Lines 171–198 walked every goal/step/action/parameter on every save with hardcoded "GoalName" knowledge and bare catches. Pure deletion-test win.

**Fix**: Block deleted in full (29 lines removed, no replacement). Remaining flow at line 168 (`GroupModifiersRecursive`) flows directly to `validateResponse.ValidateGoalState` at line 170.

**Pass 4**: No replacement debugging hook left behind, no orphaned imports. Deletion is clean. ✅

---

### #2 — TypeConverter throws → structured Error

**v1 finding**: `TryConvertTo` lines 266 and 296 threw `InvalidOperationException` on CLR-type-name leaks, bypassing the framework's structured error pipeline. A `Try*` method must never throw.

**Fix**: Both sites now `return (null, new Errors.Error("...", "ClrTypeNameInGoalSlot", 500) { FixSuggestion = "..." })`. StatusCode 500 correctly puts these in `ErrorCategory.Runtime` (line 47 of Error.cs). Bonus: the previously-untrapped `JsonSerializer.Deserialize<GoalCall>` call at line 276 is now wrapped in a try/catch with the same filter pattern.

**Pass 4 — caller trace**: Searched all 16 call sites of `TryConvertTo` (PLang/, PLang.Generators/). None depend on the exception propagating: every caller either tuple-deconstructs `(value, error)` and checks `error`, or discards with `_`. The throw → Error conversion is a behaviorally invisible improvement at the caller layer. ✅

---

### #3 — Bare-catch filtering (5 hand-written sites + generator)

| Site                               | Old                | New filter                                                      | Verdict |
|------------------------------------|--------------------|------------------------------------------------------------------|---------|
| `TypeConverter.cs:88`              | `catch`            | `JsonException ‖ NotSupportedException ‖ ArgumentException`      | ✅ |
| `TypeConverter.cs:101`             | `catch (JsonException)` (inner) | same triple as above                                | ✅ broadened with care |
| `TypeConverter.cs:190`             | `catch (Exception)`| `not (NullReferenceException ‖ OOM ‖ StackOverflowException)`    | ✅ matches codebase pattern |
| `TypeConverter.cs:280` (GoalCall)  | (no try/catch)     | `not (NullRef ‖ OOM ‖ StackOverflow)` + structured Error return  | ✅ new defensive wrap |
| `Variables/this.cs:162`            | `catch`            | `JsonException ‖ NotSupportedException` + Debug.Write            | ✅ surfaces failure |
| `FluidProvider.cs:140`             | `catch`            | `JsonException ‖ NotSupportedException`                          | ✅ |
| `Errors/Error.cs:292`              | `catch`            | `JsonException ‖ NotSupportedException`                          | ✅ |
| `LazyParamsGenerator.cs:542` (gen) | `catch (Exception)`| `not (NullRef ‖ OOM ‖ StackOverflow)`                            | ✅ propagates to all 118 generated handlers |

**Pass 4 — filter-shape audit**:
- `JsonException‖NotSupportedException` is the exact shape `JsonSerializer` documents for serialize/deserialize errors. Adding `ArgumentException` at TypeConverter:88 is intentional (catches `Deserialize`'s "type not supported" path which can surface as ArgumentException). Reasonable.
- `not (NullRef‖OOM‖StackOverflow)` matches the existing project pattern at other sites. Catches `OperationCanceledException` and wraps as ServiceError — could theoretically mask intended cancellation. Builder doesn't currently use cancellation, so no live problem; worth knowing if cancellation is added later.
- All filter shapes are tighter than the bare catch they replaced. ✅

---

### #4 — IsDeferredActionTemplate uses CLR identity (`Data/this.cs:522`)

**v1 finding**: Old code matched on PLang type-name string ("action" / "list<action>"). User `[PlangType("action")]` aliases would collide.

**Fix**:
```csharp
var clr = type?.ClrType;
if (clr == null) return false;
var actionType = typeof(App.Goals.Goal.Steps.Step.Actions.Action.@this);
if (clr == actionType) return true;
return typeof(IEnumerable<App.Goals.Goal.Steps.Step.Actions.Action.@this>).IsAssignableFrom(clr);
```

**Pass 4 — over-broad?** The new check matches more than the old one:
- `Action.@this` ✅ (was matched as "action")
- `List<Action.@this>` ✅ (was matched as "list<action>")
- `Action.@this[]` ✅ NEW
- `Actions.@this` (which IS `IList<Action.@this>`, found at `App/Goals/Goal/Steps/Step/Actions/this.cs:5`) ✅ NEW
- `Dictionary<string, Action.@this>` — does NOT match (Dictionary's IEnumerable is `IEnumerable<KVP<K,V>>`, not `IEnumerable<V>`) ✅

The expansion is actually correct: any collection-of-Action that holds `%var%` templates needs deferred resolution. Old code's narrow string match would silently miss `Actions.@this` collections; new code catches them. Improvement.

**Pass 4 — null ClrType?** `type?.ClrType` returns null when the Type's Value is unrecognized. For Value="action", PlangTypeIndex resolves to Action.@this via the @this-namespace convention (Action namespace ends in "Action" → name="action"). Verified the type is in PlangTypeIndex.Assemblies (App assembly). Works in builder bootstrap. ✅

---

### #5 — Variables.Set snapshot-clone logs failure (`Variables/this.cs:162`)

**v1 finding**: Bare catch silently fell back to alias mode — re-creates the bug the round-trip was added to fix.

**Fix**: Filter narrowed to `JsonException ‖ NotSupportedException`; on catch, `_ = _context?.App?.Debug?.Write($"[Variables.Set] snapshot-clone failed for '{name}': ...")`.

**Pass 4**:
- Debug.Write returns `Task.CompletedTask` when debug is disabled (Debug/this.cs:111), so the discard is cheap.
- The log fires only when `Debug.IsEnabled` — failures in production are still silent. This is a deliberate trade-off (don't crash production over a JSON serialize edge case) but worth flagging: the comment claims "surface the failure" but the surfacing is debug-mode-only.
- Fall-through to alias mode preserved — same behavior as before, just observable in dev.

**Verdict**: ✅ accepted; matches the project pattern of "fail forward, log when watching." The v1 concern (the bug isn't debuggable when it re-regresses) is now addressable: turn debug on, repro, see the line.

---

### #6 — Context Clone/CreateChild doc-only (`Actor/Context/this.cs:259, 271`)

**v1 finding**: Clone and CreateChild propagate different state. Either fix both, or document intent.

**Fix**: Doc comments added to both methods marking them test-fixture-only. No code change.

**Pass 4 — verify the claim**: Grepped `PLang/` for `CreateChild` callers — only the definition site is found. Production code creates contexts only via the Actor ctor. Test code uses both methods (PLangContextTests, SettingsTests, VariablesTests, etc.). The doc accurately describes reality. ✅

If a production caller is added later, the divergence becomes a real bug — but that's a future-state problem, and the doc comment provides the warning in the right place.

---

### #7 — PlangTypeIndex (`Utils/PlangTypeIndex.cs`)

**v1 finding**: `Reset()` cleared 2 of 4 caches (only `_nameToType` and `_typeToName`); `_clrTypeFullNamesInitialized` non-volatile, breaks DCL on ARM.

**Fix**:
- `Reset()` deleted entirely (zero callers — confirmed).
- `private static volatile bool _clrTypeFullNamesInitialized;`

**Pass 4 — memory-model audit**:
.NET ECMA-335 + CLR 4.0+: volatile writes have release semantics, volatile reads have acquire semantics. The DCL pattern at lines 57–69 reads the flag (acquire), and only inside the lock writes to the HashSet then sets the flag (release). Acquire-write semantics ensure that any thread observing `_clrTypeFullNamesInitialized == true` also observes all prior HashSet.Add calls. Standard DCL idiom — now correct on ARM/AArch64. ✅

`Reset()` removal: the only test files calling it would have been broken; verified by grep that no test calls Reset on `PlangTypeIndex` (the test references to `Clone`/`CreateChild` from #6 are unrelated). Clean delete. ✅

---

### #8 — error.handle.Wrap symmetric (`modules/error/handle.cs:109`)

**v1 finding**: GoalFirst returned `recoveryResult` (line 96), RetryFirst returned bare `Ok()` (line 109). Asymmetric.

**Fix**: Line 109 now `return recoveryResult;` — symmetric.

**Pass 4**:
- `recoveryResult.ErrorChain.Add(...)` at line 110 is unreachable when Success — fine.
- `recoveryResult` carries the recovery's value through both code paths now. PLang test gap is the right follow-up; the coder logged it in `Documentation/Runtime2/todos.md`.
- No regression: the only behavior change is "RetryFirst + recovery succeeds" now flows the recovery's value to the caller. Previously this case dropped the value. Anyone whose code accidentally depended on the bare `Ok()` would have been observing a bug. ✅

---

## Carryover items — explicitly deferred

| # | Item | Status |
|---|------|--------|
| 9 | Three "formal syntax" renderers (Catalog/ExampleRenderer, FluidProvider, builder/DefaultBuilderProvider) — same logic in three places | DEFERRED — coder note: "pure cleanup, separate pass" |
| 10 | Culture-sensitive `ToString` at `ExampleRenderer.cs:105`, `FluidProvider.cs:138` | DEFERRED — folds into #9 (same renderers) |

Both should land together in a future "consolidate-formal-renderers" pass. Acceptable to defer; flag for the next planning pass.

---

## Carryover sub-findings (legitimately lower-priority, not on v1 priority list)

These were called out in v1's per-file notes but were not on the priority list. Worth tracking but not blocking:

1. **`TypeConverter.cs:91–102`** — silent first-element-of-array (drops items 1..N when JSON is array but target is single object). Bare catch is now filtered, but data loss persists on the silent path.
2. **`TypeConverter.cs:56`** — `null → int = 0` silent default for value types. No error, no warning.
3. **`TypeConverter.cs:88` outer fall-through** — when JSON parse fails AND it's not an array, original `JsonException.Message` is discarded as we fall through to other rules. v1 suggested only falling through when `!jsonStr.TrimStart().StartsWith({,[)`.
4. **`Variables.Set` log surfaces only with Debug enabled** — silent in production (acceptable trade-off, but the comment claim "surface the failure" overstates).

None of these are regressions introduced by this commit; they pre-existed and were never prioritized.

---

## Cross-cutting v2 observations

### A. Filter shape is now consistent

The codebase had a mix of bare-catch and filtered-catch before this commit. After: every reviewed site uses one of two appropriate filter shapes (`JsonException ‖ NotSupportedException [‖ ArgumentException]` for serialize/deserialize sites; `not (NullRef ‖ OOM ‖ StackOverflow)` for general sites). Future contributors have clear precedent. ✅

### B. The throw → Error pattern propagates correctly

The v1 fix at TypeConverter (#2) plus the generator filter (#3 last row) means handler exceptions now consistently arrive at the user as a `Data.@this.FromError(...)` instead of an unwrapped CLR exception. Combined with `__SnapshotParams()` enrichment at LazyParamsGenerator:545, errors carry full debug context (param state at failure). This is an end-to-end improvement — not a single fix but the whole pattern lining up.

### C. Clone/Copy family hazard for Context — managed by documentation

v1 flagged the divergence between Clone and CreateChild. The coder's choice — document as test-fixture rather than reconcile — is defensible because production zero-callers means there's no behavior to break. The risk surfaces only if a future author wires Clone/CreateChild into production; the doc comment is the warning. Not an unconditional pass; an accepted trade-off.

---

## Verdict

**CLEAN** — Coder closed the v1 priority list correctly. Every fix is verified at the code level AND traced through callers / memory model / behavioral edge cases. No fix introduces a regression.

Recommendation: send to **tester** next. The behavioral changes (#4 broader IsDeferredActionTemplate, #8 RetryFirst recovery-value flow) deserve test coverage before this branch lands. The recovery-value test is already logged in `Documentation/Runtime2/todos.md`.

Items #9, #10, and the four carryover sub-findings should land in a follow-up pass; they are not blockers.
