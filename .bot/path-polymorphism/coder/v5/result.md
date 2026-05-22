# Coder v5 — result

Addressed all three codeanalyzer v2 findings. Build clean; C# **2882 / 2882**
(2881 + 1 new N1 test); PLang `--test` **203 / 203 / 0 stale**.

## N1 — `file.exists` authorization gate restored  *(Medium)*

**Decision (Ingi):** gate it — symmetric with the http scheme, restores
pre-branch behavior.

`FilePath.AsBooleanAsync()` was a context-free `File.Exists || Directory.Exists`
with no `AuthGate`. Now it routes through the already-gated `ExistsAsync()` —
the exact shape `HttpPath.AsBooleanAsync()` uses:

```csharp
public override async Task<bool> AsBooleanAsync()
{
    var existsResult = await ExistsAsync();   // ExistsAsync does AuthGate(Read)
    return existsResult.Success && existsResult.Value is true;
}
```

Both schemes now share one rule: probe via `ExistsAsync`, a denied/errored probe
answers `false`. In-root paths stay free (`IsInRoot()` short-circuits
`Authorize`); only out-of-root existence probes prompt. The duplicated
`File.Exists || Directory.Exists` body is gone — `ExistsAsync` is the single home.

**Test fallout.** Two condition-pipeline tests (`IfExists_PathToExistingFile_*`,
`IfExists_PathToMissingFile_*`) constructed context-less `FilePath`s; `Authorize`
throws without `Context.Actor`. A context-less path isn't a shape production
produces (`path.Resolve` always threads context). Updated both to build in-root,
context-bearing paths — AuthGate auto-grants, the existence assertion is
unchanged. New test `FilePath_AsBooleanAsync_OutOfRoot_DeniedPermission_AnswersFalse`
proves the gate: a genuinely-present out-of-root file answers `false` under a
denying channel.

## N2 — `path.Equals` / `GetHashCode` → `RootComparison`  *(Low)*

Both hard-coded `OrdinalIgnoreCase`; on Linux that makes `/srv/x` and `/SRV/x`
(distinct files) compare equal and hash-collide. Switched to `RootComparison`
(the case-sensitivity rule `Relative` / `IsUnder` / `ValidatePath` already use):

```csharp
@this other => string.Equals(_absolutePath, other._absolutePath, RootComparison),
...
public override int GetHashCode() =>
    StringComparer.FromComparison(RootComparison).GetHashCode(_absolutePath);
```

## N3 — `assert.ResolveTruthy` dedup  *(Low)*

`ResolveTruthy` called `IBooleanResolvable.AsBooleanAsync()` directly,
duplicating `Data.ToBooleanAsync()`'s dispatch. Now an `IBooleanResolvable`
value is routed through `data.ToBooleanAsync()` — single home for the rule.
Plain values still use assert's `IsTruthy` (its string-`"false"` semantics
differ from `Data.ToBoolean` deliberately — not collapsed).

```csharp
private static async Task<bool> ResolveTruthy(data.@this? data)
{
    if (data == null) return false;
    if (data.Value is app.data.IBooleanResolvable)
        return await data.ToBooleanAsync();
    return IsTruthy(data.Value);
}
```

## Files

- `PLang/app/types/path/file/this.Operations.cs` — N1
- `PLang/app/types/path/this.cs` — N2
- `PLang/app/modules/assert/code/Default.cs` — N3
- `PLang.Tests/App/Types/PathTests/HandlerShapeTests.cs` — new N1 test
- `PLang.Tests/App/Modules/condition/DefaultEvaluatorTests.cs` — 2 tests given context
