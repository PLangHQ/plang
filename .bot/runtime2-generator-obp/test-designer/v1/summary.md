# test-designer v1 — Test contract for v4 (resolution lives in As<T>; Data flows through)

## What this is

The architect's v4 plan supersedes v3 with one architectural sharpening: **resolution lives in `Data.As<T>(context)`, not `Data.Value`'s getter. `Data` is stateless w.r.t. resolution.** Plus the v3 carry-overs: every action property is `Data<T>` or `[Provider]`, scaffolding moves to `App.Run`, source generator collapses to a 2-leaf property hierarchy.

This session translates that design into a comprehensive C# TUnit test suite — ~139 test methods across 18 files, all bodies `Assert.Fail("Not implemented")`. The signatures + intent comments ARE the spec; the coder fills in bodies during Phase 1+ work.

## What was done

**Mid-stream guidance from Ingi:** "Data flows through, no unwrapping of Data, and make source gen simpler — that is the high level I want out of this." This became the explicit spine of three contract test files (instead of being implicit in matrix coverage).

**Approach:** After the plan was approved with "go for it, I'll see the end result," I skipped per-batch approval and wrote all 18 files in one pass. Build is green.

**Files created (17 new, 1 rewritten):**

Matrix tests under `PLang.Tests/Generator/Matrix/<Group>/<Group>Tests.cs`:
- `Plain/PlainTests.cs` — String/Int/Bool/Path × literal/converted/error paths (9 tests)
- `Nullable/NullableTests.cs` — missing/present/null Value (5)
- `WithDefault/WithDefaultTests.cs` — String/Int/Enum/Bool defaults (8)
- `DataPlain/DataPlainTests.cs` — `Data == Data<object>` flow-through (5)
- `DataWrapped/DataWrappedTests.cs` — `%var%`-bearing scalars/lists/dicts/sub-action lists (8)
- `Provider/ProviderTests.cs` — registered/missing provider injection (4)
- `IsNotNull/IsNotNullTests.cs` — null rejection at validation (4)
- `Markers/MarkersTests.cs` — `IContext`/`IChannel`/`IAction`/`IStep`/`IStatic`/multi (7)
- `Resolution/ResolutionTests.cs` — full match, interpolation, deep walk, re-resolve, concurrency (14)
- `Modifier/ModifierTests.cs` — wrap/retry/Handled-override (4)
- `Snapshot/SnapshotTests.cs` — `__SnapshotParams` on error/success (5)

Generator-level contract tests under `PLang.Tests/Generator/`:
- `GeneratorValidationTests.cs` — build-time `Data<T>`-only check, uniform property shape, no helper-family emission, thin `ExecuteAsync` (13)
- `SnapshotParamsTests.cs` — per-property snapshot entry, attaches to error only (8)

App contract tests:
- `App/GetParameterTests.cs` — `Action.GetParameter` lookup walk (6)
- `App/AppRunScaffoldingTests.cs` — callstack, save/restore, ServiceError translation, Handled-override bypass (8)
- `App/Memory/DataAsTResolutionTests.cs` — fast paths, full match, interpolation, deep walks, fresh-per-call, action-list non-recursion (13)
- `App/Memory/DataValueRawTests.cs` — `.Value` is raw, no side effect, no `_resolved`/`ResetResolution`/`IsDeferredActionTemplate` (11)

Rewritten in place (was: snapshot-once `.Value` semantics — the OPPOSITE contract):
- `App/Memory/DataResolutionTests.cs` — now asserts shared-Data flow-through, loop iteration freshness, sub-goal isolation, concurrent `As<T>` safety (7)

**Total: 139 test methods.**

## Code example

The matrix tests assert generated-handler behavior via a fixture the coder will build:

```csharp
public class StringPlainTests
{
    // Literal string Parameter Value → property exposes Data<string> with the same value.
    [Test] public async Task StringPlain_LiteralValue_ResolvesToTypedData() => Assert.Fail("Not implemented");

    // Property is read twice in same call → second read returns the cached backing-field instance (reference equality).
    [Test] public async Task StringPlain_ReadTwice_ReturnsCachedBackingField() => Assert.Fail("Not implemented");
}
```

The contract tests directly probe the v4 architecture:

```csharp
public class DataValueRawTests
{
    // .Value on a string with "%var%" content → returns the literal "%var%" string, NOT a substituted value.
    // (This is the v4 contract change: today this triggers resolution; after v4 it does not.)
    [Test] public async Task Value_StringWithVarPlaceholder_ReturnsRawNotSubstituted() => Assert.Fail("Not implemented");

    // Data flows through Action.GetParameter unchanged — the same Data instance is returned (reference equality).
    [Test] public async Task DataFlow_ThroughGetParameter_ReferenceIdentityPreserved() => Assert.Fail("Not implemented");
}
```

## What's still in progress / what to do next

**Coder's job (Phase 1+ of v4):**
1. Add `PLang.Generators` as an analyzer reference to `PLang.Tests.csproj`.
2. Build the matrix handler stubs (one `[Action]` partial per matrix entry under `PLang.Tests/Generator/Matrix/<Group>/<Name>.cs`). Each is a real generated handler with the property shape declared and a minimal `Run()`.
3. Build the test fixture (e.g., `App.RunMatrixAction<THandler>(parameters, defaults, vars)` returning `MatrixResult` with `Data`, `Frame`, `Snapshot` accessors).
4. Implement v4 Phases 1–6 per the architect's plan.
5. Fill in test bodies as each phase lands. Tests are designed so the matrix and contract suites flip from failing → passing in roughly this order:
   - Phase 1 (`Action.GetParameter`) → `GetParameterTests` flips green.
   - Phase 2 (`Data.As<T>`, delete cache flags) → `DataValueRawTests`, `DataAsTResolutionTests`, `DataResolutionTests` flip green; matrix's `Resolution/*` flips green.
   - Phase 3 (`App.Run` scaffolding, generator emits new shape) → `AppRunScaffoldingTests`, `GeneratorValidationTests`'s shape tests flip green.
   - Phase 4 (property hierarchy collapse, `EmitSnapshotEntry`) → `SnapshotParamsTests` flips green.
   - Phase 5 (build-time `Data<T>` enforcement) → `GeneratorValidationTests`'s build-error tests flip green.

**Decisions still open:** none I'm aware of. Architect's open question (Phase 0 review point) remains — recommend a brief check after the matrix handlers + fixture land but before Phase 1 begins.

## Notes on what was NOT done

- No matrix handler stubs (need analyzer wiring on PLang.Tests).
- No fixture / test helpers (coder builds these).
- No PLang `.goal` tests (per Ingi: existing PLang tests already exercise variable resolution end-to-end).
- No tests against today's generator (per Ingi: existing C# action tests already cover that; the matrix encodes v4's contract directly, will fail before Phase 2/3 lands — intentional regression contract).
