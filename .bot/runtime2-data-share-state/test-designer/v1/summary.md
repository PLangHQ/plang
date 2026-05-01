# test-designer v1 — Data identity preservation + [VariableName] migration

## What this is

The architect (`runtime2-data-share-state`/architect/v1) redesigned PLang's Data identity model. Today's `As<T>()` always allocates a fresh wrapper, dropping `Properties`, event subscribers, and memory pointer sharing — even when the source already matches T. That violates the "everything is Data" principle: typed views should be LIVE windows into the same variable, sharing state by reference, with only Type and converted `.Value` being distinct.

This branch fixes the identity model first (Phases 1–3), then does the surface migration off `[VariableName]` to typed `Data<T>` (Phases 4–6) as a consequence. The Legacy emission path in `PLang.Generators` goes away.

My job as test-designer was to translate that design into a behavioral contract — the test suite the coder must satisfy. Tests stub `Assert.Fail("Not implemented")` for C# and `- throw "not implemented"` for PLang.

## What was done

**9 new C# test files (62 tests)** + **4 new PLang `.test.goal` files** (+ 2 helper goals), organized in 8 batches that map 1:1 to the architect's 6 phases:

| Batch | File | Tests | Phase |
|---|---|---|---|
| 1 | `PLang.Tests/App/DataTests/EventListTests.cs` | 6 | 1 (events → Lists) |
| 2 | `PLang.Tests/App/DataTests/AsTIdentityTests.cs` | 10 | 2b (identity preservation) |
| 3 | `PLang.Tests/App/DataTests/NamePropagationTests.cs` | 6 | 2a (name propagation) |
| 4 | `PLang.Tests/App/DataTests/PlangAssignabilityTests.cs` | 8 | 2c+2d (string-not-iterable + AsEnumerable) |
| 5 | `PLang.Tests/App/VariablesTests/SubscriberSurvivalTests.cs` | 8 | 3 (Variables.Set dumb storage) |
| 6 | `PLang.Tests/App/Modules/variable/SetTypeInferenceTests.cs` | 12 | 4 (variable.set always-typed) |
| 7a | `PLang.Tests/App/Modules/list/ListAddIdentityTests.cs` | 4 | 5 Pattern A spot-check |
| 7b | `PLang.Tests/App/Modules/loop/ForeachStringNotIterableTests.cs` | 3 | 5 + 2c spot-check |
| 8 | `PLang.Tests/Generator/Diagnostics/Plng001PostMigrationTests.cs` | 5 | 6 (Legacy delete) |

PLang test goals:
- `Tests/Modules/Variable/Set/TypeInference/TypeInference.test.goal`
- `Tests/Modules/Variable/Set/SnapshotClone/SetSnapshotClone.test.goal`
- `Tests/Modules/List/Mutation/ListAddVisibleAfterCall.test.goal` + `AddProduct.goal`
- `Tests/Modules/Loop/Foreach/StringNotIterable/ForeachStringNotIterable.test.goal` + `Inc.goal`

### Design discipline

The contract is anchored on **reference equality** — `ReferenceEquals` for `.Value`, `Properties`, and the three event lists across `As<T>` reads and `Variables.Set` replacements. Where the architect requires aliasing, tests assert ref-share. Where a fresh wrapper is required, tests assert ref-distinctness. I deliberately did NOT pin "exactly N allocations" or specific call counts — that ties the implementation. A test suite that pins behavior in terms of invariants gives the coder room to land the cleanest code while still being protected from regressions.

### Decisions made / open questions resolved

- **Phase 3 — Set(string, object?, Type?) overload** — User picked: drop entirely. variable.set constructs Data itself; Variables.Set takes Data only. The `Set_NonDataValue_WrapsAndFiresOnCreate` test from my plan was dropped accordingly.
- **Phase 3 — Properties on replacement** — Kept architect's stated default: events alias on replacement, Properties does NOT (per-binding metadata). Pinned with `Set_PropertiesNotAliased_NewBindingHasOwnProperties`. If user revisits, that one test flips.
- **PLang assert.type** — User: assert.type doesn't exist but coder will add it; the TypeInference end-to-end goal uses the syntax as-if-implemented. The coder's scope for this branch includes adding the assert action.
- **Variables.Remove fires OnDelete** — added a test (`Remove_FiresOnDelete_OnRemovedData`). Today's Remove (Variables/this.cs:357) does NOT fire it; the architect's events list expects it to. Pinned as part of the Phase 3 contract so the coder wires it up.

## Code example

The pattern across all 9 files is identical: TUnit `[Test]`, `async Task`, `Assert.Fail("Not implemented")`, with comments above each test that ARE the spec. Every test name is `MethodOrBehavior_Scenario_ExpectedResult`. Example from `AsTIdentityTests.cs`:

```csharp
// Variance fast path aliases all three event lists. Subscribing on either
// side and firing on the other proves the lists are ref-shared, not copied.
// This is what makes Debug --debug={"variables":[...]} survive cross-type
// reads in handler bodies.
[Test]
public async Task AsT_Variance_OnChangeAliased_FireOnSourceVisibleThroughWrapped()
{
    Assert.Fail("Not implemented");
}
```

PLang goals carry the same shape — body is `- throw "not implemented"`, the comments above the goal name describe the user-visible scenario:

```
ForeachStringNotIterable
/ End-to-end string-not-iterable check (Phase 2c). set %s% = "hello", set
/ %count% = 0, foreach %s% calls Inc (which increments %count%), assert
/ %count% equals 1 — NOT 5. Without the IsPlangIterable carve-out, foreach
/ would iterate the string char-by-char and the assertion would fail at 5.
- throw "not implemented"
```

## Verification

- `dotnet build PLang.Tests/PLang.Tests.csproj` — clean (0 errors, only pre-existing warnings).
- `dotnet run --treenode-filter "/*/*/EventListTests/*"` — all 6 tests discovered, failing with `Not implemented` as expected.

## What's next

Hand off to the **coder**. The coder works from `architect/v1/plan.md` for the design and from this v1 contract for the acceptance criteria. All 62 C# tests + 4 PLang tests must pass.
