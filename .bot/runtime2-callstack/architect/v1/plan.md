# CallStack — Causal Frame Model with Auto-Materialization on Error

## Why this exists

Today's `App/CallStack/this.cs` is partly wired. `App.Run` pushes a frame per action and pops in `finally` (`PLang/App/this.cs:391, 426`), but:

- `frame.Error` is never assigned when an action fails. `frame.Errors.Add` is never called. `CallStack.Errors` (the run-wide accumulator) is always empty.
- `%!error%` reads from a parallel `Context.Error` property set/restored by `error.handle.Wrap`. Two sources of truth.
- `IsEnabled=false` is incoherent: `Push` returns a transient un-pushed frame, but `App.Run`'s `finally` calls `PopAsync` unconditionally. Works only by coincidence (empty stack → no-op pop).
- The CallStack lives on `Actor.Context.CallStack` and is unconditionally constructed at `Actor.this.cs:87`. Wrong home — it's a debug/observability concern, not an actor concern.
- No variable-diff capture. `Frame.Variables` exists but `SnapshotVariables` only computes `GetChangedSince(StartedAt)` once on pop.
- No causal "spawned by" link distinct from "called by" — needed for events, scheduled goals, callbacks.
- No render-agnostic data shape. Today's `GetStackTrace` is a single string formatter; nothing supports flamegraph or causal-graph projections.

This branch installs the data model for all of the above and wires the runtime to populate it correctly. Renderers and consumer surfaces beyond `%!error.CallFrames%` and the existing error message integration are explicitly out of scope (tracked separately).

## Settled design

### Ownership and lifecycle

- CallStack moves from `Actor.Context.CallStack` → `App.Debug.CallStack`.
- Off by default. Lazy-allocated only when `--debug={callstack:true}` is set on startup.
- Configuration shape:
  ```jsonc
  --debug={
    callstack: true,                 // shorthand for { enabled: true, ...defaults }
    callstack: {
      enabled: true,
      compress: "auto" | "off",      // render-time folding (default: "auto")
      maxFrames: 1000,               // storage cap, oldest dropped (default: 1000)
      keepHistory: false             // retain popped frames? (default: false)
    }
  }
  ```
- Even when CallStack is disabled, **frames are auto-materialized on error** by walking `action.Step.Goal.Parent` from the failing action. Reconstructed frames are flagged `Reconstructed=true` so consumers know there are no diffs/siblings/timing — only the static path.

### Frame becomes Call

Rename `App.CallStack.CallFrame` → `App.CallStack.Call.@this` (OBP @this convention). The collection is `CallStack.Calls`. Same shape, new home.

Reads natural in C#: `call.Action.Module` (the static action def the call invoked).
Reads natural in PLang: `%!callStack.Calls[0].Action.Step.Text%`.

The rename is mechanical — every existing `CallFrame` reference becomes `Call.@this`.

### Call shape (all new properties added)

```
App.CallStack.Call.@this
{
    Id              : string                 — unique frame id (already present)
    Action          : Action.@this           — ref to static action def (OBP, already present)
    Caller          : Call.@this?            — parent in this run's call chain (was Parent)
    Cause           : Call.@this? | string?  — what spawned this run (event handler trigger,
                                                schedule frame, callback identifier). Null for
                                                normal goal.call descents.
    StartedAt       : DateTime               — already present
    CompletedAt     : DateTime?              — already present
    Duration        : TimeSpan               — already present (Stopwatch-backed)
    Errors          : List<IError>           — list of errors observed (retries append).
                                                Already declared, just never populated.
    Handled         : bool                   — flipped true by error.handle.Wrap on
                                                successful recovery. Replaces today's
                                                ad-hoc `Phase = ExecutionPhase.Error`.
    Recovering      : bool                   — true while a recovery body is running
                                                (only relevant in option A — N/A for B).
                                                NOT introduced; kept here as note that
                                                Option B avoids the need.
    Diffs           : List<Diff>             — variable mutations during this call's lifetime.
                                                Empty unless --debug={callstack:true}.
    Tags            : Dict<string,string>    — annotations from C# handlers and `tag` action.
    Reconstructed   : bool                   — true when materialized from action.Step.Goal walk
                                                rather than pushed at execution time.
    EventId         : string?                — already present
    Indent          : int                    — already present
    Children        : List<Call.@this>       — retained when keepHistory=true; otherwise null
}

App.CallStack.Diff (new record)
{
    Name            : string         — variable name
    Before          : object?        — value at frame start (or last set)
    After           : object?        — new value
    At              : DateTime       — when the change happened
}
```

