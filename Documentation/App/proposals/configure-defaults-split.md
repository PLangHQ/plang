# Configure Actions: Settings.Apply and the Defaults Question

## Status

**Settings.Apply — implemented.** Shipped on `runtime2-builder-v2-http`.
**Defaults split — deferred.** Noted as a future consideration, not needed now.

## Background

Configure actions (e.g., `http.configure`) had a manual if-chain in every provider:

```csharp
// Before: 8 if-statements with hardcoded string keys per module
if (action.TimeoutInSec.HasValue)
    engine.Settings.Set("http.TimeoutInSec", action.TimeoutInSec.Value, action.Context, isDefault);
if (action.BaseUrl != null)
    engine.Settings.Set("http.BaseUrl", action.BaseUrl, action.Context, isDefault);
// ... repeat for every property
```

Every module with a configure action would need the same boilerplate. Adding a property means updating three places: the action record, Config, and the if-chain.

## What We Shipped: Settings.Apply

`Settings.Apply<TConfig>()` replaces the if-chain with reflection. It reads `action.Parameters` (the raw list from the .pr file) and writes only those to the scope chain, matching against `TConfig` property names:

```csharp
// After: one line, works for any module
engine.Settings.Apply<Config>(action, action.Context, action.Default);
```

This solves:
- **Partial updates** — only properties in `Parameters` get written. Step 1 sets `baseUrl`, step 2 sets `timeout` — they don't overwrite each other.
- **Null ambiguity** — if `baseUrl` is in `Parameters` with value `null`, the developer set it to null. If it's not in `Parameters`, the developer didn't mention it.
- **Boilerplate** — one line per module instead of N if-statements.

The key insight: the LLM should only output properties the developer mentioned. `action.Parameters` already contains exactly the developer's intent. We just weren't using it — the provider was reading the resolved record properties (where everything has a value) instead of the raw parameter list.

## Two Classes: configure + Config

The action record (`configure`) and the settings class (`Config`) are intentionally separate:

- **`configure`** — the LLM's output. What the developer asked for. Nullable types = partial update.
- **`Config`** — C# truth. Default values, property shapes, type key for `Settings.For<T>()`.

The LLM maps developer intent. C# owns defaults. They look similar but serve different roles.

## Future Consideration: Build-Time Default Pinning

There's an open question about determinism: if a .pr file is built against a runtime where `TimeoutInSec` defaults to 30, and a later runtime changes it to 60, should the old .pr still use 30?

One approach discussed: store build-time defaults in the .pr file as a separate `"defaults"` section. The LLM outputs only developer-set values in `"parameters"`, the builder C# code fills everything else into `"defaults"` via reflection on the `IConfig` class.

**Decision: deferred.** Runtime default changes in PLang are intentional — if you update the runtime, apps pick up new behavior. Pinning build-time defaults creates shadow versioning. The complexity (new Action property, 3-tier source generator resolution, builder changes) isn't justified until there's a concrete problem.

If this ever becomes needed, the design is documented here. The key constraint: it requires builder C# code that reflects on App `IConfig` classes, so it's blocked until the builder migrates to App anyway.
