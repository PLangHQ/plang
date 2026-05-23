# coder summary — path-polymorphism branch

**Latest version:** v8

## What this is

The `path-polymorphism` branch hosts the typed-returns sweep, the path
scheme polymorphism work (FilePath / HttpPath under a common `IPath`), and
the bootstrap fixes that fell out of the self-rebuild loop. v8 closes the
test-coverage gaps that tester v7 flagged after the v6/v7 code landed.

## v8 (this version) — tests-only

Five findings from tester v7 (4 missing-coverage + 1 parked-file + 1
process). Two new C# test files, four new tests appended to an existing
file, one renamed PLang `.test.goal`. No production code touched.

### Files added

- `PLang.Tests/App/Goals/GoalCallResolutionTests.cs` — 4 tests for the
  slash-qualified resolution coder v6 introduced.
- `PLang.Tests/App/Modules/builder/BuilderRunAsyncTests.cs` — 2 tests
  guarding the inverted `File.Exists` bootstrap check.
- `PLang.Tests/App/Modules/ModulesDescribeReturnTypeTests.cs` — 7 tests
  pinning `Action.ReturnTypeName` for a representative slice of the live
  catalog plus a sanity sweep ("every catalog row carries a value").

### Files modified

- `PLang.Tests/App/Modules/builder/GetActionsTests.cs` — appended 4 tests
  for the new `Actions` filter parameter (named restrict, empty-list as
  no-filter, unknown name → empty, case-insensitive).
- `Tests/Modules/Condition/Files/FileNotExists/ConditionFileNotExists.test.goal`
  — renamed from `.goal2` (the parked extension wasn't discovered by
  `plang --test`, so the negative-branch guard for `if X exists` was inert).
  Built the .pr alongside its two callees (WhenExists.goal, WhenMissing.goal).

### Code example — N4 sanity sweep

The interesting pattern here is the "every catalog row carries a value"
sweep — it pins the *contract* of `DescribeReturnTypeName` (a row must
always describe its return: "data" for polymorphic, real T name otherwise)
without enumerating every action by hand:

```csharp
[Test]
public async Task ReturnTypeName_AllCatalogRows_HaveAValue()
{
    var catalog = _app.Modules.Describe();
    var missing = catalog.Where(a => string.IsNullOrEmpty(a.ReturnTypeName))
                         .Select(a => $"{a.Module}.{a.ActionName}")
                         .ToList();

    await Assert.That(missing.Count)
        .IsEqualTo(0)
        .Because($"actions missing ReturnTypeName: {string.Join(", ", missing)}");
}
```

A future Run() that doesn't return `Task<Data>` / `Task<Data<T>>` (e.g. a
new return wrapper shape) goes red here with the offending row's name in
the message.

### Baseline (this round)

- C# `dotnet run --project PLang.Tests` — **2906 / 2906** (+17 from v7's 2889)
- PLang `plang --test` — **204 / 204** (+1 from v7's 203, 0 stale)
- Build — 0 errors, 454 pre-existing nullable warnings (unchanged)

### Process note

`baseline-tests.md` is in this version — addresses tester v7's N5 nudge.

---

## Prior versions (one-liners)

- **v7** — codeanalyzer v4 F1 (Data<T>.From docstring tightened) + F2
  (orphan `<summary>` block on `DescribeReturnTypeName` removed). Doc-only.
- **v6** — Slash-qualified `goal.call` resolution (ancestor walk), inverted
  `File.Exists` bootstrap check, `builder.actions` Include parameter, two
  builder validators.
- **v5 and earlier** — typed-returns sweep (~70 handlers, 9 provider
  interfaces); IPath, IIdentity, IStore.Exists/Tables typed; runtime2 merge
  + conflict resolution.
