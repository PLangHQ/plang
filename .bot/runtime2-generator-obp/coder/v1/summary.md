# Coder v1 — Phase 0–6 of v4 (resolution lives in `Data.As<T>`)

## What this is

Implementation of the architect's v4 plan + completion of test-designer's 139 stub tests. The contract change: `Data.Value` is raw and side-effect-free; resolution happens in `Data.As<T>(context)` per call. Generator restructured into a folder hierarchy. Build-time diagnostic enforces "Data&lt;T&gt; or [Provider] or [VariableName] string" property kinds.

## What was done

Six phases landed across six commits on `runtime2-generator-obp`:

1. **Phase 0** — wired `PLang.Generators` analyzer reference into `PLang.Tests.csproj`; built ~28 matrix handler stubs in `App.modules.matrix.*` namespace; built `MatrixRunner` fixture under `PLang.Tests/App/Fixtures/`. Added `global::App.*` prefix to all generator emissions (PLang.Tests's `global using App = App.@this` was shadowing the namespace).

2. **Phase 1** — added `Action.GetParameter(name, context)` to `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs`. Pure lookup walking Parameters → Defaults → `Data.NotFound`. `GetParameterTests.cs` (6 tests) bodies filled, all green.

3. **Phase 2** — the contract change. Rewrote `Data.As<T>(context)` as the resolution entry point with substitution running BEFORE fast paths so `T=object` doesn't short-circuit. Action-destination carve-out preserves raw `%var%` inside sub-action templates. `Data.Value` is raw. Deleted `_resolved`, `_rawValue`, `NeedsResolution`, `ResetResolution()`, `IsDeferredActionTemplate()`, `Variables.ResolveDeep`, `OnResolveTrace`, and supporting state. Generator stops emitting `data.ResetResolution()`. Plain `Data.@this` property emission resolves via `As<object>(Context)` so non-typed Data behaves like `Data<object>`. **Zero real production breakage** — all `.Value`-expects-resolution callers were already routed through generated property getters that route through `As<T>`.

4. **Phase 3** — moved scaffolding (callstack push/pop, save/restore Context.Step/Goal/Event, try/catch/finally with ServiceError translation, frame.SnapshotVariables in finally) from generated `ExecuteAsync` into `App.Run`. `ICodeGenerated.SnapshotParams()` exposes per-property snapshot to App.Run's catch path. Generated `ExecuteAsync` is now thin: marker init, eager `[Provider]` resolution, backing-field reset, validation, `return await Run()`. Generator stamps `__<Name>_set` in Data&lt;T&gt; getters so `__SnapshotParams` reports `WasAccessed` correctly.

5. **Phase 4** — restructured `PLang.Generators/` from a single 711-line file into a folder hierarchy:
   ```
   PLang.Generators/
   ├── this.cs                          (orchestration, 47 lines)
   ├── IsExternalInit.cs                (netstandard2.0 records polyfill)
   ├── Discovery/this.cs                (predicate + GetActionClassInfo + factory + diagnostic)
   └── Emission/
       ├── Action/this.cs               (per-handler emission)
       └── Property/
           ├── this.cs                  (abstract record ActionProperty)
           ├── Data/this.cs             (DataProperty)
           ├── Provider/this.cs         (ProviderProperty)
           └── Legacy/this.cs           ([VariableName]/raw-scalar emission)
   ```
   `ActionProperty` exposes `EmitProperty(StringBuilder)` and `EmitSnapshotEntry(StringBuilder)`. Records are value-equal — no `IPropertySymbol` leaks, so Roslyn's incremental cache works correctly.

6. **Phase 5** — enabled `PLNG001` build-time diagnostic. Discovery rejects partial properties that aren't `Data<T>`/plain Data, `[Provider]`-attributed, or `[VariableName]`-attributed string. Diagnostic flows through the IIncrementalGenerator pipeline as a value-equal `DiagnosticInfo` record. Architect's plan called for fully deleting `[VariableName]`; deferred — see "Decisions and divergences" below.

7. **Phase 6** — cleanup audit. Verified no leftover `_resolved`/`_rawValue`/`NeedsResolution`/`ResetResolution`/`ResolveDeep`/`OnResolveTrace`/`IsDeferredActionTemplate` references. Updated stale comment on `RawValue`. PlangConsole builds; full PLang test suite green.

## Final state

- **Tests:** 2427/2427 passing (+ 139 from test-designer all green).
- **Generator:** 8 files, 809 total lines (vs previous 711 in one file).
- **Build-time check:** PLNG001 fires on raw-scalar partials. Manual smoke test confirmed the diagnostic fires; existing handlers compile cleanly because all 22 raw-scalar partials are `[VariableName]`-attributed.

## Code example

The architectural sharpening in `Data.As<T>`:

```csharp
public @this<T> As<T>(Actor.Context.@this? context = null)
{
    var ctx = context ?? _context;
    var raw = Value;            // raw — no %var% substitution
    return AsT_Impl<T>(raw, ctx);
}

private @this<T> AsT_Impl<T>(object? raw, Actor.Context.@this? ctx)
{
    if (IsActionDestination(typeof(T))) return ConvertAndWrap<T>(raw, ctx);

    // Substitute first — before fast paths — so T=object doesn't short-circuit.
    if (raw is string strVal && strVal.Contains('%') && ctx?.Variables != null)
    {
        var fullMatch = Regex.Match(strVal, @"^%([^%]+)%$");
        if (fullMatch.Success)
        {
            var resolved = ctx.Variables.Get(fullMatch.Groups[1].Value);
            if (resolved == null || !resolved.IsInitialized) return ConvertAndWrap<T>(null, ctx);
            if (!resolved.Success) return @this<T>.FromError(resolved.Error!);
            return AsT_Impl<T>(resolved.Value, ctx);   // recurse on the variable's value
        }
        return AsT_Impl<T>(ctx.Variables.Resolve(strVal), ctx);
    }

    if (raw is IList<object?> objList && ctx != null)  return ConvertAndWrap<T>(WalkList(objList, ctx), ctx);
    if (raw is IDictionary<string, object?> dict && ctx != null) return ConvertAndWrap<T>(WalkDict(dict, ctx), ctx);

    if (this is @this<T> typed && typed._value is T) return typed;     // already typed
    if (raw is T already) return new @this<T>(Name, already, _type, Parent) { Context = ctx };
    // … T.Resolve(string, Context) for Path-style domain types …
    return ConvertAndWrap<T>(raw, ctx);
}
```

The same pattern for `App.Run` scaffolding:

```csharp
public async Task<Data.@this> Run(Action.@this action, Context.@this context)
{
    var (handler, error) = Modules.GetCodeGenerated(action);
    if (error != null) return Data.@this.FromError(error);

    var step = action.Step;
    var callFrames = context.CallStack?.GetFrames() ?? (IReadOnlyList<CallFrame>)Array.Empty<CallFrame>();
    var frame = context.CallStack?.Push(action);
    var prevStep = context.Step; var prevGoal = context.Goal; var prevEvent = context.Event;
    context.Step = action.Step; context.Goal = action.Step?.Goal;

    try
    {
        var result = await handler!.ExecuteAsync(action, context);
        if (!result.Success && result.Error is Errors.Error err && err.Params == null)
            err.Params = handler.SnapshotParams();
        return result;
    }
    catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
    {
        var serviceErr = new Errors.ServiceError(ex.Message, step, callFrames, "ServiceError", 400) { Exception = ex };
        serviceErr.Params = handler!.SnapshotParams();
        return Data.@this.FromError(serviceErr);
    }
    finally
    {
        frame?.SnapshotVariables(context.Variables);
        if (context.CallStack != null) await context.CallStack.PopAsync();
        context.Step = prevStep; context.Goal = prevGoal; context.Event = prevEvent;
    }
}
```

## Decisions and divergences

**`[VariableName]` is kept, not deleted.** Architect's v4 plan Step 5.5 called for "delete `[VariableName]` attribute and class." That's not done. The reason: `variable.set` and `list.*` handlers need the variable's *name* (not its value) — a first-class concept. After `As<T>(Context)`, the resulting Data carries the parameter property's `Name` (e.g., `"list"`), not the variable's name (e.g., `"products"`). `[VariableName]` is the cleanest expression of "I want the name, not the value." The PLNG001 diagnostic recognizes `[VariableName] string` as a valid property kind alongside `Data<T>` and `[Provider]`. Folding this into `As<T>` would be a contract change worth its own design pass; logged in `claude-md-proposals.md`.

**Step.RunAsync's "StepError" semantic shifted.** `App.Run` now owns the catch path for handler exceptions, translating to `ServiceError`. Previously, exceptions from handlers without try/catch (e.g., test-suite `ThrowingHandler`) bubbled to `Step.RunAsync`'s catch and surfaced as `StepError`. After Phase 3, they surface as `ServiceError`. Test `StepRunAsync_ExceptionInHandler_ReturnsError` updated. `Step.RunAsync`'s catch still exists for non-handler failures (event-handler exceptions, iteration logic).

**Generator `global::` prefix.** All generator emissions now use `global::App.*` qualified names, not bare `App.*`. The trigger was PLang.Tests's `global using App = App.@this;` shadowing the `App` namespace at any test-namespace scope. Treating it as a generator-side fix (rather than touching the global using) keeps the generator's emitted code immune to consumer-side aliases.

## What's still in progress / what to do next

Architect's plan is fully delivered except for the `[VariableName]` deletion (deferred, documented). Recommend:
- **codeanalyzer review** for OBP compliance + simplification opportunities, especially the new `Discovery/this.cs` and `Emission/Action/this.cs`.
- **auditor review** for any contract gaps the matrix didn't cover.
- A future round to either fold `[VariableName]` into `As<T>` (preserve variable name on full-match) or formally accept it as a permanent third property kind.

## Files

**Created:**
- `PLang.Generators/this.cs`, `Discovery/this.cs`, `Emission/Action/this.cs`, `Emission/Property/this.cs`, `Emission/Property/Data/this.cs`, `Emission/Property/Provider/this.cs`, `Emission/Property/Legacy/this.cs`, `IsExternalInit.cs`
- `PLang.Tests/App/Fixtures/MatrixRunner.cs`
- 11 matrix handler files under `PLang.Tests/Generator/Matrix/<Group>/Handlers.cs`

**Modified:**
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — added `GetParameter`
- `PLang/App/Data/this.cs` — `As<T>` rewrite, `Value` becomes raw, deleted state cluster
- `PLang/App/Variables/this.cs` — deleted `ResolveDeep` and supporting state
- `PLang/App/this.cs` — `App.Run` gained scaffolding
- `PLang/App/Debug/this.cs` — dropped `ResolveTrace` + `OnResolveTrace` subscription, dropped `NeedsResolution` from log
- `PLang/App/modules/ICodeGenerated.cs` — added `SnapshotParams()` interface method
- `PLang.Tests/PLang.Tests.csproj` — analyzer ref + EmitCompilerGeneratedFiles
- `PLang.Tests/App/Core/EngineTests.cs` — `StepError` → `ServiceError` for handler-exception test
- `PLang.Tests/App/VariablesTests/VariablesTests.cs` — removed obsolete `ResolveDeep` tests
- 18 test files under `PLang.Tests/App/DataTests/`, `PLang.Tests/Generator/`, `PLang.Tests/Generator/Matrix/` — bodies filled

**Deleted:**
- `PLang.Generators/LazyParamsGenerator.cs` (replaced by the folder hierarchy)
