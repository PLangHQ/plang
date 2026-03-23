# Proposal: Parameters/Defaults Split for Configure Actions

## Problem

Configure actions (e.g., `http.configure`) need to distinguish between "developer set this value" and "builder filled in the default." Currently both end up in `Action.Parameters` — there's no way to tell them apart at runtime.

This matters because:

1. **Partial updates break.** If a developer writes two configure steps:
   ```
   - configure http, base url http://api.example.com
   - configure http, timeout 60
   ```
   The second step's .pr has `baseUrl` set to its default (null or the class default). When the provider writes all parameters to the settings scope, it overwrites step 1's base url.

2. **Null is ambiguous.** C# has no `undefined`. A null `BaseUrl` could mean "developer didn't mention this" or "developer explicitly set it to null." We can't tell.

3. **Runtime default changes leak.** If `Config.TimeoutInSec` changes from 30 to 60 in a new runtime version, already-built .pr files should still use 30. But if defaults aren't stored in the .pr, old software silently picks up the new default.

## Solution: Split Parameters and Defaults in .pr Files

The LLM outputs **only what the developer mentioned** in `parameters`. The builder C# code (deterministic, not LLM) fills in everything else as `defaults` by reflecting on the module's `ISettings` class.

### .pr Format

```json
{
  "module": "http",
  "action": "configure",
  "parameters": [
    { "name": "baseUrl", "value": "http://api.example.com" }
  ],
  "defaults": [
    { "name": "timeoutInSec", "value": 30 },
    { "name": "contentType", "value": "application/json" },
    { "name": "encoding", "value": "utf-8" },
    { "name": "unsigned", "value": false },
    { "name": "followRedirects", "value": true },
    { "name": "maxRedirects", "value": 10 }
  ]
}
```

- **`parameters`**: Developer's intent. Written by the LLM. Only properties the developer mentioned.
- **`defaults`**: Build-time snapshot of all other properties. Written by builder C# code via reflection on the `ISettings` class. Captures the default values at build time for determinism.

### How It Solves Each Problem

**Partial updates:** `Settings.Apply` only writes `parameters` to the scope chain. `defaults` are never written to settings. Step 2 writes `timeoutInSec=60`, step 1's `baseUrl` is untouched.

**Null ambiguity:** If `baseUrl` is in `parameters` with value `null`, the developer explicitly set it to null. If it's in `defaults` with value `null`, the developer didn't mention it. Clear distinction.

**Runtime default stability:** The .pr stores `"timeoutInSec": 30` in `defaults`. Even if the runtime's `Config.TimeoutInSec` changes to 60 later, the source generator resolves from `defaults` first, so existing software stays on 30.

## Runtime Changes

### Action: Add `Defaults` property

```csharp
// In Action/this.cs
public List<Data>? Defaults { get; init; }
```

### Source Generator: 3-tier resolution

Property getter resolution order:
1. `Action.Parameters` — developer-set values
2. `Action.Defaults` — build-time defaults from .pr
3. `[Default]` attribute — source code fallback (for actions without .pr defaults, e.g., called via `engine.RunAction`)

### Settings: Add `Apply` method

```csharp
// In Settings/this.cs
public void Apply<TConfig>(Action action, PLangContext context, bool isDefault = false)
    where TConfig : ISettings, new()
{
    var prefix = ResolvePrefix<TConfig>();
    var configProps = typeof(TConfig).GetProperties()
        .Select(p => p.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Only write Parameters (developer intent), not Defaults
    foreach (var param in action.Parameters)
    {
        if (configProps.Contains(param.Name))
            Set($"{prefix}.{param.Name}", param.Value, context, isDefault);
    }
}
```

This replaces the manual if-null-set chain in every provider's `Configure` method:

```csharp
// Before: 8 if-statements with hardcoded string keys
if (action.TimeoutInSec.HasValue)
    engine.Settings.Set("http.TimeoutInSec", action.TimeoutInSec.Value, action.Context, isDefault);
if (action.BaseUrl != null)
    engine.Settings.Set("http.BaseUrl", action.BaseUrl, action.Context, isDefault);
// ... 6 more

// After: one line, works for any module
engine.Settings.Apply<Config>(action, context, action.Default);
```

### GoalMapper: Map `defaults` from .pr

```csharp
// When creating Action from .pr JSON
new Action
{
    Module = ...,
    ActionName = ...,
    Parameters = MapParameters(prAction.Parameters),
    Defaults = MapParameters(prAction.Defaults),  // new
    Return = ...
}
```

## Builder Changes

### Builder C# Code (ApplyStep or similar)

After the LLM returns the action with parameters, the builder fills in defaults:

```csharp
// Pseudo-code in the builder pipeline
void FillDefaults(PrAction action)
{
    var settingsType = FindSettingsType(action.Module, action.Action); // e.g., http.Config
    if (settingsType == null) return; // not a configure action

    var instance = Activator.CreateInstance(settingsType); // Config with default values
    var paramNames = action.Parameters.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    action.Defaults = new List<PrParameter>();
    foreach (var prop in settingsType.GetProperties())
    {
        if (paramNames.Contains(prop.Name)) continue; // already in parameters
        action.Defaults.Add(new PrParameter
        {
            Name = prop.Name,
            Value = prop.GetValue(instance),
            Type = prop.PropertyType.FullName
        });
    }
}
```

### Builder Prompt

Add a rule: for configure actions, only include properties the developer explicitly mentioned. Don't fill in defaults — the builder pipeline handles that deterministically.

## Scope

This proposal is **not HTTP-specific**. Any module with a configure action and an `ISettings` class benefits:
- `http.configure` + `http.Config`
- Future: `db.configure` + `db.Config`, `cache.configure` + `cache.Config`, etc.

The pattern is: LLM handles intent mapping (what the developer said), C# handles defaults (deterministic snapshot).

## What Doesn't Change

- `Config` / `ISettings` classes — still define defaults and property shapes
- `ModuleView<T>` — still resolves from scope chain
- `Settings.Resolve` — still walks context → parent → defaults → class default
- Scope chain mechanics — unchanged
- Reading side — `engine.Settings.For<Config>(context).Resolve("TimeoutInSec", 30)` works exactly as before

## Open Questions

1. **Should `defaults` apply to all actions, not just configure?** Every action could benefit from .pr-stored defaults for determinism. The source generator's 3-tier resolution (Parameters → Defaults → [Default] attribute) is generic.

2. **Context lifetime on configure actions.** The `configure` action has `IContext` which carries `PLangContext`. If the action object is stored in settings, the context from the goal that ran configure leaks into later requests (e.g., webserver startup goal's context persists into web requests). Current solution: `Settings.Apply` reads from the action but doesn't store the action object — it writes individual values to the scope chain.

3. **Builder migration.** The builder currently runs on v1 runtime. This proposal requires builder C# code that reflects on Runtime2 `ISettings` classes. This may be blocked until the builder migrates to Runtime2. In the interim, the manual if-chain in providers works — it's just boilerplate.
