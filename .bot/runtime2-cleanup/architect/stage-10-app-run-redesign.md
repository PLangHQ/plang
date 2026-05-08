# Stage 10: `app-run-redesign`

**Read first:**
- `plan/principles.md` — OBP discipline, especially smell #4 (allocate-here / mutate-there / clean-up-elsewhere) and Rule E (decomposed parameters → navigation).
- `plan/scope-map.md` — App is the bootstrap root; Context is per-actor; CallStack is shared today (per-context scope deferred per todos.md).

**Goal:** Reduce `App.Run` from ~85 lines and ~10 foreign mutations on `context` to ~5 lines, by extracting two new abstractions:

- **`Context.@this.AnchorScope(action)`** — a using-disposable that captures the current `Step`/`Goal`/`Event`/`Step.Context` anchors, sets them to the action's, and restores on dispose.
- **`Call.@this.ExecuteAsync(handler, context)`** — a method on the per-Call frame that wraps handler invocation, error stamping (`SnapshotParams`, `CallFrames`), and audit-collection.

App.Run shrinks to: get handler → push call frame → set anchors → execute. Each piece is one method call.

**Scope:**
- *Included:* design + implement `Context.AnchorScope(action)` and `Call.ExecuteAsync(handler, context)`; rewrite `App.Run` using both; preserve all existing behavior (CallStackOverflow handling, OCE swallowing, error stamping, anchor restoration).
- *Excluded:* `app.Variables` / `app.Context` shortcut removal — that's stage 22 (`app-shortcuts-drop`). They're bigger sweeps than stage 10's structural refactor and deserve their own focused session. The earlier note in plan.md that stage 10 might fold them in is reversed: split.
- *Excluded:* CallStack scope question (per-context vs shared) — filed in `Documentation/Runtime2/todos.md`. Stage 10 doesn't change scope.
- *Excluded:* Any change to `Goals.Goal.this.cs:288`'s parallel CallStack.Push call site — that's a separate location with its own Run shape; stage 10 only touches `App.this.cs`.

**Deliverables:**

### New abstractions

**`PLang/App/Actor/Context/this.cs`** — gain `AnchorScope(action)`:

```csharp
/// <summary>
/// Captures the current Step/Goal/Event/Step.Context anchors and sets them
/// to the action's for the dispatch's lifetime. On dispose, restores.
/// Used by App.Run to scope the dispatch context — parallel dispatches of
/// the same Step (legal under Task.WhenAll on `goal.call`) don't leave a
/// sibling branch's Context pointer leaked on the shared Step instance.
/// </summary>
public IDisposable AnchorScope(Goals.Goal.Steps.Step.Actions.Action.@this action) { ... }
```

The disposable captures previous values, swaps in the action's, and restores on Dispose. Internal nested struct or sealed class — coder's choice; the type is private to Context's surface.

**`PLang/App/CallStack/Call/this.cs`** — gain `ExecuteAsync(handler, context)`:

```csharp
/// <summary>
/// Executes the resolved handler under this Call frame. Wraps:
///   - handler.ExecuteAsync(action, context) invocation
///   - error stamping: SnapshotParams onto Error.Params, CallFrames from
///     this.SnapshotChain() if not already set
///   - this.Errors.Add(err) and CallStack.Audit.Add(err) on failure
///   - OperationCanceledException swallowing into ServiceError (timeout.after
///     contract: inner action's generated ExecuteAsync swallows OCE; this
///     catch is the safety net for handlers that bubble it differently)
/// Returns the handler's result (or a ServiceError-wrapped result on
/// caught exception).
/// </summary>
public async Task<Data.@this> ExecuteAsync(ICodeGenerated handler, Actor.Context.@this context) { ... }
```

This method holds the inner try/catch that App.Run currently has at lines 466-497. It uses `this.Action` (already on Call) and `this.SnapshotChain()` (already on Call) so it doesn't need extra parameters.

The CallStack.Audit reach: Call holds a back-ref to its parent CallStack today (`Audit` is on CallStack). Verify Call has access — if not, the brief sets up the wiring.

### App.Run after refactor

```csharp
public async Task<Data.@this> Run(Goals.Goal.Steps.Step.Actions.Action.@this action,
                                   Actor.Context.@this context,
                                   CallStack.Call.@this? cause = null)
{
    var (handler, error) = Modules.GetCodeGenerated(action);
    if (error != null) return Data.@this.FromError(error);

    Call.@this call;
    try { call = CallStack.Push(action, context.Variables, cause); }
    catch (Errors.CallStackOverflowException ex) { return HandleOverflow(ex, action.Step, cause); }

    await using var _disposable = call;
    using var _anchor = context.AnchorScope(action);
    return await call.ExecuteAsync(handler, context);
}

private Data.@this HandleOverflow(Errors.CallStackOverflowException ex,
                                   Goals.Goal.Steps.Step.@this? step,
                                   Call.@this? cause)
{
    var chain = cause?.SnapshotChain() ?? Array.Empty<Call.@this>();
    var err = new Errors.ServiceError(ex.Message, step!, chain, "CallStackOverflow", 500) { Exception = ex };
    CallStack.Audit.Add(err);
    return Data.@this.FromError(err);
}
```

