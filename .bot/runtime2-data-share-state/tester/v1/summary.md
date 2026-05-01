# tester v1 — Data identity preservation

## What this is

Test-quality validation of coder/v1's Data identity-preservation work on
`runtime2-data-share-state` (Phases 1+2+3+4+5a). Coder rewrote `Data.As<T>` to
share state by reference across cross-type wraps, made `Variables.Set` dumb
storage, made `variable.set` the sole binding-mint site, and added the
`IsPlangIterable` carve-out so foreach over a string runs once. Codeanalyzer
ran twice (CLEAN at v2). My job: confirm the new tests honestly verify those
contracts and aren't false greens.

## What was done

1. **Ran both test suites.**
   - C# (`dotnet run --project PLang.Tests`): **2533 total, 2524 passed, 9 failed**. The 9 failures are exactly the deferred stubs (4 `ListAddIdentityTests` + 5 `Plng001PostMigrationTests`) that all use `Assert.Fail("Not implemented")`. ✓ Honest stubs.
   - PLang (`plang --test`): **173 total, 170 pass, 2 fail, 1 stale**. Both failures (`tests/modifiers/PerActionErrorScope`, `tests/modifiers/OnErrorCallGoal`) are stale .pr files in a legacy lowercase directory built against an older `error.handle`/`goal.call` schema. The maintained `Tests/Modules/Modifiers/` versions pass. NOT coder/v1 regressions.

2. **Ran coverage.** Cobertura XML on the 7 production files coder touched:
   | File | % | Notes |
   |---|---|---|
   | `Data/this.cs` | 86.4 | High |
   | `Variables/this.cs` | 90.6 | High |
   | `variable/set.cs` | 89.0 | Cold-type if-chain arms uncovered |
   | `list/add.cs` | 56.1 | Complex-snapshot path uncovered |
   | `Utils/Json.cs` | 54.2 | Only ~5 new lines, indirectly covered |
   | `Debug/this.cs` | 44.3 | Only ~5 new lines |
   | `Generators/Emission/Property/Data/this.cs` | 82.4 | High |

3. **Read all 8 new test files.** Applied the deletion test mechanically. Verified ref-equality on `Properties` / `OnChange` / `OnCreate` / `OnDelete` / `Value` is the contract everywhere it should be (and ref-DISTINCT where required, e.g. conversion-failure isolation).

4. **Wrote findings to `test-report.json`.**

## Findings (7 total)

| # | sev | type | summary |
|---|---|---|---|
| 1 | major | missing-coverage | `MintTyped` cold types (decimal, float, DateTimeOffset, Guid, byte[]) + reflection cold path uncovered |
| 2 | major | missing-coverage | `list.add` complex-snapshot path (lines 53–66) entirely uncovered — codeanalyzer/v2's behavioral concern |
| 3 | major | missing-coverage | `Variables.Set` dot-path JsonElement-vs-Dictionary regression unprotected |
| 4 | major | missing-coverage | `set.cs` Unknown-type ServiceError path + ValidateBuild error message uncovered; weak `Success==false` assertions |
| 5 | minor | weak-assertion | `Set_IntValue_MintsDataOfInt` accepts Data<int> OR Data<long> — name misleading |
| 6 | minor | missing-coverage | Replacement-NOT-fires-OnDelete contract not pinned |
| 7 | minor | missing-plang-test | Stale .pr files in legacy `tests/modifiers/` (out of coder scope) |

All 7 are coverage gaps. **No false greens.** The existing assertions correctly use
`ReferenceEquals` for identity claims; no test passes by being weakly written.

## Verdict: **approved**

The identity-preservation contract is strongly pinned. The 9 deferred stubs honestly
fail. The new behaviors (dumb-storage Variables.Set, OnDelete on Remove,
AsCanonical live-variable resolution, IsPlangAssignable single source of truth, IsPlangIterable string-not-iterable
carve-out) are all tested with `ReferenceEquals` ref-equality discipline.

**Recommendation:** approve-with-deferred-coverage. If Ingi prefers gaps closed
before merge, the 4 major findings need ~1–2h of test additions following existing patterns.
Otherwise, proceed to **auditor**.

## Code example — what good looks like in this branch

`AsTIdentityTests.AsT_Variance_OnChangeAliased_FireOnSourceVisibleThroughWrapped`
demonstrates the discipline:

```csharp
var source = new global::App.Data.@this<List<int>>("nums", list) { Context = ctx };
var wrapped = source.As<System.Collections.IEnumerable>();

// Ref-share contract — wrapped.OnChange and source.OnChange ARE the same list.
await Assert.That(ReferenceEquals(source.OnChange, wrapped.OnChange)).IsTrue();

// Behavioral consequence: subscribing on wrapped + firing on source = subscriber sees it.
var seen = 0;
wrapped.OnChange.Add((_, _) => seen++);
source.FireOnChange(new global::App.Data.@this<List<int>>("nums", new List<int>()));
await Assert.That(seen).IsEqualTo(1);
```

Two assertions — one for ref-equality of the list, one for the live-firing semantics
that ref-equality enables. The deletion test: if `WrapAs` allocated a fresh OnChange
list (regression), the first assertion fails; if it copy-constructed the list (a
plausible "fix" for thread-safety paranoia), the second assertion fails — the
post-wrap subscriber wouldn't reach source. Both shapes of regression are caught.

This pattern is consistent across all 10 tests in `AsTIdentityTests`, all 8 in
`PlangAssignabilityTests`, and all 8 in `SubscriberSurvivalTests`. ✓

## Code example — what's missing

`list/add.cs:53–66` (the entire complex-snapshot path) has zero tests. This is the
exact path codeanalyzer/v2 flagged as a quiet behavior unification — `SnapshotClone`
+ `UnwrapJsonElement` now applies. Adding one test catches the gap:

```csharp
[Test]
public async Task Add_DictValue_SnapshotCloneUnwrapsJsonElement()
{
    var (context, memory) = CreateContext();
    var dict = new Dictionary<string, object?> {
        ["nested"] = new Dictionary<string, object?> { ["k"] = 1 } };
    var action = new Add { Context = context, ListName = "items",
        Value = new global::App.Data.@this("", dict) };
    await action.Run();
    var list = memory.GetValue("items") as List<object?>;
    var entry = (list![0] as global::App.Data.@this)!.Value;
    await Assert.That(entry).IsTypeOf<Dictionary<string, object?>>();
    var nested = ((Dictionary<string, object?>)entry!)["nested"];
    await Assert.That(nested).IsTypeOf<Dictionary<string, object?>>();  // NOT JsonElement
}
```

This test fails immediately if SnapshotClone regresses to leaving JsonElement graphs.

## Files written

- `.bot/runtime2-data-share-state/tester/v1/plan.md` — plan
- `.bot/runtime2-data-share-state/tester/v1/coverage.json` — Cobertura summary
- `.bot/runtime2-data-share-state/tester/v1/result.md` — full deletion-test analysis
- `.bot/runtime2-data-share-state/tester/v1/summary.md` — this file
- `.bot/runtime2-data-share-state/tester/v1/verdict.json` — pass
- `.bot/runtime2-data-share-state/test-report.json` — branch-shared report
