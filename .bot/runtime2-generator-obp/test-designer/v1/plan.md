# v1 — Test plan for v4 (resolution lives in `As<T>`, Data is stateless)

## What this is

Translate the architect's v4 plan into a comprehensive C# TUnit test suite that defines the behavioral contract for the coder. Every test starts as `Assert.Fail("Not implemented")` — the signature + intent comment IS the spec. Tests will live alongside the production code (not in `.bot/`).

The architect's v4 plan asserts one architectural sharpening: **resolution lives in `Data.As<T>(context)`, not `Data.Value`'s getter. `Data` is stateless w.r.t. resolution. The handler's `??=` backing field is the only resolution cache, and it has the right lifetime by construction (reset per `ExecuteAsync` call).**

That contract drives the whole suite. Almost every test either probes the new `As<T>` resolution path or asserts a negative — `.Value` does NOT do work, no caching on `Data`, no `_resolved`/`ResetResolution`/`IsDeferredActionTemplate`.

## Scope

**In:**
- C# matrix handlers under `PLang.Tests/Generator/Matrix/` (~28 test action classes covering every property kind × type-shape combination).
- C# contract tests for the new shape: `Action.GetParameter`, `Data.As<T>`, `Data.Value` (raw contract), `App.Run` scaffolding, generator-side build validation, `__SnapshotParams` simplification.
- Rewrite `PLang.Tests/App/Memory/DataResolutionTests.cs` — its current 276 lines encode the *opposite* contract (snapshot-once `.Value` semantics) and become invalid the moment Phase 2 lands. I'll replace it with v4-shaped tests in the same file location.

**Out:**
- PLang `.goal` tests. Per Ingi: existing PLang tests already exercise variable resolution end-to-end through every variable use; adding more is redundant.
- The fixture / test helpers that drive these tests (e.g., "build an Action with synthetic Parameters and call ExecuteAsync via the production `App.Run` path"). The coder builds those; my plan assumes they exist with reasonable shape and uses them in test bodies via `Assert.Fail`.
- Phase-0-against-today's-generator pre-check. Per Ingi: existing C# action tests already validate today's behavior; the matrix encodes v4's contract directly. Some tests will fail until Phase 2/3 lands — that's intentional, they're the regression contract for the refactor.

## Test approach — three categories

1. **Matrix handlers** — minimal `[Action]` partials under `PLang.Tests/Generator/Matrix/<Group>/<Name>.cs`. Each is real generated code; the generator produces the property bodies; tests build a synthetic Action with Parameters and exercise the production execution path.
2. **Contract tests** — TUnit test classes that probe specific behaviors of `Action.GetParameter`, `Data.As<T>`, `Data.Value`, `App.Run`, the generator's build validation, and `__SnapshotParams`. Some directly construct Data; others use a one-off matrix handler.
3. **Rewrite** — `DataResolutionTests.cs` replaced in-place to encode v4's contract.

## Batch plan

I'll propose ~10 tests per batch, get your approval per batch, then write the files. Total estimate: ~85–90 test methods across 10 batches.

| # | Batch | Area | Tests |
|---|---|---|---|
| 1 | Matrix: Plain + Nullable | `Matrix/Plain/`, `Matrix/Nullable/` | ~10 |
| 2 | Matrix: WithDefault + DataPlain | `Matrix/WithDefault/`, `Matrix/DataPlain/` | ~10 |
| 3 | Matrix: DataWrapped + Provider | `Matrix/DataWrapped/`, `Matrix/Provider/` | ~10 |
| 4 | Matrix: IsNotNull + Markers | `Matrix/IsNotNull/`, `Matrix/Markers/` | ~9 |
| 5 | Matrix: Resolution + Modifier + Snapshot | `Matrix/Resolution/`, `Matrix/Modifier/`, `Matrix/Snapshot/` | ~10 |
| 6 | Contract: `Action.GetParameter` | `App/Goals/Goal/Steps/Step/Actions/Action/GetParameterTests.cs` | ~6 |
| 7 | Contract: `Data.As<T>` resolution | `App/Memory/DataAsTResolutionTests.cs` (new) | ~12 |
| 8 | Contract: `Data.Value` raw + `DataResolutionTests.cs` rewrite | `App/Memory/DataResolutionTests.cs` (rewrite in place) | ~8 |
| 9 | Contract: `App.Run` scaffolding | `App/AppRunScaffoldingTests.cs` (new) | ~6 |
| 10 | Contract: Generator validation + `__SnapshotParams` | `Generator/GeneratorValidationTests.cs`, `Generator/SnapshotParamsTests.cs` | ~10 |