Six lines for the happy path. The CallStackOverflow handler stays close (its own private method) because it can't fold into ExecuteAsync — overflow happens at Push, before the Call frame exists.

### Behaviour to preserve precisely

**1. CallStackOverflowException at Push time.**
Current code (line 441) calls `Push` inside a try/catch. Overflow exits via `Data.@this.FromError`. The post-refactor private `HandleOverflow` method preserves the same behaviour: snapshot chain from the *caller* (which exists), build a ServiceError, add to Audit, return.

**2. OperationCanceledException swallowed.**
Current code catches OCE *inside* the handler-execution try/catch at line 489. The comment is explicit: timeout.after depends on this — the inner action's generated ExecuteAsync swallows OCE into a ServiceError result; Step.RunAsync deliberately re-raises OCE. App.Run swallowing OCE is intentional.

After refactor: the OCE catch lives inside `Call.ExecuteAsync`. Same behaviour, same scope (only handler-execution path catches OCE — not the Push, not the AnchorScope).

**3. Error stamping** (SnapshotParams, CallFrames).
Current code (lines 472-481): on result.Error, stamp `err.Params = handler.SnapshotParams()` if null; stamp `err.CallFrames = call.SnapshotChain()` if empty; add to `call.Errors` and `stack.Audit`.

After refactor: this lives inside `Call.ExecuteAsync`. The ICodeGenerated handler is the parameter (already in scope); SnapshotChain is `this.SnapshotChain()`; Errors.Add and Audit.Add use Call's own fields. Identical stamping logic.

**4. Anchor save+restore** (Step, Goal, Event, Step.Context).
Current code (lines 458-464 + finally 503-506): captures prev*, sets to action's, restores in finally. The `using var _ = context.AnchorScope(action)` replaces all of this. The disposable's struct holds the captured prev* values; Dispose restores.

**5. await using order.**
Current code: `await using var _ = call;` (line 452) then the try-finally for anchor restore. The Call's IAsyncDisposable runs *after* the finally block. After refactor: same order — `await using var _disposable = call;` declared *before* `using var _anchor = ...;`. C# disposes in reverse order of declaration; `_anchor` (sync) disposes first, restoring anchors; `_disposable` (async) disposes second, popping the Call.

This ordering matters: Call's dispose pops the AsyncLocal, unsubscribes Variables.OnSet, removes from Children. Anchor dispose only restores the Context's Step/Goal/Event pointers. The current code's finally→`await using` dispose ordering is preserved by the declaration order in the new code.

### Files touched

**Modified (3):**
- `PLang/App/Actor/Context/this.cs` — add `AnchorScope(action)` method + the private nested disposable type.
- `PLang/App/CallStack/Call/this.cs` — add `ExecuteAsync(handler, context)` method.
- `PLang/App/this.cs` — `App.Run` rewritten (~85 → ~10 lines including HandleOverflow helper).

**No file relocations, no caller sweeps.** The new methods are internal to App.Run's flow.

### Risk + dependencies

**Risk: medium.** Behavior-preserving refactor, but the existing App.Run has subtle ordering and contract guarantees (OCE swallowing, dispose order, error stamping). Each subtlety is named in "Behaviour to preserve precisely" above; the build + test suite catches any miss.

Possible failure modes:
1. **Dispose order regression** — if the `await using call;` and the AnchorScope `using` are declared in the wrong order, Call's dispose runs while anchors are still pointing at the dispatched action. Likely benign (Call dispose doesn't read anchor state), but follow the order in the sketch above to be safe.
2. **OCE leaking past Call.ExecuteAsync** — make sure the OCE catch is *inside* ExecuteAsync, not around the call to it. App.Run's outer flow shouldn't catch OCE; only ExecuteAsync's inner handler-execution does.
3. **Error stamping missing on the catch path** — current code stamps `serviceErr.Params = handler.SnapshotParams()` even on the catch-Exception path (line 493). After refactor, both paths must stamp.
4. **Tests covering the dispatch flow** — `PLang.Tests/App/Goals/`, `PLang.Tests/App/CallStack/`, `PLang.Tests/App/Core/EngineTests.cs`. Run after the refactor.

