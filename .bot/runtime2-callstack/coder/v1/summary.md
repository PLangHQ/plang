# coder — runtime2-callstack — v1

## What this is

Implementation of the architect's callstack plan and the test-designer's contract.
Everything land in 7 commits on `runtime2-callstack`.

## What was done

### Phases 1–9 (the big refactor — `01cfc14f`)

- **`Call.@this`** in OBP folder layout (`PLang/App/CallStack/Call/this.cs`).
  Replaces `CallFrame`. Properties: `Id`, `Action`, `Caller`, `Cause`, `Errors`,
  `Handled`, `Children`, `StartedAt`, `CompletedAt`, `Duration`, `Diffs`, `Tags`.
  Methods: `Tag(k,v)`, `GetItem<T>`/`SetItem<T>` (typed metadata bag),
  `SnapshotChain()`, `IAsyncDisposable`. Drops `EventId`/`Indent`/`Phase`/single-`Error`/
  `Variables`-snapshot from the old shape.
- **`CallStack.@this`** rewritten — instance-level `AsyncLocal<Call?> _current`
  (was static — broke parallel test instances), `Audit` (was `Errors`), `Root`,
  `Flags : CallStackFlags`, locked `Children` mutation for parallel branches.
- **`CallStackFlags`** — record struct gating `Timing/Diff/DeepDiff/Tags/History/MaxFrames`.
  `Default` = all-off, `Shorthand` = Timing+Tags on. Settable on the live stack so
  `App.Debug.Apply` can update it from `--debug={callstack:...}`.
- **`App.Errors.@this`** — new namespace `@this` with AsyncLocal `Error`, audit `All`,
  `Push(IError) : IDisposable`. Replaces `Context.Error` / `vars.Set("!error", ...)`.
- **App.Run reshape** — `await using var call = stack.Push(action, vars, cause)`,
  populates `call.Errors` + `stack.Audit` on failure, `call.SnapshotChain()` post-push
  so chain[0] IS the failing Call.
- **error.handle.Wrap** — dispatches recovery via `using (app.Errors.Push(caught))`,
  `cause: erroredCall` threaded through `App.Run`, flips `Handled=true` on success.
- **Variables.@this** gains collection-level `OnSet`/`OnCreate`/`OnRemove` events
  fired at the existing rebind/first-set/Remove sites (commit `1a22f388`).
- **Source generator** emits `Call.@this` chain via `SnapshotChain()` instead of
  the old `GetFrames()`.
- **Cycle detection** — `MaxDepth` + `ContainsGoal` enforced on Push, refined to
  fire only on goal-boundary crossings using `PrPath` for identity (commit `1881d6c0`).
- **Old test files deleted** — `CallFrameTests`, `CallStackTests`,
  `CallStackIntegrationTests`. `EngineTests` / `AppRunScaffoldingTests` /
  `ModifierTests` updated to use `Current` ref equality for push/pop symmetry.

### Test contract (commits `452eb3d3`, `e2e58830`, `5add6f21`)

- **95 C# TUnit tests across 15 files** — Call shape, tree mechanics, AsyncLocal
  forks, Cause linkage, cycle detection, flags, Items extension, Diff capture
  (incl. memory-bound test absorbed from PLang P8), Audit, SnapshotChain,
  app.Errors.Push LIFO, ServiceError chain, Variables collection events, tag
  action handler, --debug={callstack:...} JSON parse.
- **16 PLang test goals + 8 helpers** under `Tests/App/CallStack/`:
  - 4 standalone (ErrorVarIsNullOutsideHandler, HandledFlagSetWhenRecoverySucceeds,
    DirectGoalCycleThrows, Diffs).
  - 12 with helper goals: `Inner`, `CycleA`+`CycleB`, `ChainOuter`+`ChainMiddle`+
    `ChainThrows`, `ThrowItem`, `CaptureCause`, `OuterRecoveryWithInnerThrow`, `SlowGoal`.
- **`Call.@this` gains two PLang-friendly properties**: `Depth` (Caller-chain length,
  derived) and `Chain` (foreach-iterable list — drops the need to walk
  `%!callStack.Current.Caller.Caller…`).
- **`debug.md`** updated with `--debug={callstack:...}` reference: shorthand `true`,
  per-flag table (timing/diff/deepDiff/tags/history/maxFrames), PLang reading paths.

### Cycle-detection refinement (commit `1881d6c0`)

A bug discovered while writing tests:

- The naive "ContainsGoal-on-Push throws if goal name in chain" tripped on every
  consecutive action within the same goal (orchestrator dispatching elseif,
  foreach iterating, retry re-firing). Three pre-existing failures
  (`MultiBranch_SecondBranchMatches_BranchIndexIs1`,
  `MultiActionOrchestrate_InnerElseIfMatches_FilterSkipsPhantomSites_SubStepsRun`,
  `Foreach_BodyInnerGoalFailsInsideConditionIf_PropagatesError`) were all the
  same cause.
