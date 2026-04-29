# Coder v2 — Summary

## What this is

Targeted follow-up to tester v2's review of coder v1's Waves 1–4. Three
major findings (F3-1, F3-2, F3-3) flagged that Wave 3's load-bearing
semantic changes had no C# test guarding the contract — reverting each
would leave the suite green. This version adds those anchors.

**Scope:** test-only. No runtime code changes. User's dispatch was
explicit: "Coder takes F3-1, F3-2, F3-3." F4c-1 (dormant prompt rules)
and the minor findings stay out of this version.

## What was done

Three edits in two files:

### `PLang.Tests/App/Modules/variable/settests.cs`

**F3-1 — two assertions added:**
- `Set_ReturnsOk`: asserts `result.Value == "testValue"`. Guards
  `set.cs:57` returning the stored Data (not `Data.Ok()`).
- `Set_AsDefault_DoesNotOverwriteExisting`: asserts `result.Value == "original"`.
  Guards the AsDefault-with-existing branch at `set.cs:47–51` returning
  the existing Data.

**F3-2 — new test `ActionRunAsync_AliasesResultUnderData_DoesNotMutateName`:**
- Runs `variable.set %myVar% = "hello"` via `action.RunAsync(Ctx)` (not
  `_app.Run`, which bypasses `Action.RunAsync` and hence the aliasing).
- Asserts `Variables.Get("__data__")` and `Variables.Get("myVar")` are
  reference-equal to the returned `result`.
- Asserts `result.Name != "__data__"` — the key invariant. The parameter
  Data's Name is "value"; the old code mutated it to "__data__".

### `PLang.Tests/App/Memory/VariablesTests.cs`

**F3-3 — new test `Set_DataWithDifferentName_AliasesByKey_NoClone_NoRename`:**
- `new Data("originalName", 42)` → `stack.Set("alias", original)`.
- Asserts `ReferenceEquals(retrieved, original)` — same instance.
- Asserts `retrieved.Name == "originalName"` — not mutated to "alias".
- A revert to old ShallowClone + rename fails both asserts.

## Verification

- `dotnet build PLang.Tests` — 0 errors.
- Full suite: 2275/2276 passing (was 2273/2274). The +2 comes from F3-2
  and F3-3 adding new test methods; F3-1 only adds assertions to existing
  tests. The single failing test is the pre-existing
  `Query_ToolCall_LlmRequestsToolAndHandlesError` LLM flake — unrelated.

## Code example — the F3-2 contract anchor

The load-bearing line in `Action/this.cs:116` is `Variables.Set("__data__", result)` —
aliases, not mutates. The matching test:

```csharp
[Test]
public async Task ActionRunAsync_AliasesResultUnderData_DoesNotMutateName()
{
    var context = _app.Context;
    var action = TestAction.Create("variable", "set",
        ("name", "%myVar%"), ("value", "hello"));

    var result = await action.RunAsync(context);

    var dataVar = context.Variables.Get("__data__");
    var myVar   = context.Variables.Get("myVar");

    await Assert.That(ReferenceEquals(dataVar, result)).IsTrue();
    await Assert.That(ReferenceEquals(myVar,   result)).IsTrue();
    await Assert.That(result.Name).IsNotEqualTo("__data__");
}
```

If anyone reintroduces `result.Name = "__data__";` before line 116, the
final assertion fires. That's the anchor.

## What's next

- Hand to **tester** for re-verification of F3-1/2/3 closure.
- Then **architect** for the F4c-1 (dormant prompt rules) conversation —
  per tester v2, that's coder-or-architect's call, and the decision
  affects whether to attempt another Tests/ rebuild.
- Then **security** after Wave 3 gaps close.

## Files modified

- `PLang.Tests/App/Modules/variable/settests.cs` — F3-1 (two asserts),
  F3-2 (new test).
- `PLang.Tests/App/Memory/VariablesTests.cs` — F3-3 (new test).

## Files NOT modified

- No runtime code. No `PLang/App/**`.
- No PLang tests. No `Tests/**`.
- The untracked `Tests/Modules/Test/Report/snapshots/junit_sensitive_masked.xml`
  delta in `git status` is test-run churn (timestamps + generated keys);
  it's not included in this commit.
