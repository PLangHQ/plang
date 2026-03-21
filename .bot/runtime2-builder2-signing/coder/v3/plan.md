# v3 Plan: AsyncDynamicData + IdentityData fix

## Problem
`IdentityData.ResolveDefault()` uses sync-over-async (`GetAwaiter().GetResult()`) because it's called from a property getter via `DynamicData` (sync `Func<object?>`). When resolution fails, the error is silently swallowed — returns null with no diagnostic.

## Design
Add `AsyncDynamicData` alongside `DynamicData`. This takes `Func<Task<object?>>` and resolves asynchronously. The resolution chain needs async support at each layer.

### Changes

#### 1. `AsyncDynamicData` class (Data.cs)
```csharp
public class AsyncDynamicData : Data
{
    private readonly Func<Task<Data>> _factory;
    private Data? _cached;

    public AsyncDynamicData(string name, Func<Task<Data>> factory, Type? type = null)
        : base(name, null, type) => _factory = factory;

    public async Task<Data> ResolveAsync()
    {
        if (_cached != null) return _cached;
        _cached = await _factory();
        return _cached;
    }

    // Sync fallback — returns cached value if resolved, null otherwise
    public override object? Value => _cached?.Value;
}
```

Key decisions:
- Factory returns `Task<Data>` not `Task<object?>` — so errors flow naturally
- Caches result after first resolution (lazy, load when needed, cache after)
- Sync `Value` returns cached value — safe for serialization/logging after resolution

#### 2. `MemoryStack.GetAsync()`
New async method that resolves `AsyncDynamicData` before returning:
```csharp
public async Task<Data?> GetAsync(string name)
{
    var data = Get(name); // existing sync path
    if (data is AsyncDynamicData async)
        return await async.ResolveAsync();
    return data;
}

public async Task<object?> GetValueAsync(string name)
{
    var data = await GetAsync(name);
    return data?.Value;
}
```

#### 3. Source generator: `__ResolveAsync<T>`
Add async variant that calls `__memoryStack.GetAsync()`. The generated `Run()` wrapper is already async, so the property getters in `*__Generated` can use the async path.

Actually — the property getters are sync. They can't await. The source generator emits code like:
```csharp
get => _set_Data ? _Data! : __Resolve<object>("Data");
```

This is a fundamental constraint: C# property getters are sync. Two options:
- **Option A**: Make `ResolveAsync` part of the `Run()` wrapper — resolve all `AsyncDynamicData` vars upfront before the action runs
- **Option B**: Pre-resolve in `Initialize()` of the generated code

Option A is simpler and explicit. The generated `Run()` already does setup. Add a pre-resolution step that awaits any `AsyncDynamicData` in the memorystack before the action's `Run()` executes. This way, by the time `__Resolve<T>` runs (sync), the `AsyncDynamicData` is already cached and `.Value` returns the resolved value.

#### 4. IdentityData.GetAsync()
Replace `ResolveDefault()` with proper async:
```csharp
public async Task<Data> GetAsync()
{
    if (_resolved && base.Value != null)
        return Data.Ok(base.Value);

    _resolved = true;
    var providerResult = _engine.Providers.Get<IIdentityProvider>();
    if (!providerResult.Success) return providerResult;

    var action = new Get { Context = _engine.Context };
    var result = await providerResult.Value!.GetOrCreateDefaultAsync(action);
    if (result.Success) base.Value = result.Value;
    else Error = result.Error;
    return result;
}
```

Remove the sync `Value` override — `IdentityData` no longer does lazy resolution via property. Resolution happens through `GetAsync()`.

#### 5. Actor.cs:79
Change from:
```csharp
Context.MemoryStack.Put(new DynamicData("MyIdentity", () => engine.System.Identity.Value));
```
To:
```csharp
Context.MemoryStack.Put(new AsyncDynamicData("MyIdentity", async () => await engine.System.Identity.GetAsync()));
```

#### 6. Tests
- Update `IdentityData_ResolveDefault_SaveFails` test to check error via `GetAsync()`
- Add test: `AsyncDynamicData_ResolveAsync_CachesResult`
- Add test: `AsyncDynamicData_ResolveAsync_Error_SurfacesError`
- Add test: `MemoryStack_GetAsync_ResolvesAsyncDynamicData`

## Open question
The source generator approach (Option A — pre-resolve in generated `Run()`) means we need to identify which vars are `AsyncDynamicData` and await them. The simplest: in the generated `Run()` wrapper, before calling the action's `Run()`, call a helper that scans parameters for `%var%` references, looks them up, and if any are `AsyncDynamicData`, awaits them. After that, the sync `__Resolve<T>` works because `.Value` returns the cached result.

## Files
- `PLang/Runtime2/Engine/Memory/Data.cs` — add `AsyncDynamicData`
- `PLang/Runtime2/Engine/Memory/MemoryStack.cs` — add `GetAsync`, `GetValueAsync`
- `PLang/Runtime2/modules/identity/IdentityData.cs` — `GetAsync()`, remove sync hack
- `PLang/Runtime2/Engine/Context/Actor.cs` — use `AsyncDynamicData`
- `PLang.Generators/LazyParamsGenerator.cs` — pre-resolve async vars in generated Run()
- Tests
