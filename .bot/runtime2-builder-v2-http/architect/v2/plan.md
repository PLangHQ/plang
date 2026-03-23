# Build-Time Defaults Pinning

## Problem

A built `.pr` file must reproduce the same behavior regardless of runtime version. Today, when the source generator resolves an action property that isn't in `Action.Parameters`, it falls back to the `[Default]` attribute — which lives in source code and changes with runtime updates.

Example: if `request`'s `[Default(false)]` on `Unsigned` changes to `[Default(true)]` in a future runtime, every already-built `.pr` that doesn't explicitly set `Unsigned` silently switches behavior. Same risk for hashing algorithms, timeouts, encoding — any default.

## Solution

Add `Defaults` to `Action`. The builder fills it at build time by reflecting on the action record. The source generator resolves: **Parameters → Defaults → `[Default]` attribute**.

- **Parameters** — what the developer said (LLM output)
- **Defaults** — build-time snapshot of everything else (builder C# code, deterministic)
- **`[Default]` attribute** — source code fallback for programmatic `engine.RunAction` calls that have no `.pr`

## Changes

### 1. Action: add `Defaults` property

**File:** `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/Action/this.cs`

```csharp
[Store, Debug, Default]
public List<Data>? Defaults { get; init; }
```

No `[LlmBuilder]` — the LLM never sees or writes defaults. Same `List<Data>` shape as `Parameters`.

### 2. Source generator: 3-tier resolution

**File:** `PLang.Generators/LazyParamsGenerator.cs`

Add `__defaults` field alongside `__parameters`:

```csharp
private List<PLang.Runtime2.Engine.Memory.Data>? __defaults;
```

Wire it in `CodeGeneratedExecuteAsync` — the engine passes defaults from the action:

```csharp
public async Task<Data> CodeGeneratedExecuteAsync(
    List<Data> parameters, @this engine, PLangContext context,
    List<Data>? defaults = null)
{
    __parameters = parameters;
    __defaults = defaults;
    // ... rest unchanged
}
```

Update `ICodeGenerated` to accept defaults:

```csharp
public interface ICodeGenerated
{
    Task<Data> CodeGeneratedExecuteAsync(
        List<Data> parameters, Engine.@this engine, PLangContext context,
        List<Data>? defaults = null);
}
```

The optional parameter keeps backward compat — existing `engine.RunAction` calls that pass no defaults still work, falling through to `[Default]`.

Change `__Resolve<T>` to check defaults after parameters:

```csharp
private T? __Resolve<T>(string name)
{
    // 1. Check parameters (developer intent)
    var data = __parameters?.FirstOrDefault(
        d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

    // 2. Check defaults (build-time snapshot)
    data ??= __defaults?.FirstOrDefault(
        d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

    // ... rest of resolution (variable interpolation, type conversion) unchanged
}
```

And `__HasParam` becomes `__HasParamOrDefault`:

```csharp
private bool __HasParam(string name)
{
    return (__parameters?.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) ?? false)
        || (__defaults?.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) ?? false);
}
```

The `[Default]` attribute on the property is still the final fallback in the generated getter — it's baked into the source code at compile time. The resolution chain is:

```
__Resolve (parameters → defaults) ?? [Default] attribute value
```

### 3. Engine: pass defaults through

**File:** `PLang/Runtime2/Engine/this.cs`

Update `RunAction` to pass empty defaults (no `.pr` = no build-time defaults):

```csharp
public async Task<Data<TResult>> RunAction<TAction, TResult>(TAction action, PLangContext context)
    where TAction : ICodeGenerated
{
    var result = await action.CodeGeneratedExecuteAsync(new List<Data>(), this, context, defaults: null);
    // ...
}
```

The step execution path (wherever it calls `CodeGeneratedExecuteAsync`) passes `action.Defaults`:

```csharp
// In step execution (wherever actions are dispatched)
await codeGenerated.CodeGeneratedExecuteAsync(action.Parameters, engine, context, action.Defaults);
```

### 4. Configure actions: `IConfigure<TConfig>`

**File:** `PLang/Runtime2/modules/IConfigure.cs` (new)

```csharp
namespace PLang.Runtime2.modules;

/// <summary>
/// Marks a configure action and links it to its IConfig class.
/// The builder uses this to reflect on TConfig for filling defaults
/// instead of reflecting on the action record itself.
/// </summary>
public interface IConfigure<TConfig> where TConfig : Engine.Settings.IConfig, new() { }
```

**File:** `PLang/Runtime2/modules/http/configure.cs` (update)

```csharp
[Action("configure", Cacheable = false)]
public partial class configure : IContext, IConfigure<Config>
{
    // ... unchanged
}
```

### 5. Builder: fill defaults in ValidateActions

**File:** `PLang/Modules/PlangModule/Program.cs`

After validation passes, fill defaults for each action:

```csharp
public async Task<(bool isValid, IError? Error)> ValidateActions(Actions actions)
{
    // ... existing validation ...

    // Fill build-time defaults
    foreach (var action in actions)
        FillDefaults(action);

    return (true, null);
}

private void FillDefaults(ActionData action)
{
    var handlerType = _libraries.GetHandlerType(action.Module, action.ActionName);
    if (handlerType == null) return;

    // Determine which type to reflect on for defaults
    var defaultsSourceType = GetDefaultsSourceType(handlerType);

    var paramNames = action.Parameters
        .Select(p => p.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    action.Defaults = new List<Data>();

    if (defaultsSourceType == handlerType)
    {
        // Regular action: reflect on action record's [Default] attributes
        FillFromAttributes(action, handlerType, paramNames);
    }
    else
    {
        // Configure action: reflect on Config class instance
        FillFromConfigInstance(action, defaultsSourceType, paramNames);
    }
}

/// <summary>
/// If the handler implements IConfigure&lt;TConfig&gt;, return TConfig.
/// Otherwise return the handler type itself.
/// </summary>
private static Type GetDefaultsSourceType(Type handlerType)
{
    var configureInterface = handlerType.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType
            && i.GetGenericTypeDefinition() == typeof(IConfigure<>));

    return configureInterface?.GetGenericArguments()[0] ?? handlerType;
}

/// <summary>
/// Regular actions: read [Default] attributes from partial properties.
/// </summary>
private static void FillFromAttributes(ActionData action, Type handlerType, HashSet<string> paramNames)
{
    foreach (var prop in handlerType.GetProperties())
    {
        if (paramNames.Contains(prop.Name)) continue;

        var defaultAttr = prop.GetCustomAttribute<DefaultAttribute>();
        if (defaultAttr == null) continue;

        action.Defaults.Add(new Data
        {
            Name = prop.Name.ToLowerInvariant(),
            Value = defaultAttr.Value,
            Type = new Memory.Type { Value = prop.PropertyType.Name.ToLowerInvariant() }
        });
    }
}

/// <summary>
/// Configure actions: instantiate Config class, read C# default values.
/// </summary>
private static void FillFromConfigInstance(ActionData action, Type configType, HashSet<string> paramNames)
{
    var instance = Activator.CreateInstance(configType);

    foreach (var prop in configType.GetProperties())
    {
        if (paramNames.Contains(prop.Name)) continue;

        action.Defaults.Add(new Data
        {
            Name = prop.Name.ToLowerInvariant(),
            Value = prop.GetValue(instance),
            Type = new Memory.Type { Value = prop.PropertyType.Name.ToLowerInvariant() }
        });
    }
}
```

### 6. GoalMapper: map defaults from .pr

**File:** `PLang/Runtime2/Engine/Utility/GoalMapper.cs`

When creating `Action` from `.pr` JSON, map the `defaults` array:

```csharp
Defaults = MapParameters(prAction.Defaults),  // same shape as Parameters
```

### 7. Settings.Apply: no change needed

`Settings.Apply` already reads from `action.Parameters` only. Defaults don't leak into the settings scope. This is correct — configure step 1 sets `baseUrl`, step 2 sets `timeout`, neither overwrites the other.

## .pr Format

```json
{
  "module": "http",
  "action": "request",
  "parameters": [
    { "name": "url", "value": "https://api.example.com/users" }
  ],
  "defaults": [
    { "name": "method", "value": "GET", "type": "httpmethod" },
    { "name": "contenttype", "value": "application/json", "type": "string" },
    { "name": "encoding", "value": "utf-8", "type": "string" },
    { "name": "timeoutinsec", "value": 30, "type": "int32" },
    { "name": "unsigned", "value": false, "type": "boolean" }
  ]
}
```

## What Doesn't Change

- `[Default]` attributes on action records — still needed for `engine.RunAction` (no `.pr`)
- `Settings.Apply` — still reads `Parameters` only
- `Settings.Resolve` / `ModuleView<T>` — scope chain unchanged
- `Config` / `IConfig` classes — unchanged
- Existing `.pr` files without `defaults` — work fine, `Defaults` is null, falls through to `[Default]` attribute

## Files to Create/Modify

| File | Change |
|------|--------|
| `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/Action/this.cs` | Add `Defaults` property |
| `PLang/Runtime2/modules/ICodeGenerated.cs` | Add `defaults` parameter |
| `PLang/Runtime2/modules/IConfigure.cs` | New — `IConfigure<TConfig>` interface |
| `PLang/Runtime2/modules/http/configure.cs` | Implement `IConfigure<Config>` |
| `PLang.Generators/LazyParamsGenerator.cs` | 3-tier resolution: `__defaults` field, update `__Resolve`, `__HasParam`, `CodeGeneratedExecuteAsync` |
| `PLang/Runtime2/Engine/this.cs` | Pass `defaults: null` in `RunAction` |
| `PLang/Runtime2/Engine/Utility/GoalMapper.cs` | Map `defaults` from `.pr` |
| `PLang/Modules/PlangModule/Program.cs` | `FillDefaults` in `ValidateActions` |

## Definition of Done

- `Action.Defaults` is populated at build time via `ValidateActions`
- Regular actions: defaults come from `[Default]` attributes on the action record
- Configure actions (`IConfigure<TConfig>`): defaults come from `Config` class instance
- Source generator resolves: Parameters → Defaults → `[Default]` attribute
- `engine.RunAction` (no `.pr`) still works — `defaults` is null, `[Default]` attribute is the fallback
- Existing `.pr` files without `defaults` section still work — null defaults, same fallback
- `Settings.Apply` unchanged — still writes `Parameters` only to scope chain
- Built `.pr` files produce deterministic behavior across runtime versions
