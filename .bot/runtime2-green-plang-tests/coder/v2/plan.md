# Coder v2 — Plan

Address tester v2's three major W3 findings (F3-1, F3-2, F3-3). No runtime code
changes; only new/amended C# test assertions. Scope constraint from the user:
"take F3-1, F3-2, F3-3" — F4c-1 and the minor findings stay out of this version.

## Why

Tester proved via deletion-tests that three load-bearing semantic changes made
in v1 pass C# tests even if the implementation is reverted. Each finding adds
an assertion that anchors the contract in C#, so a future edit can't silently
un-do it.

---

## F3-1 — Assert `variable.set` return value

**File:** `PLang.Tests/App/Modules/variable/settests.cs`

**Contract:** `variable.set` returns a `Data` whose `.Value` equals the stored
value (not empty `Data.Ok()`). Powers ReturnMapping + GoalCallReturn PLang
tests via `%__data__%`.

**Two tests need an added line:**

1. `Set_ReturnsOk` (line 39) — add `await Assert.That(result.Value).IsEqualTo("testValue");`
2. `Set_AsDefault_DoesNotOverwriteExisting` (line 62) — add `await Assert.That(result.Value).IsEqualTo("original");`
   (When AsDefault=true and var exists, handler returns the existing Data, so
   `.Value` is "original" — reverting this branch would return `Data.Ok()` with null Value.)

**Deletion test:** revert `return Task.FromResult(stored);` → `return Task.FromResult(Data.@this.Ok());`
in `set.cs:57`. Either assertion fires.

---

## F3-2 — Assert `Action.RunAsync` no-mutation of `result.Name`

**File:** `PLang.Tests/App/Modules/variable/settests.cs` (new test beside existing ones — same Ctx pattern).

**Contract:** `Action.RunAsync` at `Action/this.cs:116` aliases the result under
`%__data__%` via `context.Variables.Set("__data__", result)`. Old code was
`result.Name = "__data__"; Variables.Put(result);` — mutating shared references.
New code must not mutate `result.Name`.

**Test shape:**

```csharp
[Test]
public async Task ActionRunAsync_AliasesResultUnderData_DoesNotMutateName()
{
    var context = _app.Context;
    var action = TestAction.Create("variable", "set",
        ("name", "%myVar%"), ("value", "hello"));

    var result = await action.RunAsync(context);

    // Reachable under both the variable name and %__data__%
    var dataVar = context.Variables.Get("__data__");
    var myVar = context.Variables.Get("myVar");

    await Assert.That(ReferenceEquals(dataVar, result)).IsTrue();
    await Assert.That(ReferenceEquals(myVar, result)).IsTrue();

    // Name is NOT mutated to "__data__"
    await Assert.That(result.Name).IsNotEqualTo("__data__");
}
```

Note on why `result.Name != "__data__"`:
- The `Value` parameter Data is created with `Name="value"` (from TestAction.Create).
- `__ResolveData("value")` returns the parameter Data itself (plain string, no %var%).
- `variable.set` calls `Variables.Set(Name, Value, ...)` — aliases dv as-is, so returned `stored.Name == "value"`.
- If `Action.RunAsync` mutated it to `"__data__"`, assertion fires.

**Deletion test:** add `result.Name = "__data__";` before line 116 → assertion fires.

---

## F3-3 — Assert `Variables.Set` aliasing-without-clone

**File:** `PLang.Tests/App/Memory/VariablesTests.cs` (add near the `Set_*` tests block).

**Contract:** `Variables.Set(name, Data)` at `this.cs:68–85` aliases the Data
reference as-is. No clone, no rename. Dictionary key is source of truth; Data.Name
stays advisory.

**Test shape:**

```csharp
[Test]
public async Task Set_DataWithDifferentName_AliasesByKey_NoClone_NoRename()
{
    var stack = new Variables();
    var original = new Data("originalName", 42);

    stack.Set("alias", original);

    var retrieved = stack.Get("alias");
    await Assert.That(ReferenceEquals(retrieved, original)).IsTrue();
    await Assert.That(retrieved.Name).IsEqualTo("originalName");
}
```

**Deletion test:** revert `Variables.Set` to old clone-if-names-differ:
```csharp
if (value is Data.@this dv) {
    var stored = string.Equals(dv.Name, name, StringComparison.OrdinalIgnoreCase)
        ? dv : dv.ShallowClone();
    stored.Name = name;
    _variables[name] = stored;
    return stored;
}
```
→ both `ReferenceEquals` and `Name == "originalName"` assertions fire.

---

## Verification

1. `dotnet run --project PLang.Tests -- --filter "SetTests|VariablesTests"` — expect green on new assertions.
2. Full `dotnet run --project PLang.Tests` — expect 2274/2275 (1 pre-existing flake).
3. No PLang test run needed — these are pure C# additions, no runtime code changed.

## Commit shape

One commit: "Tests: anchor W3 contracts — variable.set return, Action no-mutation, Variables.Set aliasing"

## Questions / blockers

None. This is tight C#-only scope matching exactly what tester wrote in `v2/result.md`.
Waiting for Ingi's approval before editing files.
