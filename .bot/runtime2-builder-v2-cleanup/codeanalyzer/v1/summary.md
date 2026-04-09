# v1 Summary — Code Analysis of runtime2-builder-v2-cleanup

## What this is
Full 5-pass code analysis of Ingi's cleanup branch covering pieces 1-4 (identity, crypto, signing, http) plus engine infrastructure changes. ~244 files, ~24K lines changed.

## What was done
Analyzed all changed C# files across 5 passes: OBP compliance, simplification, readability, behavioral reasoning, and deletion testing.

**Key findings:**

1. **Engine.Channels disposal gap** (Medium) — Engine creates `Channels = new EngineChannels(this)` but never disposes it. Actors dispose their own Channels, but the engine-level instance leaks. Not a regression, but should be fixed. File: `Engine/this.cs:330-376`.

2. **Data.Name public setter** (Low-Medium) — Changed from init-only to `{ get; set; }` for identity rename flow. But Data objects are keyed by Name on Variables — public setter allows Name/key divergence. Recommend `internal set`. File: `Data.cs:76`.

3. **Test coverage gaps** (Medium) — 464 lines of new code with zero direct tests:
   - `PlangSerializer` (94 lines) — new serializer, no tests
   - `DefaultAssertProvider` (159 lines) — comparison logic with numeric coercion, no direct tests
   - `DefaultFileProvider` (211 lines) — 7 methods with multiple error paths, minimal tests

4. **`Data.Clone()` is dead code** (Low) — Defined but zero callers. 15 lines that could be deleted.

5. **`__condition__` removal verified safe** — New approach checks `stepResult.Value is bool` directly, gated by `IsConditionStep()`. Cleaner and correct.

**No OBP violations found.** The [Provider] pattern, Data-typed params, event consolidation, and file provider extraction all follow OBP correctly.

## Code example
The dominant pattern across all modules — before and after:

```csharp
// Before: manual provider resolution in every handler
public async Task<Data> Run()
{
    var provider = Context.Engine.Providers.Get<IIdentityProvider>();
    if (!provider.Success) return provider;
    return await provider.Value!.GetAsync(this);
}

// After: [Provider] attribute, source generator wires it
[Provider]
public partial IIdentityProvider Identity { get; }
public async Task<Data> Run() => await Identity.GetAsync(this);
```

## Recommendation
Send to **tester** for coverage analysis on the three untested providers. The Channels disposal and Data.Name setter can go to the coder as a quick fix.
