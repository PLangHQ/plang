# Coder v2 — codeanalyzer findings + test gaps + raw-string refactor

## What this is

Response to `codeanalyzer/v1` review (38 findings, NEEDS WORK verdict) plus Ingi's observation that the v1 test set should have caught several of the issues. Ran `plang --test` early in the session — it crashed with `StackOverflowException` in `Data.AsT_Impl`, confirming codeanalyzer's Finding 27 was a real production bug, not theoretical. Made the fix, then worked through the rest of the codeanalyzer's load-bearing flags, plugged the test gaps, and (per Ingi's request mid-session) rewrote the generator's emission code to use C# 11+ raw string literals (Finding 19).

## What was done

Five phases across five commits on `runtime2-generator-obp`:

1. **Phase A** — `Data.AsT_Impl` cycle detection. Thread-static visited-set keyed on the raw `%`-containing string; protects both the full-match (`%a%` → variable's value) and partial-match (`Resolve` returns string still containing `%`) paths. Unblocked `plang --test`. (`PLang/App/Data/this.cs`)

2. **Phase B** — Codeanalyzer top 3:
   - **Finding 1**: `ActionClassInfo` `sealed class` → `sealed record` with `EquatableArray<T>` for collections. `EquatableArray<T>` is a small struct wrapper around `T[]` with sequence-equality `Equals`/`GetHashCode`. The `IIncrementalGenerator` cache now hits when two structurally identical inputs come through successive compilations.
   - **Finding 11**: `__variables` field deleted from emission.
   - **Finding 12**: `__paramData` dict + `ParamData()` accessor + the legacy assignment all deleted.
   - New tests: `IncrementalCacheTests` (9 entries verifying value equality of `ActionClassInfo` and `EquatableArray<T>`) and `NoDeadEmissionTests` (regex over every `*.Action.g.cs` asserting every private field has at least one read; would have flagged `__variables` and `__paramData` automatically).
   - Added a non-analyzer `ProjectReference` to `PLang.Generators` in `PLang.Tests.csproj` so tests can compile-time-import generator types for contract checks.

3. **Phase E** (added mid-session at Ingi's request — Finding 19) — Replaced `sb.AppendLine(...)` cascades in `PLang.Generators/Emission/Action/this.cs` with C# 11+ raw string literals (`$$"""..."""`). The emitted shape now reads top-to-bottom as it would appear in `.g.cs`. Output drift: 5 trivial blank-line differences in a sampled handler. No semantic changes.

4. **Phase C** — Behavioral concerns:
   - **Finding 33**: comment on `App.Run`'s `catch` block explaining why `OperationCanceledException` is deliberately caught (`timeout.after` depends on it; the asymmetry with `Step.RunAsync` — which excludes OCE — is intentional).
   - **Finding 28**: comment on `Data.SubstitutePrimitive` documenting the shape contract (only typed-generic `IList<object?>` / `IDictionary<string, object?>` match; non-generic `ArrayList`/`Hashtable` pass through).
   - New tests: `AppRun_HandlerThrowsOCE_TranslatesToServiceError_DoesNotPropagate` and `OceThrowingHandler` fixture; `AsT_NonGenericArrayList_PassesThroughWithoutSubstitution` and `AsT_NonGenericHashtable_PassesThroughWithoutSubstitution`.

5. **Phase D** — Trivial cleanup (Findings 2, 3, 6, 9, 21):
   - F2: drop the dead `OriginalDefinition.Name == "@this"` disjunct (Roslyn returns the bare identifier).
   - F3: extract the triple `INamedTypeSymbol` cast into a single named local.
   - F6: `RawScalarPropertyDescriptor` `public` → `internal`.
   - F9: rename `LazyParamsGenerator` class → `@this` (matches OBP `this.cs` convention). Generated folder moves from `PLang.Generators.LazyParamsGenerator` to `PLang.Generators.this`; updated three test files reading the path.
   - F21: drop the redundant `({Type})` cast for enum `[Default(...)]` values in Discovery (Emission already wraps in `({InnerType})…`, so a separate cast produced `({T})({T})value`).

Plus housekeeping: gitignore for `Tests/Modules/Cache/**/%*%/` artifacts that get created when a Cache `.test.goal` references `%path%` in its output directory but the variable isn't bound at file-creation time.

## Final state

- **C# tests**: 2444/2444 green (was 2427 in v1; +4 cycle, +9 cache, +1 dead-emission, +1 OCE, +2 non-generic).
- **`plang --test`**: completes with 165 pass / 42 fail / 5 stale. Was 100% crashing pre-Phase A.
- **Generator**: `ActionClassInfo` and the per-property records all value-equal via `EquatableArray<T>`. Emission file is half raw-string-literal templates; output is byte-equivalent (5 blank-line differences) to v1.

## Code example

The cycle-detection pattern (`PLang/App/Data/this.cs`):

```csharp
[ThreadStatic]
private static HashSet<string>? _resolvingValues;

private @this<T> AsT_Impl<T>(object? raw, Actor.Context.@this? ctx)
{
    if (IsActionDestination(typeof(T))) return ConvertAndWrap<T>(raw, ctx);

    if (raw is string strVal && strVal.Contains('%') && ctx?.Variables != null)
    {
        var isCycleRoot = _resolvingValues == null;
        _resolvingValues ??= new HashSet<string>(StringComparer.Ordinal);
        if (!_resolvingValues.Add(strVal))
            return ConvertAndWrap<T>(strVal, ctx);    // cycle — return raw, don't recurse
        try
        {
            // …existing full-match + partial-match branches…
        }
        finally
        {
            _resolvingValues.Remove(strVal);
            if (isCycleRoot) _resolvingValues = null;
        }
    }
    // … rest of method …
}
```

The raw-string-literal pattern (`PLang.Generators/Emission/Action/this.cs`):

```csharp
sb.Append("""
        public async System.Threading.Tasks.Task<global::App.Data.@this> ExecuteAsync(
            global::App.Goals.Goal.Steps.Step.Actions.Action.@this action, global::App.Actor.Context.@this context)
        {
            __action = action;
            __app = context.App;
            var app = __app!;
            __resolutionError = null;
            if (action != null)
            {

    """);
// …per-property reset emitted via $$""" interpolation …
```

The `EquatableArray<T>` pattern (`PLang.Generators/EquatableArray.cs`):

```csharp
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[]? _array;

    public bool Equals(EquatableArray<T> other)
    {
        if (_array is null) return other._array is null;
        if (other._array is null) return false;
        return _array.AsSpan().SequenceEqual(other._array.AsSpan());
    }
    // GetHashCode walks elements unchecked.
}
```

## Findings explicitly NOT taken in v2

Logged in `plan.md` for a future cleanup pass:

- Findings 4, 5: Discovery's parallel classifiers + 70-line `BuildProperty` cascade (refactor opportunity, not a bug).
- Findings 13, 15, 16, 17, 18: transitional dead code that Phase 5 cleanup of legacy helpers will sweep.
- Findings 14, 23: drop `__app`, simplify Provider lazy-fallback (touches every generated handler — own focused PR).
- Finding 22: split four-branch Data getter into per-shape methods (readability only).
- Finding 24: `SubstitutePrimitive` couples Data to Action (pre-existing; marker interface refactor is a separate design).
- Findings 25, 26: typed-fast-path duplication, hand-rolled `ToBoolean` (pre-existing).
- Finding 29: `As<T>` ignores `_type.Convert` for JSON-typed Data (pre-existing, no current handler hits it).
- Findings 30, 31, 32, 34, 35, 36, 37, 38: pre-existing or pure readability.

Finding 20 (inconsistent `SetFlag` vs `Backing == null` checks across the four Data getter shapes) wasn't taken either — I proposed a direct-init composition test in the plan but realized the check is cosmetic, not behavioral, and the matrix tests already exercise all four shapes through the standard pipeline. Logged for a focused readability pass.

## Test-gap analysis (Ingi's question)

| Gap from v1 | Filled in v2 by |
|--|--|
| Cache hits | `IncrementalCacheTests` — 9 entries pinning value-equality of `ActionClassInfo` and `EquatableArray<T>` |
| Dead emission | `NoDeadEmissionTests` — regex over every `*.Action.g.cs` asserting every private field has at least one read |
| Cycle in `AsT_Impl` | 4 new entries in `DataAsTResolutionTests` (cyclic, self-referencing, partial-match self-reference, deep chain) |
| Non-generic collections | 2 new entries in `DataAsTResolutionTests` (`ArrayList`, `Hashtable`) |
| OCE in App.Run | `AppRun_HandlerThrowsOCE_TranslatesToServiceError_DoesNotPropagate` + `OceThrowingHandler` fixture |

Direct-init composition (Finding 20) wasn't filled — logged above.

## What's still in progress / what to do next

- **Codeanalyzer round 2** — verify the cycle fix, the EquatableArray contract, the OCE comment, and the raw-string emission pass review.
- **Tester / auditor** for the 42 failing `plang --test` cases — none are introduced by v2 (same 165/42/5 numbers across all five phases), but this is the first time the suite has been able to complete since the cycle bug landed. Worth investigating which were broken before vs. structural failures the suite is now surfacing for the first time.
- **Future cleanup PR** for the deferred findings (4, 5, 13, 14, 15, 16, 17, 18, 22, 23, 25, 26, 29, 30-38).

## Files

**Created:**
- `PLang.Generators/EquatableArray.cs`
- `PLang.Tests/Generator/IncrementalCacheTests.cs`
- `PLang.Tests/Generator/NoDeadEmissionTests.cs`

**Modified:**
- `PLang/App/Data/this.cs` — cycle detection in `AsT_Impl`; non-generic shape contract comment
- `PLang/App/this.cs` — OCE catch documentation comment
- `PLang.Generators/this.cs` — `LazyParamsGenerator` class → `@this`
- `PLang.Generators/Discovery/this.cs` — `ActionClassInfo` → record; `RawScalarPropertyDescriptor` `public`→`internal`; drop dead `@this` disjunct; extract `INamedTypeSymbol` local; drop enum-default double cast
- `PLang.Generators/Emission/Action/this.cs` — drop `__variables` / `__paramData` / `ParamData()` emission; rewrite emission with raw string literals
- `PLang.Tests/PLang.Tests.csproj` — add non-analyzer reference to `PLang.Generators`
- `PLang.Tests/App/AppRunScaffoldingTests.cs` — `OceThrowingHandler` + OCE behavioral test
- `PLang.Tests/App/DataTests/DataAsTResolutionTests.cs` — cycle tests + non-generic collection tests
- `PLang.Tests/Generator/SnapshotParamsTests.cs`, `GeneratorValidationTests.cs`, `NoDeadEmissionTests.cs` — generator folder rename path update
- `PLang.Tests/App/Modules/settings/SettingsDataTests.cs` — comment cleanup post-rename
- `.gitignore` — exclude `Tests/Modules/Cache/**/%*%/` runtime artifacts