Total: ~91 tests.

## Batch 1 preview — Matrix: Plain + Nullable

To make the shape concrete, here's what Batch 1 would propose. (Subsequent batches follow the same pattern; I'll detail them when approved.)

**Handlers** (each is a minimal `[Action]` partial — body returns predictable `Data.@this`):

```
PLang.Tests/Generator/Matrix/Plain/
  StringPlain.cs       // partial class StringPlain : IContext { partial Data<string> Path { get; init; } ... }
  IntPlain.cs          // Data<int> Count
  BoolPlain.cs         // Data<bool> Flag
  PathPlain.cs         // Data<FileSystem.Path> File   (App-resolvable via static Resolve)

PLang.Tests/Generator/Matrix/Nullable/
  StringNullable.cs    // Data<string>? Tag
  IntNullable.cs       // Data<int>? Maybe
```

**Test methods** (signatures with intent comments; bodies are `Assert.Fail("Not implemented")`):

```csharp
namespace PLang.Tests.Generator.Matrix.Plain;

public class StringPlainTests
{
    // Literal Parameter Value resolves to typed Data<string> with same value.
    [Test] public async Task StringPlain_LiteralValue_ResolvesToTypedData() => Assert.Fail("Not implemented");

    // Property is read twice in same call → same backing-field instance returned (handler-level cache).
    [Test] public async Task StringPlain_ReadTwice_ReturnsCachedBackingField() => Assert.Fail("Not implemented");
}

public class IntPlainTests
{
    // Parameter Value of "42" (string) → typed Data<int> via TypeMapping conversion.
    [Test] public async Task IntPlain_StringValue_ConvertsToInt() => Assert.Fail("Not implemented");

    // Parameter Value of 42 (boxed int) → typed Data<int> via fast path (Value is T already).
    [Test] public async Task IntPlain_IntValue_FastPath() => Assert.Fail("Not implemented");
}

public class BoolPlainTests
{
    // Parameter Value of "true" → typed Data<bool>.
    [Test] public async Task BoolPlain_StringTrue_ConvertsToBool() => Assert.Fail("Not implemented");
}

public class PathPlainTests
{
    // FileSystem.Path has static Resolve(string, Context) → As<T> dispatches to it for string Values.
    [Test] public async Task PathPlain_StringValue_UsesStaticResolve() => Assert.Fail("Not implemented");
}

namespace PLang.Tests.Generator.Matrix.Nullable;

public class StringNullableTests
{
    // Parameter not present → property reads as Data<string>? with null Value (NotFound semantics).
    [Test] public async Task StringNullable_Missing_ReadsAsNullData() => Assert.Fail("Not implemented");

    // Parameter present with literal → typed Data<string>? with the value.
    [Test] public async Task StringNullable_Present_ResolvesToValue() => Assert.Fail("Not implemented");
}

public class IntNullableTests
{
    // Missing parameter → null Data; no exception, no validation failure.
    [Test] public async Task IntNullable_Missing_ReadsAsNull() => Assert.Fail("Not implemented");

    // Present integer parameter → typed Data<int>?.
    [Test] public async Task IntNullable_Present_ResolvesToInt() => Assert.Fail("Not implemented");
}
```

**Total Batch 1: 10 tests across 6 handlers.**

## Files to be created

**Matrix handlers (~28 files, one per matrix entry):**
- `PLang.Tests/Generator/Matrix/Plain/{StringPlain,IntPlain,BoolPlain,PathPlain}.cs`
- `PLang.Tests/Generator/Matrix/Nullable/{StringNullable,IntNullable}.cs`
- `PLang.Tests/Generator/Matrix/WithDefault/{StringWithDefault,IntWithDefault,EnumWithDefault,BoolWithDefault}.cs`
- `PLang.Tests/Generator/Matrix/DataPlain/DataPlain.cs`
- `PLang.Tests/Generator/Matrix/DataWrapped/{DataWrappedString,DataWrappedList,DataWrappedDict,DataWrappedActionList}.cs`
- `PLang.Tests/Generator/Matrix/Provider/{ProviderProp,ProviderMissing}.cs`
- `PLang.Tests/Generator/Matrix/IsNotNull/IsNotNullProp.cs`
- `PLang.Tests/Generator/Matrix/Markers/{IContextHandler,IChannelHandler,IActionHandler,IStepHandler,IStaticHandler}.cs`
- `PLang.Tests/Generator/Matrix/Resolution/{FullVarMatch,Interpolation,DeepResolutionList,DeepResolutionDict,ReResolveAcrossCalls,ConcurrentHandlers}.cs`
- `PLang.Tests/Generator/Matrix/Modifier/ModifierAction.cs`
- `PLang.Tests/Generator/Matrix/Snapshot/SnapshotOnError.cs`