`Caller` replaces today's `Parent` field — same role, renamed to clarify against `Cause`. The semantics are: `Caller` is the synchronous parent in the same execution chain; `Cause` is the asynchronous origin (event subscription frame, schedule registration, callback issuer) — null for normal `goal.call` descents where Caller alone is the truth.

### Recovery scope: Option B (synthetic recovery frame)

When `error.handle.Wrap` catches an error and is about to run the recovery body, it pushes a synthetic frame:

```
Stack before error:        Stack during recovery:
  step5.http.get             step5.http.get  (errored, Handled=false yet)
                             recovery        (synthetic, Cause = step5.http.get)
                               log_error     (recovery body action)
                               retry         (recovery body action)
```

On recovery success: pop the recovery frame, mark `step5.http.get.Handled = true`. On recovery failure: error chain propagates (existing `ErrorChain.Add(recoveryError)` mechanism on the original error), recovery frame pops with its own `Errors` populated.

This means **`%!error%` becomes the walk-back rule**: walk `Caller` chain, return the most recent `Errors.Last()` from a frame whose `Handled == false`. Drop `Context.Error` entirely.

Why B over A: nested recovery (recovery body action also errors and has its own recovery handler) requires only "skip handled frames" in the walk. Option A would require a third state ("in-recovery") and a flag check. B is fewer concepts and more honest in the rendered trace.

### Sibling-group split: Option X (temporal/causal)

Compression rule for `%!callStack%` rendering:

> Collapse consecutive sibling Calls that share the exact same `(Caller, Action.Module, Action.Step.Index)`. Break the group at:
> - any sibling with a different `(Module, Step.Index)`
> - any sibling whose `Errors` is non-empty (errored frames always render standalone)

Result: a foreach-over-247-orders where iteration #2, #78, #120, #230 had errors renders as alternating compressed groups + standalone errored frames + a final fully-expanded errored frame for the unhandled one. See the worked example in this branch's design discussion (transcript) for the full layout.

A separate flamegraph projection (out of scope here — future view) uses the same Call tree but aggregates by `(Module, Step.Index)` regardless of position. Same data, different fold.

### Auto-materialization on error

In `App.Run`'s catch path (and on `!result.Success` from the handler), if `App.Debug.CallStack` is null OR `IsEnabled == false`, materialize a Call list by walking from the failing action upward:

```csharp
static IReadOnlyList<Call.@this> Reconstruct(Action.@this errored)
{
    var list = new List<Call.@this>();
    var current = errored;
    Call.@this? caller = null;
    while (current != null)
    {
        var call = new Call.@this(current, caller) { Reconstructed = true };
        list.Add(call);
        caller = call;
        current = current.Step?.Goal?.Parent?.LastAction; // walks up via parent goal
    }
    return list;
}
```

The exact walk depends on what `Goal.Parent` exposes — likely `Goal.Parent.CurrentStep.CurrentAction` or similar. Walked path is the static call path, not an execution log. Reconstructed frames have empty Diffs, no Children, no Duration data — flagged accordingly.

`ServiceError` already accepts `IReadOnlyList<CallFrame>` in its constructor (`App/this.cs:418`). After rename, it accepts `IReadOnlyList<Call.@this>`. Both auto-materialized and live frames flow through the same constructor.

### Variable diffs via Variables.Events

`App.Variables.@this` grows an event surface (mirrors the existing `App.Events.@this` pattern):