- Fixed: cycle check now only fires when the new action's goal *differs* from the
  caller's goal — a real boundary crossing. Direct recursion (A→A within one
  goal frame) terminates at MaxDepth (PLang has no boundary marker between
  consecutive same-goal Pushes); indirect cycles (A→B→A) trip ContainsGoal at
  the boundary.
- Identity uses `Goal.PrPath` not `Goal.Name` (Name can collide across an app's
  goal tree).

## Code example — App.Run with the new shape

```csharp
public async Task<Data.@this> Run(Action action, Context context, Call? cause = null)
{
    var (handler, error) = Modules.GetCodeGenerated(action);
    if (error != null) return Data.@this.FromError(error);

    var stack = Debug.CallStack;
    await using var call = stack.Push(action, context.Variables, cause);

    var previousStep = context.Step;
    var previousGoal = context.Goal;
    context.Step = action.Step;
    context.Goal = action.Step?.Goal;

    try
    {
        var result = await handler!.ExecuteAsync(action, context);
        if (!result.Success && result.Error is Errors.Error err)
        {
            call.Errors.Add(result.Error!);
            stack.Audit.Add(result.Error!);
        }
        return result;
    }
    catch (Exception ex) when (ex is not (NRE or OOM or SOE))
    {
        var sv = new ServiceError(ex.Message, action.Step!,
            call.SnapshotChain(), "ServiceError", 400) { Exception = ex };
        call.Errors.Add(sv);
        stack.Audit.Add(sv);
        return Data.@this.FromError(sv);
    }
    finally
    {
        context.Step = previousStep;
        context.Goal = previousGoal;
        // await using disposes the Call here:
        // AsyncLocal restore + Children removal (history off) + OnSet unsubscribe
    }
}
```

## Test results

- **C# (TUnit):** 2580 / 2580 pass.
- **PLang `--test`:** 16 callstack test goals are written but cannot be executed yet
  — the build pipeline on `runtime2` (and therefore this branch) fails with
  `[TypeMismatch] Cannot convert String to this` during `builder.validateResponse`
  → cascades to a NullReference in `DefaultFileProvider.Save:86` (Path was never set).
  Verified pre-existing by reproducing on the `runtime2` base branch with no
  callstack changes. Investigating in a follow-up commit on this branch — the
  hint that the builder system prompt may be teaching the wrong parameter shape
  is the lead.

## Files changed

Created:
- `PLang/App/CallStack/Call/this.cs`, `CallStackFlags.cs`, `Diff.cs`
- `PLang/App/Errors/this.cs`
- `PLang/App/modules/debug/tag.cs`
- 15 C# test files under `PLang.Tests/App/{CallStackTests,Errors,VariablesTests,Modules/debug,Debug}/`
- 8 PLang helper goals under `Tests/App/CallStack/`

Replaced:
- `PLang/App/CallStack/this.cs` (AsyncLocal tree, Audit, cycle detection)
- `PLang/App/CallStack/SerializableCallStack.cs` (typed against Call.@this)

Modified:
- `PLang/App/this.cs` (App.Run; new Errors property)
- `PLang/App/Actor/Context/this.cs` (CallStack passthrough; drop Error)
- `PLang/App/Actor/this.cs` (drop CallStack construction)
- `PLang/App/Debug/this.cs` (own CallStack; parse --debug={callstack:...})
- `PLang/App/Variables/this.cs` (collection-level events)
- `PLang/App/modules/error/handle.cs` (Cause linkage; app.Errors.Push)
- `PLang/App/Errors/{IError,Error,ServiceError}.cs` (CallFrames typed Call.@this)
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` (RunAsync threads cause)
- `PLang.Generators/Emission/Action/this.cs` (SnapshotChain instead of GetFrames)
- `PLang/App/GlobalUsings.cs` (drop CallFrame alias; note Call collision)
- `PLang/.gitignore` (whitelist `App/modules/debug/`)
- `PLang.Tests/GlobalUsings.cs`, several test files updated for the new shape
- 16 `.test.goal` files under `Tests/App/CallStack/`
- `Documentation/v0.2/debug.md`

Deleted:
- `PLang/App/CallStack/CallFrame.cs`
- `PLang.Tests/App/Core/{CallFrameTests,CallStackTests,CallStackIntegrationTests}.cs`

## Status / what's next

Done with the C# implementation. **Still investigating** the build pipeline
issue blocking PLang test execution. The hint to follow: the LLM may be returning
the wrong parameter shape because the system prompt is teaching it incorrectly.
`--debug={"llm":{"system":true,"response":true}}` will show what the LLM sees
and emits.

Ready for codeanalyzer review of the C# code while I dig into the builder.