**Matrix test classes (one per handler, named `<Handler>Tests.cs`)** — co-located with the handlers under `PLang.Tests/Generator/Matrix/<Group>/`.

**Contract test classes:**
- `PLang.Tests/App/Goals/Goal/Steps/Step/Actions/Action/GetParameterTests.cs`
- `PLang.Tests/App/Memory/DataAsTResolutionTests.cs` (new)
- `PLang.Tests/App/Memory/DataValueRawTests.cs` (new — focused asserts that `.Value` does no resolution)
- `PLang.Tests/App/AppRunScaffoldingTests.cs`
- `PLang.Tests/Generator/GeneratorValidationTests.cs`
- `PLang.Tests/Generator/SnapshotParamsTests.cs`

**Files rewritten in place:**
- `PLang.Tests/App/Memory/DataResolutionTests.cs` — replaces snapshot-once tests with v4-shaped tests (the file stays for historical continuity in `git log`; content is fully replaced).

## What I expect from the coder (informational, not part of this plan)

The matrix tests assume a fixture exists that can:
1. Construct an `Action.@this` with synthetic `Parameters: List<Data>` and `Defaults: List<Data>`.
2. Resolve the handler instance via `App.Modules.Get` (or a test-scoped registry).
3. Invoke `App.Run(action, context)` so the production scaffolding runs.
4. Return the result `Data` plus access to `frame.Snapshots` and `Error.Params` for snapshot assertions.

Phase 0 of the v4 plan implies this fixture is the matrix's runner. I'll write tests assuming a method roughly named `await App.RunMatrixAction<THandler>(parameters, defaults, vars)` returning a `MatrixResult` with `Data`, `Frame`, `Snapshot` accessors. Coder is free to shape it differently — the tests only depend on its existence in spirit; bodies are `Assert.Fail` so signature drift in the helper is harmless.

## Mid-stream guidance from Ingi

While writing Batches 3+, Ingi clarified the big-picture goal: **"Data flows through, no unwrapping of Data, and make the source generator simpler."** I made that the explicit spine of three files:
- `DataValueRawTests.cs` — `.Value` is read-only, never resolves, never caches; `Data` flows through `GetParameter` with reference identity preserved.
- `DataAsTResolutionTests.cs` — `As<T>` fast path returns `this` when already typed (no unwrap/rewrap); every read is a fresh resolution.
- `GeneratorValidationTests.cs` — asserts the "one uniform shape per parameter property" promise; verifies the property hierarchy collapses to `DataProperty + ProviderProperty`; verifies the generator no longer emits `__Resolve`/`__StripPercent`/`data.ResetResolution()`/etc.

The matrix tests still cover the kind × type-shape granularity, but the high-level contract has its own dedicated test files now.

## Final approach

Per the mid-stream "go for it, I'll see the end result" — I skipped per-batch approval and wrote all the test files at once. Build is green. ~139 test methods landed (vs. ~91 estimated) — the extra coverage came from making the big-picture contract explicit rather than implicit in the matrix.

## What I did NOT write (coder territory)

- The matrix handler stubs (`Matrix/Plain/StringPlain.cs`, etc.). These need the source generator to run on PLang.Tests — currently it doesn't (PLang.Tests.csproj has no analyzer reference to PLang.Generators). The coder wires up the analyzer reference, builds the handlers, and the test fixture that exercises them.
- The fixture / test helper. The architect's plan describes "build an Action with synthetic Parameters and call ExecuteAsync via the production App.Run path." Coder builds it.
- Test bodies. All test bodies are `Assert.Fail("Not implemented")` — coder fills them in as part of Phase 1+ work.

## Open items

None blocking.
