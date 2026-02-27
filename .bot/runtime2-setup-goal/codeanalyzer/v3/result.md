# Code Analyzer v3 — runtime2-setup-goal

## Overall Verdict: NEEDS WORK

One high-severity behavioral finding: SettingsData bridge is unreachable from PLang code. Tests mask this by operating on the wrong actor's MemoryStack.

---

## Finding 1 (High): SettingsData bridge unreachable from PLang execution

**Category:** Behavioral — builds pass, tests pass, runtime silently returns null.

### The design intent

`SettingsData` (DataSource/SettingsData.cs:11) is a `Data` subclass registered on MemoryStack so that `%Settings.ApiKey%` resolves to a per-key database read. The docstring states:

> Registered on MemoryStack as "Settings" so that %Settings.ApiKey% resolves to a per-key database read.

### The problem

SettingsData is registered **only** on the System actor's MemoryStack:

```csharp
// Actor.cs:64-68
if (string.Equals(name, "System", StringComparison.OrdinalIgnoreCase))
{
    Context.MemoryStack.Put(new SettingsData(engine));
}
```

But all PLang step execution uses `engine.Context`, which is `User.Context`:

```csharp
// Engine/this.cs:145-146
public PLangContext Context => User.Context;
public Memory.MemoryStack MemoryStack => User.Context.MemoryStack;
```

The execution chain:
1. `Executor.Run2` (line 367): `engine.Goals.Setup.RunAsync(engine, engine.Context, ...)` — `engine.Context` = `User.Context`
2. `RunGoalAsync` (line 209, 278): `context ??= User.Context`
3. `Goal.RunAsync` → `Steps.RunAsync` → `Step.RunAsync` → `Actions.RunAsync` — all pass the same context
4. LazyParamsGenerator's `__Resolve<T>` uses `context.MemoryStack` — which is User's
5. `MemoryStack.Get("Settings")` looks up in `User.Context.MemoryStack._variables` — no "Settings" key exists there
6. Returns `null` → `%Settings.ApiKey%` resolves to `default(T)`

### Why tests don't catch this

All 15 SettingsData test references use `_engine.System.Context.MemoryStack`:

```csharp
// SettingsDataTests.cs:41, 53, 73, 82, 95, 99, 107, 119, 131, 145, 161, 194, 209, 222, 241, 261, 286
var settingsData = _engine.System.Context.MemoryStack.Get("Settings");
var result = _engine.System.Context.MemoryStack.Get("Settings.TestKey");
```

No test resolves `%Settings.ApiKey%` through `engine.Context.MemoryStack` (i.e., the User actor's stack, which is what PLang code actually uses).

### Why settings action handlers still work

The settings action handlers (`get.cs`, `set.cs`, `remove.cs`) access `Context.Engine.System.DataSource` directly — they bypass MemoryStack entirely. So `- get settings 'ApiKey'` works, but `%Settings.ApiKey%` in a step parameter does not.

### Impact

**Silent data loss.** `%Settings.ApiKey%` in any PLang step parameter resolves to null/default. No error, no AskError prompt. The developer gets empty values with no indication why.

### Fix options

Two approaches, depending on design intent:

**Option A: Register SettingsData on all actors' MemoryStacks** — Change Actor.cs to remove the System-only check, so every actor gets a SettingsData instance. Simple, but creates one SettingsData per actor.

**Option B: Register SettingsData on User actor specifically** — Since `engine.Context = User.Context`, register on User. System still has its own for internal use. This is minimal change.

**Option C: Register in MemoryStack constructor** — Make SettingsData a "system variable" like `Now`/`NowUtc`/`GUID`. But MemoryStack doesn't have an engine reference, so this would require refactoring.

The fix should include a test that resolves `%Settings.X%` through `engine.Context.MemoryStack` (User's stack), not `engine.System.Context.MemoryStack`.

---

## Observation (Low): MemoryStack.Clone shares SettingsData by reference

`MemoryStack.Clone()` (line 194-196) shares Data subclasses by reference to preserve virtual `GetChild`:

```csharp
if (kvp.Value is DynamicData || kvp.Value.GetType() != typeof(Data))
{
    clone._variables[kvp.Key] = kvp.Value;  // shared by reference
}
```

When `Clone.Context` is set (line 205), the setter iterates all values and stamps `data.Context = value` (line 24-26). This mutates the **shared** SettingsData's Context, affecting the original stack too.

Currently theoretical — `CreateChild` has zero call sites in Runtime2, and SettingsData.GetChild only uses Context to stamp child Data objects (line 66: `child.Context = Context`). But if contexts diverge (e.g., different actors), the wrong Context would propagate to child Data.

Not worth a finding on its own, but worth noting for when child contexts become real.

---

## Files Reviewed

| File | Verdict |
|---|---|
| `PLang/Runtime2/Engine/Context/Actor.cs` | NEEDS WORK — SettingsData registration scope |
| `PLang/Runtime2/Engine/DataSource/SettingsData.cs` | CLEAN — code is correct, the wiring is wrong |
| `PLang/Runtime2/Engine/this.cs` | CLEAN — Context → User.Context is correct design |
| `PLang/Runtime2/Engine/Memory/MemoryStack.cs` | CLEAN (observation noted) |
| `PLang/Runtime2/Engine/Goals/Setup/this.cs` | CLEAN |
| `PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs` | CLEAN |
| `PLang/Runtime2/Engine/Goals/this.cs` | CLEAN |
| `PLang/Executor.cs` | CLEAN |
| `PLang.Tests/Runtime2/Modules/settings/SettingsDataTests.cs` | NEEDS WORK — all tests use wrong actor |
