# Code Analyzer v1 — Summary

## What this is
Code simplicity analysis of the `runtime2-settings` branch — strongly typed, goal-scoped module settings for PLang App. New subsystem: ISettings marker, Settings registry, Scope chain, ModuleView, plus archive.Settings as first consumer.

## What was done
Three-pass analysis (OBP compliance, simplification, readability) of 14 files — 6 new production, 4 modified production (diffs), 3 test files, 1 PLang test goal.

**Result: 14 CLEAN, 0 NEEDS WORK.**

No OBP violations. The settings subsystem follows OBP correctly:
- Engine owns the settings registry (`engine.Settings`)
- Scope owns its key-value data (smart wrapper)
- ModuleView is a lightweight context-bound navigation view
- Resolution walks context → parent → engine defaults → class default
- Goal scoping uses the established save/restore pattern

Three findings:

| # | Severity | What | File |
|---|----------|------|------|
| 1 | Medium | Hard cast `(T)value` in Resolve — should use safe pattern | Settings/this.cs:34,40 |
| 2 | Info | Module prefix derived from namespace — undocumented assumption | Settings/this.cs:52-54 |
| 3 | Low | No test for type mismatch in Resolve | SettingsTests.cs |

## Code example
The cleanest pattern — the scope chain resolution (Settings/this.cs):

```csharp
public T Resolve<T>(string key, PLangContext context, T classDefault)
{
    var current = context;
    while (current != null)
    {
        if (current.SettingsScope != null)
        {
            var value = current.SettingsScope.Get(key);
            if (value != null) return (T)value;  // Finding 1: should be safe cast
        }
        current = current.Parent;
    }
    var defaultValue = Defaults.Get(key);
    if (defaultValue != null) return (T)defaultValue;
    return classDefault;
}
```

Simple, readable, 18 lines. Walks the hierarchy, falls through. The only issue is the hard cast — a one-line fix.

## Files analyzed
- `PLang/App/Engine/Settings/` (ISettings.cs, this.cs, Scope.cs, ModuleView.cs)
- `PLang/App/actions/archive/` (Settings.cs, types.cs)
- `PLang/App/Engine/this.cs` (diff), `PLangContext.cs` (diff), `Goal/Methods.cs` (diff), `GlobalUsings.cs` (diff)
- `PLang.Tests/App/Engine/Settings/` (3 test files)
- `Tests/App/Settings/SetMaxGzipSize/Start.test.goal`