```
App.Variables.@this
{
    ...existing...
    public Events.@this Events { get; }   // OBP child, mirrors App.Events shape
}

App.Variables.Events.@this
{
    public event Action<string, object?, object?> OnSet;       // (name, before, after)
    public event Action<string> OnRemove;
    public event Action<string, object?> OnCreate;
}
```

When a Call is pushed (live, not reconstructed), it subscribes `Variables.Events.OnSet` with a handler that appends to `call.Diffs`. Unsubscribes on pop. Standard scoped-subscription pattern.

For the `OOM-on-large-variables` concern from the user's prior experience (10000 × 10MB):

- Default behavior: only stringify scalar-ish values (`int`, `bool`, `string` ≤ 256 chars, `decimal`, `DateTime`). Non-scalar values render as `"<List<Order> @ 5042 items>"` — type and size only, no content.
- Opt-in via `--debug={callstack:{enabled:true, deepDiff:true}}` for full deep-clone capture. Off by default.

This keeps `--debug={callstack:true}` cheap enough for the loops that previously OOM'd.

### Tag surface

`call.Tags : Dict<string,string>` is written by two surfaces:

1. **C# handler** writes via `Context.CallStack.Current?.Tag(key, value)`. No-op if no current call (callstack disabled and no error materialized yet). Use case: cache modules tag `cache.hit=true`, http modules tag `status=503`, llm modules tag `tokens=2400`.

2. **PLang developer** writes via a new `tag` action: `- tag step "checkout-flow" critical=true`. The `tag` handler resolves the current call (if any) and writes; otherwise no-op. Outside callstack scope, tags can also flow to `Context.Trace` for general logging — that's the developer's intent regardless of callstack state.

The `tag` PLang action is a tiny new module action — added under `PLang/App/modules/debug/tag.cs` or similar. Implementation fits the existing module pattern.

### Cancellation lane

