# Tester v1 Summary — runtime2-settings

## What this is
Test quality review of the Settings infrastructure for PLang App. The coder implemented a scope-chained settings system: `Scope` (ConcurrentDictionary wrapper), `Settings` (scope chain resolution + engine defaults), and `ModuleView` (context-bound typed view). 15 C# tests and 1 PLang test were written.

## Test run
- C# tests: **1254 pass, 0 fail, 0 skipped**
- PLang tests: Not runnable (builder requires `--llmservice=openai`, not available in this environment)
- Coverage: dotnet-coverage profiler failed to initialize (glibc/dependency issue). Manual code-path analysis performed.

## Findings

### Finding 1: CRITICAL — InvalidCastException on type mismatch in Resolve<T>

**File:** `PLang/App/Settings/this.cs:34,40`

`Resolve<T>` does `(T)value` — a hard unboxing cast. In C#, unboxing requires an **exact type match**: `(long)(object)42` throws `InvalidCastException` when 42 is boxed as `int`.

This is a real production scenario. When PLang deserializes a JSON parameter like `20971520`, `System.Text.Json` may box it as `int` (it fits in 32 bits). But `archive.Settings.Max` declares `long`. The `Resolve<long>` call would then do `(long)(object)intBoxedValue` — crash.

**No test covers this path.** All existing tests store values as `long` literals, so the cast always succeeds. If you flip the cast to a safe pattern (`value is T typed ? typed : classDefault`), every test still passes — no test is guarding the current behavior.

**Impact:** First production use of Settings with a numeric parameter could crash at runtime.

**Suggestion:** Add a test that stores `int` and resolves as `long`. Then fix the cast to handle numeric widening (or use `Convert.ChangeType`).

```csharp
// Test that should exist:
[Test]
public async Task Resolve_HandlesNumericWidening()
{
    var (engine, context) = CreateEngine();
    engine.Settings.Set("archive.max", 20 * 1024 * 1024, context); // int, not long
    var result = engine.Settings.Resolve<long>("archive.max", context, 100L);
    await Assert.That(result).IsEqualTo(20L * 1024 * 1024);
}
```

### Finding 2: MAJOR — Goal save/restore of SettingsScope not tested

**File:** `PLang/App/Goals/Goal/Methods.cs:29-32,89`

`RunAsync` saves/restores `context.SettingsScope` in a try/finally — nulling it at goal entry, restoring on exit. This is the mechanism that scopes settings to individual goals when the **same context** is passed to sequential goals.

**No test exercises this code path.** All SettingsTests use `CreateChild()` which creates a NEW context with a Parent chain. The save/restore mechanism (same context, different goals) is completely untested. If lines 29-32 and 89 were deleted, no test would fail.

These are two **different isolation mechanisms** and the tests only cover one:
- `CreateChild()` → Parent chain → tested by `Resolve_InheritsFromParentContext`
- `RunAsync` save/restore → same-context goal scoping → **untested**

**Impact:** If the save/restore code has a bug (e.g., restoring in wrong order, not restoring on exception), it silently leaks settings between goals.

**Suggestion:** Write a test that runs two goals sequentially on the same context. Goal A sets a setting. After A completes, goal B should NOT see A's setting (SettingsScope was restored to pre-A state).

### Finding 3: MAJOR — Scope chain gap (3+ level parent chain) not tested

**File:** `PLang/App/Settings/this.cs:28-37`

The scope chain walk does `if (current.SettingsScope != null)` to skip contexts without a settings scope. But the only parent-chain test (`Resolve_InheritsFromParentContext`) uses a 2-level chain where the parent has a scope.

**Not tested:** A 3-level chain where the middle level has `SettingsScope == null`. Setup: grandparent sets a value → parent has no scope → grandchild resolves. Should return grandparent's value by skipping the null-scope parent.

**Impact:** A bug where null SettingsScope terminates the walk (instead of skipping) would go undetected. For example, if someone changed the code to `if (current.SettingsScope == null) break;` — no test would catch it.

**Suggestion:**
```csharp
[Test]
public async Task Resolve_SkipsParentWithNoScope()
{
    var (engine, grandparent) = CreateEngine();
    engine.Settings.Set("archive.max", 42L, grandparent);

    var parent = grandparent.CreateChild();  // no settings set — SettingsScope is null
    var child = parent.CreateChild();

    var result = engine.Settings.Resolve<long>("archive.max", child, 0L);
    await Assert.That(result).IsEqualTo(42L);
}
```

### Finding 4: MINOR — Scope overwrite behavior not tested

**File:** `PLang.Tests/App/Settings/ScopeTests.cs`

No test sets the same key twice and verifies the second value wins. The ConcurrentDictionary handles this correctly by default, but the test should document this contract. If the implementation changed (e.g., to a dictionary that throws on duplicate), no test would catch it.

### Finding 5: MINOR — Null value in Scope.Set will throw

**File:** `PLang/App/Settings/Scope.cs:20-23`

`Scope.Set(string key, object value)` passes value directly to `ConcurrentDictionary[key] = value`. ConcurrentDictionary does **not** allow null values — this throws `ArgumentNullException`. But `Scope.Get` returns null for missing keys. There's no way to "unset" a setting once set.

Not tested, not documented. This may be intentional (settings are write-once-per-scope), but it's a hidden constraint.

### Finding 6: MINOR — Only one PLang .goal test

**File:** `Tests/App/Settings/SetMaxGzipSize/Start.test.goal`

There's only one PLang integration test (set + get + assert). Missing PLang tests for:
- Setting engine defaults (`set default max gzip size to 50mb`)
- Setting compression level (enum type, not numeric)
- Inheriting settings across goal calls

PLang tests validate the full pipeline (builder → .pr → GoalMapper → runtime). Without them, the builder's ability to parse settings steps is unverified for all but one scenario.

## Coverage summary (manual)

| Source file | Lines | Methods tested | Methods untested |
|---|---|---|---|
| Scope.cs | 29 | Get, Set, Contains | (all covered) |
| this.cs (Settings) | 75 | Resolve, Set, For | Resolve type mismatch path |
| ModuleView.cs | 37 | Resolve | (all covered) |
| Goal/Methods.cs | 93 | RunAsync | SettingsScope save/restore path |

All happy paths are covered. The gaps are in error/edge paths and the integration between Settings and Goal execution.

## Verdict: **needs-fixes**

Two findings are blocking:
1. **CRITICAL:** The type mismatch in `Resolve<T>` is a real production crash path that no test guards against.
2. **MAJOR:** The goal save/restore mechanism — the core purpose of goal-scoped settings — has zero test coverage.

The remaining findings are minor but should be addressed to make the test suite honest about what it covers.