**Dependencies: none on stages 11/12** (they're independent ownership realignments). Stage 10 builds on the trunk after stage 9.

### Tests

**No new tests required.** Behavior is preserved.

**Existing test coverage to verify:**
- `PLang.Tests/App/Core/EngineTests.cs` — Engine + RunAction tests.
- `PLang.Tests/App/CallStack/` — CallStack snapshot tests, frame assertions.
- `PLang.Tests/App/Modules/` — module dispatch.
- `Tests/` — full PLang suite (the dispatch path is exercised by every action invocation).

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- App.Run is ≤ 15 lines including HandleOverflow helper (target ~10).
- The two new methods (`Context.AnchorScope`, `Call.ExecuteAsync`) exist and are reachable.

### Watch for (coder eyes-on)

- **The private `_disposable` for Call vs the public `IAsyncDisposable` interface** — Call.@this is `IAsyncDisposable`. The `await using` works because it's on the interface. After refactor, ensure the variable still binds to the interface-typed receiver.
- **Step.Context pointer sharing** — line 463's `if (context.Step != null) context.Step.Context = context;` is the "shared Step instance under parallel dispatch" guard. Make sure AnchorScope's setter does the same.
- **The `ICodeGenerated` parameter type on Call.ExecuteAsync** — verify this is the right interface (it might be `IClass`, `IHandler`, or similar — read the existing `handler.ExecuteAsync(action, context)` call site to see what type `handler` is). Today's line 426 says `Modules.GetCodeGenerated(action)` — suggests `ICodeGenerated` is the type. Confirm.
- **Call.@this needs reach to `CallStack.@this.Audit`** — Call already has a parent CallStack reference (it's added to one), but verify before assuming `this.Audit` works in ExecuteAsync. If Call doesn't have direct access, ExecuteAsync may need the CallStack passed in (Rule E says navigate, not pass — but if no navigation chain exists today, the brief may be premature).
- **CallStackOverflow at Push** — the catch only triggers when the depth limit OR ContainsGoal cycle trips. Both are inside Push. The catch must stay tight to Push, not around the whole Run. The sketch's `try { call = CallStack.Push(...); } catch (...)` with the call frame variable declared *outside* the try is important — the rest of Run uses `call` after the try block. Don't move the variable declaration inside.
- **Error stamping order matters** — current code stamps `Params` *before* `CallFrames`. The contract on both is "if not already set, set." After refactor, preserve order.
- **The 4 captured anchor values** — verify all 4 (`previousStep`, `previousGoal`, `previousEvent`, `previousStepContext`) get restored in AnchorScope's Dispose. Easy to miss one when extracting.

### Stages that follow this one

- **Stage 11** (`errors-app-backref-drop`) — independent of stage 10; can land in any order after.
- **Stage 12** (`build-branch-to-build-this`) — independent.
- **Stage 22** (NEW: `app-shortcuts-drop`) — removes `app.Variables` / `app.Context` shortcuts on App.this.cs:222-223. ~25 caller sweep (2 production + ~20 test files). Carved separately because the sweep is the work, not coupled to the App.Run refactor.

### Out of scope

- The `app.Variables` / `app.Context` shortcut removal — stage 22.
- CallStack scope (per-context vs shared) — todos.md.
- Any change to `Goals.Goal.this.cs:288`'s separate CallStack.Push call — different file, different shape.
- The "Wires dormant CallStack as a side-effect" claim from the plan one-liner — already happens today (line 441's `stack.Push` and Goals.Goal:288's `Push`). Stage 10 doesn't introduce CallStack wiring; the 2026-04-27 TODO is partially stale on this point.

## Commit plan

```
runtime2-cleanup stage 10: App.Run reduced to ~10 lines via two new abstractions

App.Run was 85 lines with ~10 foreign mutations on context (Step, Goal,
Event, Step.Context) and an inline try/catch for handler execution that
stamped errors with SnapshotParams + CallFrames.

Two new abstractions extract the structural patterns:

  Context.@this.AnchorScope(action)
    - using-disposable that captures previous Step/Goal/Event/Step.Context
    - sets them to the action's
    - restores on dispose

  Call.@this.ExecuteAsync(handler, context)
    - wraps handler.ExecuteAsync(action, context)
    - stamps error.Params (SnapshotParams) and error.CallFrames
    - adds error to this.Errors and CallStack.Audit
    - swallows OperationCanceledException into ServiceError result
      (timeout.after contract — see CallStack.@this docs)

App.Run after refactor: get handler → push call frame (with overflow
catch tight to Push) → AnchorScope → call.ExecuteAsync. Six lines for
the happy path; HandleOverflow helper stays close because overflow
happens at Push-time before the call frame exists.

Behaviour preserved precisely:
- CallStackOverflowException catch tight to Push only.
- OCE swallowed inside Call.ExecuteAsync (not in App.Run's outer flow).
- SnapshotParams and CallFrames stamped in same order as before.
- Dispose order preserved: anchor restore before await-using Call dispose.

Out of scope: app.Variables / app.Context shortcut removal (stage 22);
CallStack scope (todos.md).
```
