# Coder v1 — Execute v4 of `runtime2-generator-obp`

## What this is

Implementation plan for the architect's v4 plan + test-designer's 139 stub tests. The v4 plan is the spec; this plan is the *order of operations* — what I'll change in what sequence, where the load-bearing risks are, and where I'll pause to verify before continuing.

The test suite is already written (139 methods, all `Assert.Fail("Not implemented")`). My job: build the matrix handlers + fixture, then execute v4 Phases 1 → 6, filling in test bodies as each phase lands so the suite flips green incrementally.

## Scope of this session vs follow-ups

I'll propose **landing all 6 phases in this branch**, sequenced internally with a clean commit per phase. If the contract change in Phase 2 surfaces wider breakage than expected (architect's "more than ~10 sites" threshold), I stop and reconvene before continuing.

No other sessions — one branch, one PR targeting `runtime2`. The phases are the commit boundary, not the session boundary.

## State of the world (verified just now)

- Branch `runtime2-generator-obp` is checked out, up to date with origin.
- `PLang.Generators/LazyParamsGenerator.cs` is 779 lines, single file with 11-arm property dispatch (per architect's analysis).
- `PLang/App/Data/this.cs` has the cluster the architect flagged: `_resolved`, `_rawValue`, `ResetResolution()`, `IsDeferredActionTemplate`, lazy resolution side effect on `Value` getter (lines 211–247).
- `PLang/App/Variables/this.cs` has `ResolveDeep` + `_resolveDepth` + `_resolveItemCount` + `MaxResolveItems` + `OnResolveTrace` (lines 406–526).
- `PLang/App/this.cs:376` already has `App.Run(action, context)` that just dispatches via `Modules.GetCodeGenerated(action)` and calls `handler.ExecuteAsync`. Scaffolding currently lives inside the *generated* `ExecuteAsync` (callstack push/pop, save/restore, try/catch/finally) — Phase 3 hoists it into `App.Run`.
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` (189 lines) has no `GetParameter` method yet — Phase 1 adds it.
- Migration sweep is small: only **22** non-`Data<T>` partial properties remain across `App/modules/`, all `[VariableName] string` (18× `ListName`, 4× variable/`Name`). Plus the `loop/foreach.cs` `ItemName`/`KeyName`. Architect's "small remainder" is accurate.
- Tests live at `PLang.Tests/App/...` and `PLang.Tests/Generator/Matrix/...` — 139 methods, all `Assert.Fail`.
- `PLang.Tests.csproj` does NOT have an analyzer reference to `PLang.Generators` — Phase 0 wires it.

## Phase 0 — Matrix handlers + test fixture (purely additive)

**Goal:** make the matrix runnable. Handlers compile under the source generator. Fixture builds an Action with synthetic Parameters/Defaults and invokes it via `App.Run` (production path).

1. **Wire generator into PLang.Tests.csproj.** Add `<ProjectReference Include="..\PLang.Generators\PLang.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`.
2. **Build matrix handler stubs** under `PLang.Tests/Generator/Matrix/<Group>/<Name>.cs` — one `[Action]` partial per matrix entry. Each handler has the property declarations the test expects (e.g., `partial Data<string> Path { get; init; }`) and a minimal `Run()` returning predictable `Data.@this`. ~28 handlers.
3. **Build the test fixture.** Provide a helper class — proposed location `PLang.Tests/App/Fixtures/MatrixRunner.cs` — that:
   - Constructs an `Action.@this` with `Module="matrix"`, `ActionName="<handler>"`, `Parameters: List<Data>`, `Defaults: List<Data>`.
   - Builds a minimal `Context` with seeded variables (uses existing `Bootstrap` helpers if present).
   - Resolves the handler instance manually (test handlers won't be in `App.Modules`, so the fixture skips lookup and `await handler.ExecuteAsync(action, context)` directly — equivalent to today's `App.RunAction`, but post-Phase-3 the fixture wraps with the new `App.Run` scaffolding for parity).
   - Returns a `MatrixResult { Data, Frame, Snapshot }` for assertions.
4. **Phase 0 exit criterion:** `dotnet run --project PLang.Tests` builds. Matrix tests are still all failing (`Assert.Fail`); they're not yet wired to the fixture (test bodies are filled per phase). Build green is the deliverable.

**Risk:** test handlers must not collide with the generator's "must be Data<T> or [Provider]" rule that lands in Phase 5. Phase 0 handlers are written in the v4-shape from day one — `Data<T>` only, no `[VariableName]`, no raw scalars. No retro fix needed at Phase 5.

**Commit boundary:** "Phase 0: matrix handlers + fixture, generator wired into PLang.Tests".

## Phase 1 — `Action.GetParameter(name, context)` (pure addition)

1. Add `GetParameter` to `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs`:
   ```csharp
   public Data.@this GetParameter(string name, Actor.Context.@this context)
   {
       var data = Parameters?.FirstOrDefault(p =>
           string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
           ?? Defaults?.FirstOrDefault(p =>
               string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
       return data ?? Data.@this.NotFound(name);
   }
   ```
   Pure lookup; no resolution. Context parameter is currently unused but is part of the contract — kept for symmetry with `As<T>(context)`.
2. Generator unchanged. New method exists but isn't called yet.
3. **Fill in `GetParameterTests.cs`** (6 tests) — exercises the lookup walk directly.
4. **Phase 1 exit:** `GetParameterTests.cs` is green; everything else unchanged.

**Commit boundary:** "Phase 1: Action.GetParameter (pure addition)".

## Phase 2 — Resolution moves from `.Value` to `As<T>(context)` (the contract change)

This is the architectural move. Highest-risk phase.

1. **Rewrite `Data.As<T>(context)`** — replace today's implementation (lines 409–442) with the v4 walk:
   - `string` matching `^%name%$` → `context.Variables.Get(name).Value` (or return `Data.NotFound`/`Data.FromError` as appropriate).
   - `string` containing `%...%` partial → `context.Variables.Resolve(str)` → cast/convert.
   - `IList<object?>` → walk, substitute primitives, convert via TypeMapping → typed list.
   - `IDictionary<string, object?>` → walk, substitute primitives, convert via TypeMapping → typed dict.
   - `T` has static `Resolve(string, Context)` and `_value` is string → call it.
   - Already-typed (`this is Data<T> typed && typed.Value is T`) → return `this` (fast path).
   - Otherwise → `TypeMapping.TryConvertTo(_value, typeof(T), context)`.
   - On failure → `Data<T>.FromError(...)`.
   - Skip recursion into `Action.@this` and `IEnumerable<Action.@this>` — preserves today's `IsDeferredActionTemplate` carve-out, but expressed as a non-recursion guard inside `As<T>`'s walker, not as a flag on `Data`.
2. **Rewrite `Data.Value` getter** to be plain — return `_value`, no resolution side effect, no factory replacement on first access. Factory still resolves once on first read (necessary for `DynamicData` and `SetValue(Func<>)` use cases) but does NOT do `%var%` substitution.
3. **Delete from `Data.@this`:** `_resolved`, `_rawValue`, `ResetResolution()`, `IsDeferredActionTemplate(Type?)`, `NeedsResolution` setter logic in the Value setter (the property itself stays as a marker).
4. **Delete from `Variables.@this`:** `ResolveDeep(object?)`, `_resolveDepth`, `_resolveItemCount`, `MaxResolveItems`, `OnResolveTrace` (verify no external subscribers; if any, the trace can be re-routed via `As<T>` later).
5. **Generator stops emitting `data.ResetResolution()`** in `__ResolveData`. Stops emitting `__StripPercent` (Phase 5 will delete the call sites; until then it stays as a stub that just `return data?.Value?.ToString()?.Trim('%')`).
6. **Run tests.** Matrix `Resolution/*` should flip green; `DataValueRawTests` + `DataAsTResolutionTests` flip green; `DataResolutionTests` (rewritten) flips green.

**Risk gate:** the architect's stance — *"if more than ~10 sites need updating, pause and review."* Concretely, before pushing the Phase 2 commit:
- `dotnet run --project PLang.Tests` — count failures attributable to "code reading `.Value` and expecting resolution."
- If ≤10 sites: fix them (route through the property, which routes through `As<T>`).
- If >10: stop, write findings to `v1/result.md`, ping Ingi.

**Commit boundary:** "Phase 2: resolution lives in As<T>; Data is stateless".

## Phase 3 — `App.Run` scaffolding + generator emits new shape (atomic cutover)

1. **Move scaffolding into `App.Run(action, context)`:**
   - Resolve handler (existing).
   - Push callstack frame (`context.CallStack?.Push(action)`).
   - Save `previousStep/Goal/Event`; set `Context.Step = action.Step`, `Context.Goal = action.Step?.Goal`.
   - `try { result = await handler.ExecuteAsync(action, context); … } catch (...) { translate to ServiceError, attach __SnapshotParams via interface call on handler } finally { frame?.SnapshotVariables; pop callstack; restore Step/Goal/Event }`.
   - The `__SnapshotParams` access becomes an interface method on `ICodeGenerated` (or a new `IActionSnapshot`) — generated code implements it, App.Run calls it from the catch.
2. **Generator updates `ExecuteAsync` to the thin form:**
   - `__action = action; Context = context (if IContext); markers init; eager Provider init; backing-field reset; per-class validation; return Run()`.
   - **Removed** from generated body: callstack push/pop, save/restore Step/Goal/Event, try/catch/finally, frame snapshot.
3. **Generator emits the new uniform property shape:**
   ```csharp
   private Data.@this<T>? __Name_backing;
   public partial Data.@this<T> Name
   {
       get => __Name_backing ??= __action.GetParameter("name", Context).As<T>(Context);
       init { __Name_backing = value; }
   }
   ```
   For non-`Data<T>` properties (Phase 5 hasn't deleted them yet): keep emitting the old shape under a "legacy" branch — gated by `IsDataWrapped`. Phase 5 deletes the legacy branch.
4. **Generator stops emitting** `__Resolve<T>`, `__ResolveData`, `__TryConvert`, `__FormatValue`, `__HasParam` — they're replaced by `As<T>` and `GetParameter`. `__StripPercent` stays until Phase 5 because the 22 `[VariableName]` properties still call it.
5. **`__SnapshotParams` simplifies** — `PrValue = __action.GetParameter(name, Context).Value`, `FinalValue = __<Name>_backing?.Value`. Per-property emission still inline at this phase; Phase 4 moves it into `ActionProperty.EmitSnapshotEntry`.
6. **Run tests.** `AppRunScaffoldingTests` flips green; `GeneratorValidationTests`'s shape tests flip green; matrix tests for shape (the Plain/Nullable/WithDefault/etc. groups) flip green.

**Commit boundary:** "Phase 3: App.Run owns scaffolding; generator emits the v4 property shape".

## Phase 4 — Property hierarchy + collapse dispatch

1. **Restructure** `PLang.Generators/`:
   - Rename `LazyParamsGenerator.cs` → `this.cs` (orchestration only, ~50 lines).
   - Create `Discovery/this.cs` (predicate + symbol scan + factory).
   - Create `Emission/Action/this.cs` (per-handler partial-class shell, thin ExecuteAsync, __SnapshotParams body).
   - Create `Emission/Property/this.cs` (`abstract record ActionProperty`).
   - Create `Emission/Property/Data/this.cs` (`DataProperty`).
   - Create `Emission/Property/Provider/this.cs` (`ProviderProperty`).
2. **`ActionProperty` exposes** `EmitProperty(StringBuilder)` and `EmitSnapshotEntry(StringBuilder)` — both invoked from the action emitter.
3. **Discovery factory** — trivial: `IsProvider ? new ProviderProperty(...) : new DataProperty(...)`. The 11-branch ladder is gone.
4. **Snapshot emission moves** into `ActionProperty.EmitSnapshotEntry` per-property; `Emission/Action/this.cs` just sums the contributions.
5. **Run tests.** `GeneratorValidationTests`'s structure tests flip green. `SnapshotParamsTests` flips green. Everything still passes.

**Commit boundary:** "Phase 4: collapse generator to DataProperty + ProviderProperty hierarchy".

## Phase 5 — Migration sweep + `[VariableName]` removal + build-time validation

Sequence within this phase (Phase 5 is bigger than 4 — done in three sub-commits if it grows):

1. **Survey, then convert the 22 `[VariableName] partial string` properties** to `Data<T>` + read `.Name` (or `.Value.Trim('%')` if the var-name semantic differs). Each handler is small; commit per module.
2. **Delete `[VariableName]` attribute** + class.
3. **Generator stops emitting `__StripPercent`** (last consumer is gone).
4. **Enable build-time check** in `Discovery/this.cs`: any `[Action]` partial property that is neither `Data.@this`, `Data.@this<T>`, nor `[Provider]`-attributed → emit a Roslyn diagnostic (build error).
5. **Run tests.** `GeneratorValidationTests`'s build-error tests flip green. Run full PLang test suite. Run `plang p build` against `system/builder/`.

**Commit boundary:** "Phase 5: complete Data<T> migration; enforce at build time; delete [VariableName]".

## Phase 6 — Cleanup sweep + audit

1. Audit `Data.As<T>` against the matrix — confirm every Resolution case flows through it. Slim only obviously-dead branches.
2. Confirm `Variables.ResolveDeep` is gone (no call sites).
3. Confirm no typed-POCO reflection-walk in `Data` or `Variables`.
4. Audit generator emission — no `__Resolve*`/`__StripPercent`/`__TryConvert`/`__FormatValue`/`__HasParam` survivors.
5. Run full PLang test suite. Run `plang p build` on `system/builder`.
6. Update `Documentation/Runtime2/good_to_know.md` if I learned anything architectural.
7. Write `v1/summary.md` + bot-root summary. Commit `.bot/`. Push. Open PR targeting `runtime2`.

**Commit boundary:** "Phase 6: audit and cleanup; PR targeting runtime2".

## Test contract — how each phase lands tests green

| Phase | Tests that flip green |
|-------|----------------------|
| 0     | (none — handlers compile, fixture exists) |
| 1     | `GetParameterTests.cs` (6) |
| 2     | `DataValueRawTests.cs` (11), `DataAsTResolutionTests.cs` (13), `DataResolutionTests.cs` (rewritten — 7), Matrix `Resolution/*` |
| 3     | `AppRunScaffoldingTests.cs` (8), Matrix `Plain/Nullable/WithDefault/DataPlain/DataWrapped/Provider/IsNotNull/Markers/Modifier/Snapshot`, `GeneratorValidationTests` shape tests |
| 4     | `SnapshotParamsTests.cs` (8), `GeneratorValidationTests` structure tests |
| 5     | `GeneratorValidationTests` build-error tests |
| 6     | (audit only — all green) |

## Risks and unresolved items

- **`OnResolveTrace` subscribers.** Phase 2 deletes it. Need to grep for `OnResolveTrace +=` outside of `Variables/this.cs` before deleting. If anyone is listening, route the trace via `As<T>` or drop the listener with the listener.
- **`Data.Value` factory case.** `_valueFactory` still exists (used by `DynamicData`-like patterns). The Phase 2 rewrite must keep factory resolution (lazy compute) without triggering `%var%` substitution. Two distinct concerns — keep factory, drop `NeedsResolution` side effect.
- **Provider eager init in `ExecuteAsync`.** Phase 3 moves scaffolding to `App.Run`, but provider eager init must stay in the generated `ExecuteAsync` (it needs the handler instance to assign backing fields). Order: marker init → provider init → backing reset → validation → `Run()`.
- **Matrix fixture vs `App.Run` parity.** Phase 3 changes `App.Run`'s shape. The fixture either:
  - (a) Calls `App.Run(action, context)` directly → benefits from real scaffolding, but matrix handlers must be registered in `Modules` first.
  - (b) Calls `handler.ExecuteAsync(action, context)` directly + manually wraps with the same scaffolding (decouples from `Modules`).
  
  Recommend (a) with a per-test scoped `Modules` registration helper. Cleaner contract; tests exercise the production path Ingi cares about.
- **Phase 3 `__SnapshotParams` interface.** App.Run needs to call snapshot from the catch block — handler-side. Cleanest: extend `ICodeGenerated` with `List<ParamSnapshot> SnapshotParams()`. Generator implements it. App.Run reads it.

## Open questions for Ingi

1. **All in one branch?** v4 is six phases — confirming you want them all on this branch as one PR. (This is what the architect plan says, but landing Phase 1+2 incrementally was a thought.)
2. **Phase 2 risk gate.** The "more than ~10 sites" threshold the architect proposed — confirming I should pause and ping you, not just power through. (My default: pause and write to `v1/result.md` for your read.)
3. **`OnResolveTrace`** — OK to delete with `ResolveDeep` if no external subscribers? I'll grep first; flagging in case the answer is "preserve as a hook on `As<T>`."

## Files I'll touch (high-level)

**Created:**
- `PLang.Tests/App/Fixtures/MatrixRunner.cs` (Phase 0)
- ~28 matrix handler stubs (Phase 0)
- `PLang.Generators/Discovery/this.cs` (Phase 4)
- `PLang.Generators/Emission/Action/this.cs` (Phase 4)
- `PLang.Generators/Emission/Property/this.cs` + `Data/this.cs` + `Provider/this.cs` (Phase 4)

**Modified:**
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` (Phase 1: `GetParameter`)
- `PLang/App/Data/this.cs` (Phase 2: rewrite `As<T>`, delete cluster)
- `PLang/App/Variables/this.cs` (Phase 2: delete `ResolveDeep` + state)
- `PLang/App/this.cs` (Phase 3: scaffolding into `App.Run`)
- `PLang.Generators/LazyParamsGenerator.cs` → `PLang.Generators/this.cs` (Phase 3+4: rename + thin)
- `PLang.Tests/PLang.Tests.csproj` (Phase 0: analyzer reference)
- `PLang.Tests/App/Memory/DataResolutionTests.cs` (already rewritten by test-designer; bodies filled in Phase 2)
- 22 handler files under `App/modules/list/`, `App/modules/loop/`, `App/modules/variable/` (Phase 5)

**Deleted:**
- `PLang.Generators/LazyParamsGenerator.cs` (after rename to `this.cs`)
- `[VariableName]` attribute and class (Phase 5)
- All `Data._resolved`, `_rawValue`, `ResetResolution`, `IsDeferredActionTemplate` (Phase 2)
- All `Variables.ResolveDeep` and supporting state (Phase 2)

## Awaiting Ingi's read-back before starting Phase 0.
