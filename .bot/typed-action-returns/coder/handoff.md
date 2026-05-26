# coder handoff — `typed-action-returns` Stage 0 mid-flight

**State at handoff:** branch builds clean, 3 coder commits pushed, no test bodies replaced yet.

## What's in (committed + pushed)

- `6ed436e93` — IClass interface + default Build() + builder.warning record + noop channel + Channel(name)
- `0bb362c92` — Data.As<T> made internal, public Data.As(string typeName) added for cross-type coercion
- `005daa8fd` — PlangType attribute slimmed (only Name param), kept only on the 2 divergent sites (`goal.call`, `catalog`), no-arg `[PlangType]` marker on derivable named types

### Design decisions locked with Ingi (do not re-litigate)

1. **`Data.As<T>` stays internal**, used only by the source generator. Public materialization surface is `Data.As(string typeName)`. The eventual `Data.As(Type)` shape can wait — `value.As(format)` (string) matches how a handler will call it from a `.pr`-supplied format value.
2. **`.Type`-driven implicit materialization** stays on `Data.Value` property access path (already wired via `ConvertValue()` in Navigation.cs). No contract change to `.Value`.
3. **`[PlangType]` survives** as Name-only override. Used on `GoalCall` (`"goal.call"`) and `Schema.@this` (`"catalog"`). Other catalog-visible non-`@this` classes carry the **no-arg** form `[PlangType]` as a discoverability marker — the name derives from class-name lowercased. `Shape`/`Example`/`Description` parameters were dropped; that metadata now comes from a **static-property convention** on the type itself:

       public static string Example => "...";
       public static string Description => "...";
       public static string Shape => "string";

   Read via reflection by `app.types.@this.BuildTypeEntries` (see `ReadStaticString` helper).
4. **`"translatable"` alias on TString dropped** — Ingi explicitly approved. The existing `TypeMapping_ResolvesTranslatable` test was rewritten to assert intentional removal.
5. **Builder "builder" channel registration is PLang-side, not C#.** The C# layer provides `Channel(name)` + noop fallback + the `builder.warning.@this` record. The `system/builder/Build.goal` is responsible for `channel set "builder" ...` at build start. Stage 0 tests `Builder_BuildStart_RegistersBuilderChannel` / `Builder_BuildEnd_DisposesBuilderChannel` need to drive through a PLang invocation when their test bodies get written.
6. **`file.save` cross-type coercion is out of scope for this branch.** Logged in `Documentation/v0.2/todos.md` as the canonical end-to-end test for type-driven materialization, deferred to its own branch (when modules get a real implementation pass).

### Two Stage 0 tests need rewriting

The test-designer wrote these expecting **full** PlangType removal. Ingi's call kept the attribute. When you write test bodies, reframe:

- `PlangTypeAttribute_DoesNotExistAsType` → `PlangTypeAttribute_OnlyOverridesDivergentNames` (assert attribute exists, but only `GoalCall` and `Schema.@this` carry the named form)
- `PlangTypeAttribute_NoSourceFileReferencesIt` → drop or replace with "no source file uses `[PlangType("derivable-name")]`" (covers the slim)

## What's still owed

### Stage 0 (~50% done)

- **`builder.validate` Build() iteration plumbing.** The blocker described below.
- **Replace ~30 `Assert.Fail` bodies** in `PLang.Tests/App/TypedReturnsTests/Stage0_*Tests.cs`.

### The Build() invocation seam — the next-up problem

Per test contract: `Task<Data> Build()` is **parameterless** on `IClass`. A handler's override reads its own `Path`/`Schema`/etc. via the source-gen-emitted lazy property getters — those resolve through the private `__action` field that today is set only by `ExecuteAsync`.

So calling `Build()` from `validate` needs a way to set `__action` on the handler **before** invoking Build(). Cleanest path: emit a public `SetAction(action, context)` (or `PrepareForBuild(action, context)`) method in `PLang.Generators/Emission/Action/this.cs` that does the same `__action = action; __app = context.App; __resolutionError = null;` + reset-backing-fields dance that `ExecuteAsync` does today, then have `validate`'s Build-pass do:

    handler.SetAction(action, context);
    var result = await ((IClass)handler).Build();

Validate iteration shape:

    foreach (var action in step.actions)
    {
        var handler = modules.GetCodeGenerated(action).Handler;
        if (handler is not IClass classified) continue;
        classified.SetAction(action, context);
        var buildResult = await classified.Build();
        if (!buildResult.Success) { aggregate to errors; break out }
        if (buildResult.Value is string typeName)
            StampOnTerminalVariableSet(step, typeName);
    }

`StampOnTerminalVariableSet`: walk `step.actions` backwards to find the last action with `Module=="variable" && ActionName=="set"`; update its `Parameters` list — find or insert a Data named "type" with the typeName value. Stage 4 adds precedence (user `(type)` hint wins over Build() inference): skip the stamp if a Parameter named "type" already exists with a non-default value.

### Stages 1–4

Untouched. See `.bot/typed-action-returns/architect/stages.md` and `.bot/typed-action-returns/test-designer/v1/plan.md` for the contract.

## Test status at handoff

- C# tests: build clean. 5 known regressions before OOM-kill on full run:
  - `TypeMapping_ResolvesTranslatable` — rewritten to expect null (intentional).
  - `TypeMapping_ResolvesTString` — should pass now after PlangType marker added back to TString (verify).
  - `StepLoop_ShortCircuits_OnShouldExitTrue`, `ShouldExit_True_ExitTypedResult`, `GoalRunFrom_ShortCircuits_OnExitTypedResume` — unclear if related to my changes; need targeted run.
- All Stage0_/Stage1_/Stage2_/Stage3_/Stage4_ tests still `Assert.Fail` — those are the contract to make green.

## Sequencing recommendation for the next coder

1. Source-gen `SetAction` emission (one edit to `PLang.Generators/Emission/Action/this.cs`)
2. Validate Build() iteration in `PLang/app/modules/builder/code/Default.cs Validate(...)`
3. Stage0 test bodies (8 IClass/validate + 7 Data + 9 channels + 6 PlangType-removal — last two need reframing per above)
4. Stage 1 rename — straight rename, only watches out for the divergent name handling (drop `[PlangType("testfile")]` if still present)
5. Stages 2/3/4 as architected

The architectural calls in this branch are dense and now baked into the diff. The remaining work is mechanical relative to that.
