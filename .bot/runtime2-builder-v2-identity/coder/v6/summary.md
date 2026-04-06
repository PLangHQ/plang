# v6 Summary — Auditor Findings: Unhandled Exception, Export Consistency, Envelope Filter

## What this is

Address 3 auditor findings: unhandled exception in lazy resolver, Export/Get behavior divergence, and defense-in-depth SensitivePropertyFilter gap in Data.Envelope.

## What was done

**IdentityData.cs** — Added try/catch around `GetOrCreateDefaultAsync` in `ResolveDefault()`. Returns null on `InvalidOperationException` — IdentityData already handles null Value gracefully.

```csharp
private IdentityVariable? ResolveDefault()
{
    try
    {
        return IdentityVariable.GetOrCreateDefaultAsync(_engine).GetAwaiter().GetResult();
    }
    catch (InvalidOperationException)
    {
        return null;
    }
}
```

**export.cs** — Replaced inline `LoadAll + Find(IsDefault)` with `GetOrCreateDefaultAsync` for null-name path. Now consistent with `Get.Run(null)` — both promote/auto-create.

**Data.Envelope.cs** — Added `SensitivePropertyFilter.Filter` to `_envelopeJsonOptions` via `DefaultJsonTypeInfoResolver.Modifiers`. Defense-in-depth: if `Compress()` ever handles identity data, private keys won't leak.

**IdentityHandlerTests.cs** — Updated `Export_NullName_NoDefault_ReturnsError` → `Export_NullName_AutoCreatesLikeGet` — Export(null) now auto-creates, so test expects success.

## Files modified

- `PLang/App/modules/identity/IdentityData.cs`
- `PLang/App/modules/identity/export.cs`
- `PLang/App/Engine/Memory/Data.Envelope.cs`
- `PLang.Tests/App/Modules/identity/IdentityHandlerTests.cs`

## Verification

All 1649 tests pass.
