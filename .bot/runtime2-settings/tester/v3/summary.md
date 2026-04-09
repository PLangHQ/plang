# Tester v3 Summary — runtime2-settings

## What this is
Test quality review of coder v3 fixes for auditor/security findings. Coder v3 added `Scope.Clone()` for deep-copy isolation, updated `PLangContext.Clone()`, and narrowed the bare `catch` in `Cast<T>`.

## Test run
- C# tests: **1265 pass, 0 fail, 0 skipped** (up from 1262)
- PLang tests: still not runnable (deferred)

## Resolution of auditor findings

### Auditor #1 (Major: Clone shares Scope by reference) — RESOLVED
`Scope.Clone()` creates a new `ConcurrentDictionary` from the existing one. `PLangContext.Clone()` uses `SettingsScope?.Clone()`. Test `Clone_CreatesIndependentCopy` verifies bidirectional isolation (write to clone doesn't affect original AND write to original doesn't affect clone). Test `Clone_WritesToClone_DoNotAffectOriginal` verifies the exact auditor scenario at the context level. Both honest tests — would fail if reverted to reference copy.

### Auditor #4 / Security #3 (Nit: bare catch) — RESOLVED but introduced regression path
Changed to `catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)`. This is better hygiene — critical exceptions now propagate. **However, this introduced a new gap** — see Finding 1 below.

## New findings

### Finding 1: MAJOR — Cast<T> narrowed catch misses ArgumentException from Enum.ToObject

**File:** `PLang/App/Settings/this.cs:51,54`

`Enum.ToObject(target, value)` throws `ArgumentException` when the value is not a valid underlying type (SByte/Int16/Int32/Int64/Byte/UInt16/UInt32/UInt64). A string like `"Fastest"` is not in that list.

The narrowed catch filters only `InvalidCastException | FormatException | OverflowException` — it does NOT catch `ArgumentException`. So:

```csharp
// PLang: "set compression level to fastest"
// Builder stores "fastest" as string parameter
engine.Settings.Set("archive.level", "fastest", context);

// Later:
var level = engine.Settings.Resolve("archive.level", context, CompressionLevel.Optimal);
// → Enum.ToObject(typeof(CompressionLevel), "fastest")
// → throws ArgumentException
// → NOT caught by the when filter
// → unhandled crash!
```

**This is a regression.** The bare `catch` in v2 would have caught `ArgumentException` and returned the fallback. The narrowing in v3 accidentally broke the string→enum path.

**No test covers this.** `Resolve_WidensIntToEnum` tests `int` → enum (works). No test for `string` → enum.

**Impact:** If the PLang builder stores an enum setting as a string (plausible — "fastest" is natural language), Resolve crashes instead of falling back to the default.

**Suggestion:** Add `ArgumentException` to the catch filter:
```csharp
catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
```

Or better — handle string→enum explicitly before `Enum.ToObject`:
```csharp
if (target.IsEnum)
{
    if (value is string s && Enum.TryParse(target, s, ignoreCase: true, out var parsed))
        return (T)parsed;
    return (T)Enum.ToObject(target, value);
}
```

And add a test:
```csharp
[Test]
public async Task Resolve_ConvertsStringToEnum()
{
    var (engine, context) = CreateEngine();
    engine.Settings.Set("archive.level", "Fastest", context);
    var result = engine.Settings.Resolve("archive.level", context, CompressionLevel.Optimal);
    await Assert.That(result).IsEqualTo(CompressionLevel.Fastest);
}
```

### Finding 2: MINOR — Scope.Clone() is shallow copy of values

**File:** `PLang/App/Settings/Scope.cs:38-41`

`clone._values[kvp.Key] = kvp.Value` copies object references, not deep copies. If a setting value is a mutable object (e.g., a `List<string>`), clone and original share that object — mutations through one affect the other.

Currently all settings values are primitives (long, int, CompressionLevel enum), so this is safe. The doc accurately says "independent shallow copy." But if settings values ever become complex objects, this would need revisiting.

**Impact:** None today. Worth noting for future reference.

## Verdict: **needs-fixes**

Finding 1 is a regression — the narrowing of the catch clause introduced a crash path that the previous version handled gracefully. Adding `ArgumentException` to the catch filter (or using `Enum.TryParse` for strings) is a one-line fix.
