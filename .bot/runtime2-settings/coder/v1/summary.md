# v1 Summary — Implement Settings method bodies

## What this is
Implements the Settings infrastructure for PLang Runtime2 — a scope-chained settings system that lets modules declare typed configuration (e.g., `archive.max`) resolvable per-goal with inheritance and engine-level defaults.

## What was done
Replaced 6 `NotImplementedException` bodies across 3 files:

| File | Methods | Logic |
|------|---------|-------|
| `Scope.cs` | `Get`, `Set`, `Contains` | One-liner ConcurrentDictionary ops |
| `this.cs` | `Resolve<T>`, `Set`, `For<T>` | Resolve: walks context.SettingsScope → parent chain → Defaults → classDefault. Set: isDefault writes to engine Defaults, otherwise lazy-inits context.SettingsScope. For: derives module prefix from namespace last segment. |
| `ModuleView.cs` | `Resolve<TValue>` | Builds `"{prefix}.{property}"` key, delegates to `Settings.Resolve` |

## Result
- 1254 tests pass, 0 failures (was 1239 pass, 15 fail)
- All 15 settings tests green: 5 Scope + 6 Settings + 4 ModuleView

## Code example

The core resolution logic — scope chain walk:

```csharp
public T Resolve<T>(string key, PLangContext context, T classDefault)
{
    var current = context;
    while (current != null)
    {
        if (current.SettingsScope != null)
        {
            var value = current.SettingsScope.Get(key);
            if (value != null) return (T)value;
        }
        current = current.Parent;
    }

    var defaultValue = Defaults.Get(key);
    if (defaultValue != null) return (T)defaultValue;

    return classDefault;
}
```