Cancellation is rendered visually distinct from error. Frames know they cancelled via `frame.Errors` having an `OperationCanceledException`-derived entry (already excluded from App.Run's catch). Renderer shows `× cancelled` instead of `✗ failed`.

No data model change — purely render-side. Mentioning here for completeness so the renderer spec covers it.

### ErrorChain integration

`Errors/Error.cs:35` already has `List<IError> ErrorChain`. `error/handle.cs:97,110` already appends recovery failures. The render path consumes this when present, displays as "caused by:" cascade. No code changes needed beyond the renderer.

## Migration plan

This is a behavior-preserving refactor in spirit but touches enough places that it can't be cosmetic-only.

### Phase 1: Move CallStack to App.Debug; off by default

- New property: `App.Debug.@this.CallStack : App.CallStack.@this?`. Lazy allocated when `--debug` JSON includes `callstack:true|{...}`.
- Remove unconditional construction at `Actor.this.cs:87`.
- Update `Actor.Context.@this.CallStack` getter to read through to `App.Debug.CallStack`. Keep the property on Context so PLang `%!callStack%` still resolves through `Context.@this`.
- DynamicData registration at `Actor/Context/this.cs:176` (`vars.Set(new Data.DynamicData("!callStack", () => CallStack))`) becomes `() => App.Debug.CallStack` (resolves to null when disabled — `%!callStack%` returns null cleanly).

### Phase 2: Rename CallFrame → Call.@this

Mechanical rename:
- `App/CallStack/CallFrame.cs` → `App/CallStack/Call/this.cs` (OBP @this convention).
- `App/CallStack/SerializableCallFrame.cs` → `App/CallStack/Call/Serializable.cs` or kept under `Serializable/` per existing pattern.
- All `CallFrame` references → `Call.@this`. Sites: `App/this.cs:387,388,391,424,426`, `App/CallStack/this.cs` (multiple), `App/Errors/Error.cs:24`, `App/Errors/Exceptions.cs`, `App/Debug/this.cs:148` (uses for OnChange context).
- Property `Parent` → `Caller`. New nullable property `Cause : Call.@this?`.

### Phase 3: Wire frame errors

In `App.this.cs:380-431` (`App.Run`):

```csharp
// Replace the unconditional Push/Pop with enabled-aware push and explicit error path:
var callStack = App.Debug.CallStack;
var pushed = (callStack?.IsEnabled ?? false) ? callStack.Push(action) : null;

try
{
    // save context anchors (existing code)
    var result = await handler!.ExecuteAsync(action, context);

    // Mark errors on frame
    if (!result.Success && pushed != null)
    {
        pushed.Errors.Add(result.Error!);
        callStack.Errors.Add(result.Error!);   // run-wide accumulator
    }

    // existing SnapshotParams handling
    return result;
}
catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
{
    // Materialize frames if needed for ServiceError
    var callFrames = pushed != null
        ? callStack!.GetCalls()
        : Reconstruct(action);

    var serviceErr = new ServiceError(ex.Message, step, callFrames, "ServiceError", 400) { Exception = ex };
    serviceErr.Params = handler!.SnapshotParams();

    if (pushed != null)
    {
        pushed.Errors.Add(serviceErr);
        callStack!.Errors.Add(serviceErr);
    }
    return Data.@this.FromError(serviceErr);
}
finally
{
    if (pushed != null)
    {
        pushed.SnapshotVariables(context.Variables);
        await callStack!.PopAsync();
    }
    // restore context anchors (existing code)
}
```

Delete the `IsEnabled=false → return new CallFrame(action) without push` path on `CallStack.Push` — replace with: when `IsEnabled=false`, callers do not invoke `Push`; auto-materialization handles error reconstruction. `PushError` (today's separate-on-demand path) is removed in favor of the unified flow.

### Phase 4: Recovery scope (Option B)

In `error/handle.cs` (`Wrap` method, around lines 96-110):

- Before invoking the recovery body, push a synthetic recovery frame:
  ```csharp
  var recoveryCall = callStack?.PushRecovery(originalCall, recoveryError);
  ```
  where `PushRecovery` is a new method that creates a Call with `Cause = originalCall` and a synthetic Action representing "recovery scope" (no underlying static action, or a sentinel built from the recovery goal's first action).
- Run recovery body. Recovery actions push under the recovery call as Caller chain.
- On success: pop recovery, mark `originalCall.Handled = true`.
- On failure: pop recovery (with its own errors), append recovery's error to `originalCall.Errors.ErrorChain`.

Drop `context.Error` set/restore (lines 133, 140). Drop the `IError? Error` property on `Actor.Context.@this` (line 109). Drop the `Context.Error` DynamicData registration (line 185 — replace with stack walk).

### Phase 5: %!error% as stack walk

Replace `Actor.Context.Variables` registration of `!error`:

```csharp
// was:  vars.Set(new Data.DynamicData("!error", () => Error));
//        with Error being context-level property
// now:  walk the call stack for the most recent unhandled error
vars.Set(new Data.DynamicData("!error", () =>
{
    var stack = App.Debug.CallStack;
    if (stack == null)
        return null;  // callstack disabled and no error has triggered materialization
    for (var c = stack.Current; c != null; c = c.Caller)
    {
        if (c.Handled) continue;
        if (c.Errors.Count > 0) return c.Errors[^1];
    }
    return null;
}));
```

When CallStack is disabled, `%!error%` is null in steady state. On error, `App.Run`'s catch materializes a callstack on-demand for the duration of the error's bubble-up — `%!error%` resolves correctly during recovery handlers run on the materialized stack. The materialized stack is ephemeral (lives on the error itself, accessible via `error.CallFrames`).

For the case of `error.handle.Wrap` running recovery actions when no live callstack exists: Wrap upgrades `App.Debug.CallStack` to enabled for the duration of the recovery scope, so the synthetic recovery frame and its body's frames materialize properly. After Wrap exits, callstack returns to its prior state.

### Phase 6: Variables.Events surface

- New: `App/Variables/Events/this.cs` (OBP child of Variables).
- Wire `Variables.@this[set]` at line 87 (already calls `prev.FireOnChange(dv)`) to also fire `Events.OnSet?.Invoke(name, oldVal, newVal)`.
- New: when `Call.@this` is constructed via `CallStack.Push`, subscribe `Events.OnSet` with handler that appends to `call.Diffs`. Unsubscribe in `Call.DisposeAsync`.
- Diff capture: scalar-by-default, deep-clone via opt-in `deepDiff:true` config.

### Phase 7: Reconstruction helper

- New static method `App.CallStack.@this.Reconstruct(Action.@this errored) → IReadOnlyList<Call.@this>`.
- Walks `errored.Step.Goal.Parent` chain — verify the exact Goal-Parent navigation API by reading `App/Goals/Goal/this.cs`. The walk needs each level's "current action" — likely available via `Step.Index` and `Goal.Steps`. May require a small helper if Goal doesn't expose "what was the calling step from the parent goal".
- Reconstructed Calls have `Reconstructed=true`, `Diffs=[]`, `Caller` linked to next reconstructed level, `Cause=null`, `Duration=TimeSpan.Zero`, `StartedAt=DateTime.UtcNow`.

### Phase 8: ServiceError consumes new shape

`ServiceError` constructor at `App/this.cs:418` accepts the new `IReadOnlyList<Call.@this>`. `Errors/Error.cs` `CallFrames` property (line 24 area) types as `IReadOnlyList<Call.@this>`. PLang-side consumers via `%!error.CallFrames%` continue to work — DynamicData walks the typed list.

### Phase 9: Tests

PLang tests under `Tests/` — `--test` discovery from there:

1. **Depth tracking** — nested goal.call chain populates `%!callStack.Calls.Count` at each level.
2. **Auto-materialization** — error in deep-nested call with callstack disabled produces a `ServiceError.CallFrames` list matching the static call path.
3. **Live capture vs reconstructed** — same scenario with `--debug={callstack:true}` produces frames with `Reconstructed=false` and Duration > 0.
4. **CallStack.Errors accumulator** — three handled errors + one unhandled in one foreach iteration produces `CallStack.Errors.Count == 4`, with `Handled` flags reflecting the recovery outcomes.
5. **%!error% walk-back** — error in step 5, recovery body sets `%log_message% = %!error.Message%`, assert message matches the original error.
6. **Recovery frame visible** — during recovery, `%!callStack.Current.Action.Module%` resolves through to whatever sentinel the recovery frame uses; this validates Option B's structure.
7. **Variable diffs in live mode** — set `%name%` in step 1, modify in step 2; assert `%!callStack.Calls[1].Diffs[0].Before == "ingi" && .After == "ingi gauti"`.
8. **OOM safety** — `--debug={callstack:true}` over a 100-iteration loop touching a 1MB list does not exceed a memory threshold (assertable bound).
9. **Cancellation distinct** — cancel mid-foreach, error frame's `Errors[0]` is `OperationCanceledException`, renderer can distinguish.
10. **Reconstructed flag honored** — auto-materialized frames produce `Reconstructed=true`; live-captured frames produce `Reconstructed=false`.

C# tests under `PLang.Tests/` for the data model (one concern per file):

- `Variables/EventsTests.cs` — OnSet fires on rebind, on initial set, on remove.
- `CallStack/RecoveryFrameTests.cs` — Wrap pushes synthetic frame, pops on success, marks Handled.
- `CallStack/ReconstructTests.cs` — walks Goal.Parent correctly for 3-level deep nesting.
- `CallStack/CompressionTests.cs` — group split rules: identical siblings collapse; goal-change splits; errored frames split.

### Phase 10: Renderer (out of scope, sketched only)

The error message renderer (where `Error.cs:118` formats the chain today) consumes `Error.CallFrames` as `IReadOnlyList<Call.@this>` and applies the (X) compression rule. This branch ensures the data shape supports it; a follow-on branch implements the renderer fully (with intent-line, module-vs-goal hierarchy, errored-iteration counts, etc.).

Within this branch's scope: keep the existing renderer working with the renamed types so `plang p` runs don't visibly regress. New rendering features defer.

## Out of scope

- **Callback** — the durable-execution mechanism. Tracked at `Documentation/Runtime2/todos.md:136`. This branch is OBP-disciplined enough that Callback can build on top without reshaping Call.
- **Time-travel resume** — replaying from a frame's pre-state. Adjacent to Callback, requires snapshot semantics this branch doesn't provide.
- **Flamegraph projection** — the (Y) renderer. Same Call tree, different fold. Future view.
- **Causal-graph renderer** — for distributed flows once Callback is in place.
- **LLM-rejection rendering** — needs builder-side .pr.json enrichment, not callstack data.
- **Cycle detection at call time** — `CallStack.ContainsGoal` exists but isn't enforced in App.Run. Future branch.
- **Frame name on PLang side** — `%!callStack.Calls%` is what PLang devs see. C# rename is canonical.

## File map

Modify:
- `PLang/App/this.cs` — App.Run push/pop logic (Phase 3, 4).
- `PLang/App/CallStack/this.cs` — drop IsEnabled-special-cased Push, add PushRecovery, Reconstruct; rename Parent→Caller; add Cause.
- `PLang/App/CallStack/CallFrame.cs` → `PLang/App/CallStack/Call/this.cs` — rename per OBP.
- `PLang/App/CallStack/SerializableCallStack.cs` — typed against Call.@this.
- `PLang/App/Actor/Context/this.cs` — drop `Error` property, drop `vars.Set("!error", ...)` direct registration, replace with stack walk.
- `PLang/App/Actor/this.cs` — drop unconditional CallStack construction at line 87.
- `PLang/App/Debug/this.cs` — add `CallStack` property, lazy from `--debug` JSON.
- `PLang/App/Variables/this.cs` — wire OnSet/OnRemove/OnCreate dispatch.
- `PLang/App/modules/error/handle.cs` — push recovery frame, drop Context.Error set/restore, mark Handled on success.
- `PLang/App/Errors/Error.cs` and `IError.cs` — `CallFrames` typed as `IReadOnlyList<Call.@this>`.
- `PLang/App/GlobalUsings.cs` — alias for `Call = App.CallStack.Call.@this` if used widely.

Create:
- `PLang/App/Variables/Events/this.cs` — OBP child for OnSet/OnRemove/OnCreate.
- `PLang/App/CallStack/Diff.cs` — `Diff` record.
- `PLang/App/CallStack/Reconstruct.cs` — static helper (or method on CallStack.@this).
- `PLang/App/modules/debug/tag.cs` — `tag` action handler (small module action).

Delete:
- `App/CallStack/CallFrame.cs` (after rename).
- `Context.Error` property and `Context.Error` DynamicData registration.
- `CallStack.PushError` (today's IsEnabled=false back-door — replaced by Reconstruct).

## Risk register

- **`%!error%` regression in LlmFixer / error.handle paths** — the migration from `Context.Error` to stack-walk must be verified by the existing error.handle tests. If recovery scope (B) is wrong, `%!error.Message%` reads wrong values during recovery.
- **Variables.Events ordering** — if subscriptions happen in wrong order with respect to Push, diff capture misses values. Subscribe-before-Push, unsubscribe-after-Pop is the discipline.
- **Reconstruction walk** — if `Goal.Parent` chain doesn't carry "current step at parent" data, the static walk produces incomplete frames. Verify walk against `App/Goals/Goal/this.cs` API early.
- **Async push/pop balancing** — `App.Run` is async; if any path returns without hitting `finally`, frames leak. Audit all early returns. The existing code already has the discipline but the new push-conditional needs the same care.
- **Recovery frame action reference** — synthetic recovery frame needs a non-null Action ref for OBP cleanliness. Either reuse the recovery goal's first action, or introduce a sentinel `RecoveryAction` type. Decide during implementation.
- **Memory under deep loops with deepDiff:true** — the previous OOM scenario. Default scalar-only capture mitigates; deepDiff is opt-in. Document the trade-off.

## Suggested next bot

After this plan: **test-designer** to spec the test suites for Phase 9 (PLang `--test` and C# tests). Then **coder** to implement.
