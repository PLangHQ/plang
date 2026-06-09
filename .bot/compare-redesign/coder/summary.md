# Coder — compare-redesign

## Version: v5 (FIRST IMPLEMENTATION — v1–v4 were reviews). Sprint mode: build RED across 2→6.

Ingi's call this session: **full sprint, build red mid-flight** — cut the door, hold the build red,
checkpoint each session until 2→6 lands green. (I started by writing a hand-off doc instead of
cutting code; Ingi corrected that — coder cuts code.)

### Landed & GREEN (committed)
- **Stage 1** — `Comparison` enum (`PLang/app/data/Comparison.cs`); test green.
- **Stage 2 `Peek()`** — `ScalarValue` → `Peek()` method; ~20 sites.

### Landed, build RED (committed) — the architectural core is DONE
- **The async door** (`PLang/app/data/this.cs`):
  - `public virtual ValueTask<object?> Value()` — the single public read door (sync-completing in
    memory; async read lands in Stage 3 for references). **No public sync `.Value` property.**
  - `internal virtual object? Materialize()` — the sync in-memory core (factory + parse-rung +
    cache). The sync surfaces that genuinely can't `await` (serialization, `ToString`, build-time)
    read through here. `DynamicData` overrides it (recompute). `ParseRaw()` = inner registry parse.
  - `public virtual void SetValue(object?)` — the write side (was the `Value` setter).
  - `Data<T>.Value()` typed async (`new`); `Peek()` unchanged.
  - **OBP note (Ingi caught this):** do NOT name the sync read `CurrentValue()`/`Materialized()` —
    that's verb+noun / a noun-twin of `Value` (smell #4). The sync read is the materialization
    **verb** `Materialize()` (internal plumbing); the one public door is `Value()`.
- **Source generator** (`PLang.Generators/Emission/{Property/Data,Property/Code,Action}/this.cs`):
  emits `Peek()` for param-slot diagnostics + presence guards, `Materialize()` for `[Code]` service
  injection. **This collapsed all 956 generated errors to 0.**
- **8 files migrated** as the proven reference set: `variable/set.cs`, `file/read.cs`,
  `list/{contains,any,group,join}.cs`, `variable/navigator/{List,Dictionary}.cs` (navigation reads `Materialize()` — INavigator stays sync this pass).

### Error burn-down
`2130 → 1068` (all 956 generated gone; ~90 hand-written done). The compiler error list IS the
remaining worklist: `dotnet build PLang 2>&1 | grep "error CS"` — ~1068 sites across ~91 handler
files, every one a Data-receiver `.Value` (views keep their sync `.Value`, so they never error).

### THE RECIPE for the remaining ~93 files (mechanical, proven)
Per handler:
1. `Run()`/method → `async`. Resolve each param door **once** at the top: `var x = await Param.Value();`
   (await-once; don't read `Param.Value()` twice).
2. **Guard-reorder** (only where a param is guarded): the `if (!Param.Success) return Param;` moves
   **after** `await Param.Value()` — pre-await it inspects an unresolved Data and stops catching
   bad-scheme/unset-`%var%`/convert errors. Pattern: `var p = await Path.Value(); if (!Path.Success) return Path; …use p…`. (`file/read.cs` is the worked example.)
3. `return Task.FromResult(X)` → `return X` (method is now async).
4. `Data.Value is/as T` → `Data.Materialize() is/as T` (sync surfaces); per-item in a `foreach` →
   `await item.Value()`.
5. Build-time / static-sync methods (e.g. `ValidateBuild`) can't `await` → `Materialize()`.
6. A `Data<bool.@this>` truthiness read: `(await X.Value())?.Value == true`.
NOTE: `GetChild`/navigation is still **sync** in this pass — leaving navigation-async (`ValueTask`
nav chain) as the next sub-step once the call sites compile. Do NOT touch the to-be-deleted mediator
(`Compare.cs`/`ScalarComparer`/`Operator.NormalizeTypes`) beyond making it compile — it dies in Stage 6.

### Code example — the recipe (file/read.cs)
```csharp
public async Task<data.@this> Run()
{
    var path = await Path.Value();
    if (!Path.Success) return Path;          // guard AFTER the await
    var channel = new …file.@this(path!);
    …
    if ((await ResolveVariables.Value())?.Value == true && await read.Value() is string content) …
}
```

### Next
Continue the grind: `dotnet build PLang`, work the `error CS` list file-by-file with the recipe,
commit periodically (red is fine). When PLang compiles, do navigation-async, then Stages 3–6, then
land green at the 2→6 boundary. Both C# and `.goal` tests are the deliverable once green.
