# CallStack — Call Tree with AsyncLocal-Flowed Causality

## Why this exists

Today's `App/CallStack/this.cs` is partly wired. `App.Run` pushes a frame per action and pops in `finally` (`PLang/App/this.cs:391, 426`), but:

- `frame.Error` is never assigned when an action fails. `frame.Errors.Add` is never called. `CallStack.Errors` (the run-wide accumulator) is always empty.
- `%!error%` reads from a parallel `Context.Error` property set/restored by `error.handle.Wrap`. Two sources of truth.
- `IsEnabled=false` is incoherent: `Push` returns a transient un-pushed frame, but `App.Run`'s `finally` calls `PopAsync` unconditionally. Works only by coincidence (empty stack → no-op pop).
- The CallStack lives on `Actor.Context.CallStack` and is unconditionally constructed at `Actor.this.cs:87`. Wrong home — it's a debug/observability concern, not an actor concern.
- The frames are stored in a `ConcurrentStack`, which doesn't model parallel execution honestly. Once `GoalCall.Parallel` ships, two parallel branches sharing one stack each see the other's pushes.
- No causal "spawned by" link distinct from "called by" — needed for events, scheduled goals, callbacks.
- No render-agnostic data shape. Today's `GetStackTrace` is a single string formatter; nothing supports flamegraph or causal-graph projections.
- `Frame.Variables` exists but `SnapshotVariables` only computes `GetChangedSince(StartedAt)` once on pop. No event-driven diff capture.

This branch installs the data model for all of the above and wires the runtime to populate it correctly. Renderers and consumer surfaces beyond `%!error.CallFrames%` and the existing error message integration are explicitly out of scope (tracked separately).

## Settled design

### Tree, not stack

Frames form a **tree**, navigated via `Caller` up-pointers and `Children` down-pointers. The "current" position in the tree is held in an `AsyncLocal<Call.@this?>` so parallel execution forks naturally — no shared `ConcurrentStack`, no cloning of context, no race conditions. When `Task.WhenAll` starts two branches, each inherits the AsyncLocal value; each branch then pushes its own descendants without seeing the other's pushes.

The data structure is a single tree. Renderable as a stack from any leaf (walk Caller). Renderable as a flamegraph from the root (walk Children). Renderable as a timeline (sort by StartedAt). Same data, different fold.

### Ownership and lifecycle

- CallStack moves from `Actor.Context.CallStack` → `App.Debug.CallStack`.
- **Structural data is always on**. The thin push/pop of `(Action, Caller, Cause, Errors)` is ~50ns per action with no allocation pressure beyond the Call object itself. Errors get a useful trace from this alone — no reconstruction needed, no flag-flipping.
- Optional richer data is fine-grained per-flag, not tiered:
  ```jsonc
  --debug={
    callstack: true,                           // shorthand: timing+tags on, others off
    callstack: {
      timing:   true,    // StartedAt/CompletedAt/Stopwatch
      diff:     true,    // Variables.OnSet → Diffs
      deepDiff: false,   // full clone vs scalar-only summaries
      tags:     true,    // call.Tags dict
      history:  false,   // retain popped Calls in caller.Children
      maxFrames: 1000    // cap (history-on only)
    }
  }
  ```
  Each flag gates a specific property's population. Future properties get their own flag.

### Frame becomes Call

Rename `App.CallStack.CallFrame` → `App.CallStack.Call.@this` (OBP @this convention). The collection on a CallStack instance is `Children` from the root. Same shape, new home.

Reads natural in C#: `call.Action.Module`.
Reads natural in PLang: `%!callStack.Current.Action.Step.Text%`.

### Call shape (final)

```csharp
// App/CallStack/Call/this.cs
public sealed partial class @this : IAsyncDisposable
{
    public string Id { get; }                                  // unique frame id
    public Action.@this Action { get; init; }                  // OBP ref to static action def

    public Call.@this? Caller { get; init; }                   // sync parent in this run
    public Call.@this? Cause { get; init; }                    // async origin: event publish,
                                                               // recovery scope, sync-event capture.
                                                               // Same-process refs only — never null
                                                               // becomes a string variant. Cross-process
                                                               // identity goes through Items<T>.

    public List<IError> Errors { get; } = new();               // populated on action failure / retries
    public bool Handled { get; set; }                          // flipped true by error.handle.Wrap on
                                                               // recovery success

    public List<Call.@this>? Children { get; init; }           // live siblings; popped Calls also retained
                                                               // when history:true. Always allocated.

    // --- Timing tier (null when timing flag off)
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public TimeSpan? Duration => CompletedAt - StartedAt;

    // --- Diff tier (null when diff flag off)
    public List<Diff>? Diffs { get; init; }

    // --- Tag tier (null when tags flag off and never written)
    public Dictionary<string, string>? Tags { get; init; }

    // --- Typed extension surface (always available, lazy-allocated)
    public T? Get<T>() where T : class;
    public void Set<T>(T value) where T : class;

    // --- Snapshot the Caller chain from this Call upward.
    // Returns [this, Caller, Caller.Caller, ..., Root]. Used by App.Run to attach
    // a fixed chain to ServiceError on exception. Stable refs only — no copy.
    public IReadOnlyList<Call.@this> SnapshotChain();

    // --- IAsyncDisposable: pops self from CallStack on scope exit
    public ValueTask DisposeAsync();
}

// App/CallStack/Diff.cs
public sealed record Diff(string Name, object? Before, DateTimeOffset At);
```

Notes on shape:
- **`EventId` dropped** — replaced by `Cause` chain walk. `IsInEvent` becomes `Cause is EventTriggerCall || Caller?.IsInEvent ?? false` or similar.
- **`Indent` dropped** — derivable from `Caller` walk. Renderer counts.
- **`Phase` enum dropped** — `Handled` flag replaces `Phase = ExecutionPhase.Error`. Other Phase values weren't load-bearing.
- **`Diff.After` dropped** — `Variables[Name]` always gives current; for "what was X at time T", walk diffs of X for the smallest `At > T`, its `Before` is the answer.
- **`DateTimeOffset` everywhere** — project-wide preference, not just here.
- **`Items<T>`** — strongly-typed extension surface for handlers attaching their own metadata to a Call (cache info, http status, llm token counts, schedule identity, callback identity). Lazy-allocated dictionary keyed by `Type`.

`Caller` is the synchronous parent in the same execution chain (whatever AsyncLocal.Current was at Push time). `Cause` is the asynchronous origin — null for normal `goal.call` descents, set explicitly by event publishers, error.handle.Wrap recovery dispatch, and sync-event captures.

### CallStack shape

```csharp
// App/CallStack/this.cs
public sealed partial class @this
{
    private static readonly AsyncLocal<Call.@this?> _current = new();
    public Call.@this? Current => _current.Value;

    public CallStackFlags Flags { get; init; }                 // timing/diff/tags/history/etc.
    public List<IError> Audit { get; } = new();                // run-wide accumulator (was: Errors)
    public Call.@this? Root { get; }                           // first Call pushed in this run

    public int MaxDepth { get; init; } = 1000;                 // cycle/runaway protection

    // Push returns the Call itself (which IS IAsyncDisposable). Pop is automatic on scope exit.
    public Call.@this Push(Action.@this action, Call.@this? cause = null);

    public bool ContainsGoal(string goalName);                 // for cycle detection on Push
}

public record struct CallStackFlags(
    bool Timing, bool Diff, bool DeepDiff, bool Tags, bool History, int MaxFrames);
```

The `_frames : ConcurrentStack` is gone. Tree structure via Caller/Children pointers. AsyncLocal is the only shared mutable state, and AsyncLocal is fork-safe by construction.

### Cause population — four sites

`Cause` models in-process async causality. Each setting site is explicit:

1. **Recovery body** (`error.handle.Wrap`): Wrap holds the errored Call. Each action it invokes during recovery has `Cause = erroredCall`. Caller comes from AsyncLocal as usual (which, by the time recovery runs, is the goal-level call — the errored Call already popped).

2. **Sync event handler**: at publish time, the emit handler captures `AsyncLocal.Current` and uses it as `Cause` when invoking each subscribed handler's first Call. Because the publisher's `Current` is still in scope, `Caller == Cause` for sync events — harmless redundancy.

3. **Async event handler**: same capture pattern, but the captured ref is stored in the work-item before queuing. When the handler runs in a different async context, `AsyncLocal.Current` is unrelated; `Cause` (the captured ref) is the only meaningful link.

4. **Cross-process schedule / callback**: the originating Call is gone (different process, different run). `Cause` must be `null`. The cross-process identity lives in `Items<ScheduleInfo>` / `Items<CallbackInfo>` carried on the resumed Call. The renderer composes display: "no in-process Cause, but Items has ScheduleInfo → 'fired by schedule daily-report'".

This is why `Cause` stays `Call?` only. A string variant would be lying about what the field models.

### Recovery scope — Cause linkage, no synthetic frame

When `error.handle.Wrap` catches an error and runs the recovery body:

```
Stack at error time:                Stack during recovery body:
  G (goal-level)                      G (goal-level)
    R (file.read) ✗ 404                ↑ R popped
                                        W (output.write)  Caller=G, Cause=R
                                        R2 (file.read)    Caller=G, Cause=R
```

Recovery actions are siblings of R under G in the Caller tree. `Cause = R` is the link saying "this happened because R failed." The renderer can group consecutive siblings by Cause, or compose an "errored-then-recovered" view from `R` + the `Cause = R` siblings.

On recovery success: `R.Handled = true`. On recovery failure: `R.Handled` stays false; `R.Errors[0].ErrorChain.Add(recoveryError)` (the existing chaining mechanism). No synthetic recovery frame, no special-case Action sentinel.

Nested recovery (recovery body action also errors and has its own handle): works identically. The inner errored Call gets its own `Cause`-linked siblings; `Handled` flags compose. `app.Errors.Error` (below) handles the AsyncLocal nesting.

### `%!error%` via app.Errors

`Context.Error` and the `vars.Set("!error", () => Context.Error)` registration go away. Replaced by an OBP-shaped accessor on the Errors namespace:

```csharp
// App/Errors/this.cs   (new @this for the existing namespace)
public sealed partial class @this
{
    private static readonly AsyncLocal<IError?> _current = new();

    /// Singular accessor — current error in this async context.
    public IError? Error => _current.Value;

    /// Run-wide audit of every error observed.
    public List<IError> All { get; } = new();

    public IDisposable Push(IError error)
    {
        var prev = _current.Value;
        _current.Value = error;
        return new Restore(() => _current.Value = prev);
    }
}
```

Reads:
- C#: `app.Errors.Error` → `IError?` (current error in this async flow)
- C#: `app.Errors.All` → audit list
- C#: `using (app.Errors.Push(error)) { /* recovery body */ }` — scope mgmt
- PLang: `%!error%` resolves through `app.Errors.Error` (binding kept on `Actor.Context` as a DynamicData passthrough)

`error.handle.Wrap` wraps recovery in `using (app.Errors.Push(caught)) { await RunRecovery(...); }`. AsyncLocal flows naturally into recovery body actions and nested Wraps. Single source of truth, parallelism-safe, no race.

(`App.Errors.@this` is a small new file. Future refactor — moving `Error.cs`/`ServiceError.cs`/`ActionError.cs` under an `App/Errors/Error/` folder for full OBP layout — is out of scope here.)

### Disposable lifecycle — out of scope

Today's `CallFrame.AddDisposable` / `TransferDisposable` / `_disposables` infrastructure has **zero callers** outside `CallFrame.cs` itself (verified by grep across `PLang/`). It's dead code currently.

Rather than design a new model speculatively, this branch **deletes the existing dead code** and leaves disposal lifecycle for a future branch driven by a real use case. `Call.@this.DisposeAsync` does only two things: AsyncLocal restore + (if diff flag on) Variables.OnSet unsubscription.

When a real consumer appears (e.g., a future `file.open` returning a stream that flows past goal boundaries), that branch designs and implements the disposal model with concrete requirements in hand.

### Push API — IAsyncDisposable

Per-action push uses `await using` for automatic Pop:

```csharp
await using var call = stack.Push(action, cause: null);
// save context anchors (Goal/Step)
try
{
    var result = await handler.ExecuteAsync(action, context);
    if (!result.Success)
    {
        call.Errors.Add(result.Error!);
        stack.Audit.Add(result.Error!);
    }
    return result;
}
catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
{
    var serviceErr = new ServiceError(ex.Message, step, call.SnapshotChain(), "ServiceError", 400) { Exception = ex };
    serviceErr.Params = handler.SnapshotParams();
    call.Errors.Add(serviceErr);
    stack.Audit.Add(serviceErr);
    return Data.@this.FromError(serviceErr);
}
finally { /* restore Goal/Step anchors only */ }
```

`Call.DisposeAsync` handles AsyncLocal restore, removes self from `Caller.Children` if `history:false`, finalizes Diffs (unsubscribes Variables.OnSet), and — for goal-boundary Calls — disposes attached resources.

### Children — always live, history flag retains popped

`caller.Children` is mutated on every Push (append) and on every Pop (remove if `history:false`, keep if `history:true`). Cost is negligible — one list-add per Push, conditionally one list-remove per Pop. Unlocks live-tree rendering for free.

With `history:true` the same list grows monotonically; cap at `maxFrames` with FIFO eviction.

### Cycle detection

`MaxDepth` (default 1000) and `ContainsGoal` checks fire on every Push:

- `Caller`-chain depth > `MaxDepth` → throw `CallStackOverflowException(MaxDepth)` with the chain path.
- The new goal name appears in the Caller chain → throw `CallStackOverflowException` with cycle path.

Both throw cleanly with paths that the renderer can show. Today's `ContainsGoal` exists but isn't enforced — this branch enforces.

### Variable diffs via Variables.Events

`App.Variables.@this` grows an event surface (mirrors `App.Events.@this`):

```csharp
// App/Variables/Events/this.cs
public sealed partial class @this
{
    public event Action<string, object?, object?> OnSet;       // (name, before, after)
    public event Action<string> OnRemove;
    public event Action<string, object?> OnCreate;
}
```

When `diff:true`, every Push subscribes a handler that appends `Diff(name, before, At)` to `call.Diffs`. The handler unsubscribes in `Call.DisposeAsync`. Subscribe-before-Push, unsubscribe-after-Pop is the discipline.

Default capture: scalar-only — `int/bool/decimal/DateTimeOffset/string ≤ 256 chars`. Non-scalar values render as `"<List<Order> @ 5042 items>"` with type and size only. Opt-in via `deepDiff:true` for full clone capture. This keeps `diff:true` cheap enough for the loops that previously OOM'd.

### Tag surface

`call.Tags : Dict<string,string>` is written by two surfaces:

1. **C# handler** writes via `context.CallStack.Current?.Tag(key, value)`. No-op if current is null. Use cases: `cache.hit=true`, `http.status=503`, `llm.tokens=2400`.
2. **PLang developer** writes via a new `tag` action.

The `tag` PLang action shape:
- Module: `PLang/App/modules/debug/tag.cs`
- PLang syntax: `- tag critical=true, owner=checkout` or `- tag "manual-checkpoint"`
- Inputs (one of):
  - `Data<Dictionary<string,string>> Pairs` — for key=value form (LLM extracts a Dict from the natural-language tag list)
  - `Data<string> Label` — for the bare-string form (writes `Tags[label] = "true"`)
- Behavior: resolves `context.CallStack.Current`; if non-null, merges `Pairs` into `Current.Tags ??= new()` (or sets the bare label). No-op if Current is null. Returns `Data.Ok()`.
- Cacheable = false. No `[Provider]` properties. Trivial handler — fits the existing module pattern.

### Cancellation lane

No data-model change. Frames know they cancelled because `frame.Errors` carries an `OperationCanceledException`-derived `IError`. Renderer shows `× cancelled` instead of `✗ failed`. Mentioned for completeness so the renderer spec covers it.

### ErrorChain integration

`Errors/Error.cs:35` already has `List<IError> ErrorChain`. `error/handle.cs:97,110` already appends recovery failures. The render path consumes this when present, displays as "caused by:" cascade. No code changes needed beyond the renderer.

### Error snapshot semantics

When a Call fails, the existing `error.CallFrames` mechanism captures the chain at fail time. With this branch:

- Snapshot is just a list of `Call.@this` refs — no copy.
- The refs keep the chain reachable via GC even after the live AsyncLocal has rewound past them.
- `Cause` refs additionally pin their targets — a recovery body Call referencing the errored Call as its Cause keeps the errored Call (and that Call's whole Caller chain) alive for the duration of the consumer.
- This is what we want: the renderer can walk the full causal tree from any error envelope long after the live execution has moved on.

## Migration plan

This is a behavior-preserving refactor in spirit but touches enough places that it can't be cosmetic-only.

### Phase 1: Move CallStack to App.Debug; drop ConcurrentStack

- New property: `App.Debug.@this.CallStack : App.CallStack.@this`. Always allocated (cheap), but `Flags` controls what each Call carries.
- **Construction site**: `App.Debug.@this` constructor allocates the CallStack, parsing `--debug={callstack:{...}}` JSON from the Debug startup config into a `CallStackFlags` value. The shorthand form `--debug={callstack:true}` parses to `Flags { Timing = true, Tags = true, Diff = false, DeepDiff = false, History = false, MaxFrames = 1000 }`. Without any callstack flag, `Flags` defaults to all-false (structural only).
- Remove unconditional construction at `Actor.this.cs:87`.
- `Actor.Context.@this.CallStack` getter reads through to `App.Debug.CallStack` so PLang `%!callStack%` still resolves through Context.
- DynamicData registration at `Actor/Context/this.cs:176` becomes `() => App.Debug.CallStack`.
- Replace `ConcurrentStack<CallFrame> _frames` with `AsyncLocal<Call.@this?> _current` plus tree pointers. Drop `_frames` entirely.

### Phase 2: Rename CallFrame → Call.@this; add Errors @this

- `App/CallStack/CallFrame.cs` → `App/CallStack/Call/this.cs` (OBP @this convention).
- `App/CallStack/SerializableCallFrame.cs` → typed against `Call.@this`.
- All `CallFrame` references → `Call.@this`. Sites: `App/this.cs:387,388,391,424,426`, `App/CallStack/this.cs`, `App/Errors/Error.cs:24`, `App/Errors/Exceptions.cs`, `App/Debug/this.cs:148`.
- Property `Parent` → `Caller`. New nullable `Cause : Call.@this?`. Drop `EventId`, `Indent`, `Phase` enum, `Variables` snapshot dict, `Error` (single).
- New `App/Errors/this.cs` (`@this` for the existing namespace) carrying `Error : IError?`, `All : List<IError>`, `Push(error) : IDisposable`.

### Phase 3: Wire frame errors and AsyncLocal Current

Verified against `App/this.cs:380-431`. The `await using` reshape works with this ordering:

```csharp
var stack = App.Debug.CallStack;
await using var call = stack.Push(action);     // sets AsyncLocal Current, appends to caller.Children

// Anchor save (after Push so handler sees both AsyncLocal and anchor state)
var previousStep = context.Step;
var previousGoal = context.Goal;
var previousEvent = context.Event;
context.Step = action.Step;
if (context.Step != null) context.Step.Context = context;
context.Goal = action.Step?.Goal;

try
{
    var result = await handler!.ExecuteAsync(action, context);
    if (!result.Success && result.Error is Errors.Error err)
    {
        if (err.Params == null) err.Params = handler.SnapshotParams();
        call.Errors.Add(result.Error!);
        stack.Audit.Add(result.Error!);
    }
    return result;
}
catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
{
    var serviceErr = new Errors.ServiceError(ex.Message, action.Step, call.SnapshotChain(), "ServiceError", 400) { Exception = ex };
    serviceErr.Params = handler!.SnapshotParams();
    call.Errors.Add(serviceErr);
    stack.Audit.Add(serviceErr);
    return Data.@this.FromError(serviceErr);
}
finally
{
    // Anchor restore — runs before `using` dispose
    context.Step = previousStep;
    context.Goal = previousGoal;
    context.Event = previousEvent;
}
// `call.DisposeAsync` runs here on `using` exit: AsyncLocal restore, Children cleanup
// (if history:false), Variables.OnSet unsubscribe (if diff:true).
```

Order of operations: `finally` (anchor restore) runs first, then `await using` disposes the Call (Pop). Anchor restore can rely on context still pointing at "this action's" state; nothing in Call.DisposeAsync depends on context anchors. Verified clean.

**Behavior tweak — chain composition:** today's `var callFrames = context.CallStack?.GetFrames()` runs *before* Push, so the `ServiceError`'s chain does NOT include the failing action. The new `call.SnapshotChain()` runs *after* Push and DOES include `call` at index `[0]`. Renderers that compose "failing step + caller chain" must change to "chain[0] is the failing call." This is a small render-side adjustment; the data is strictly more useful (caller chain is unambiguous about what failed).

**Drop the `frame.SnapshotVariables(context.Variables)` call in finally.** Diff capture is event-driven during handler exec (Phase 6), not a one-shot at frame end.

Delete the `IsEnabled=false` branch in `CallStack.Push`. Delete `PushError` entirely.

### Phase 4: Recovery via Cause (no synthetic frame, no flag-flipping)

In `error/handle.cs` (`Wrap` method):

```csharp
var caught = result.Error!;
using (app.Errors.Push(caught))
{
    var recoveryResult = await RunRecovery(actions!, context, cause: erroredCall);
    if (recoveryResult.Success)
    {
        erroredCall.Handled = true;
        return recoveryResult;
    }
    caught.ErrorChain.Add(recoveryResult.Error!);
}
return result;
```

`RunRecovery` is updated to thread `cause` through to `App.Run` so each recovery body action's Push gets `cause: erroredCall`. Caller comes from AsyncLocal as usual.

Drop `RunRecoveryWithErrorScope`. Drop `context.Error` set/restore (lines 132-141). Drop the `IError? Error` property on `Actor.Context.@this`. Drop the `Context.Error` DynamicData registration.

### Phase 5: %!error% via app.Errors

Replace `Actor.Context.Variables` registration of `!error`:

```csharp
vars.Set(new Data.DynamicData("!error", () => app.Errors.Error));
```

That's it. AsyncLocal handles scope, nesting, and parallelism. No stack walk, no flag check, no race.

### Phase 6: Variables collection-level events

`Variables.@this` already has per-variable events (`OnCreate`/`OnChange`/`OnDelete` on individual `Data.@this`) used by `--debug={"variables":[...]}`. Those stay untouched.

Add three **collection-level** events directly on `Variables.@this` so a Call's diff capture can subscribe once for the whole namespace:

```csharp
// PLang/App/Variables/this.cs
public event Action<string, object?, object?>? OnSet;       // (name, before, after)
public event Action<string>? OnRemove;
public event Action<string, object?>? OnCreate;
```

Wire at the existing fire sites in `Variables.@this.Set` and `.Remove`:
- After `prev.FireOnChange(dv)` (line 87): also `OnSet?.Invoke(name, prevValue, dv.Value)`.
- After `dv.FireOnCreate()` (line 91): also `OnCreate?.Invoke(name, dv.Value)`.
- After `data.FireOnCreate()` (line 109): also `OnCreate?.Invoke(name, data.Value)`.
- After `removed.FireOnDelete()` (line 365): also `OnRemove?.Invoke(name)`.

When `diff:true`, `Call.@this` ctor subscribes `OnSet` with a handler that appends `Diff(name, before, At)` to `call.Diffs`. `Call.DisposeAsync` unsubscribes.

No new file (`App/Variables/Events/this.cs` is *not* created — events live on the existing `Variables.@this`). Scalar-only capture by default; `deepDiff:true` opts into clone capture.

### Phase 7: Children + cycle detection

- `caller.Children` always allocated (small list). Push appends. Pop removes if `history:false`.
- `MaxDepth` enforcement on Push: throw `CallStackOverflowException` with chain path.
- `ContainsGoal` enforcement on Push: throw `CallStackOverflowException` with cycle path.

### Phase 8: ServiceError consumes new shape

`ServiceError` constructor at `App/this.cs:418` accepts `IReadOnlyList<Call.@this>`. `Errors/Error.cs` `CallFrames` typed as `IReadOnlyList<Call.@this>`. PLang-side `%!error.CallFrames%` continues to work — DynamicData walks the typed list.

### Phase 9: Delete dead disposal code

`CallFrame.AddDisposable`, `TransferDisposable`, `_disposables`, the disposal pass in `DisposeAsync` (lines 178-186 of today's CallFrame.cs) — none of these have callers outside CallFrame.cs itself (verified). Delete the methods, fields, and the disposal loop in DisposeAsync. `Call.@this.DisposeAsync` reduces to AsyncLocal restore + (if diff:true) Variables.OnSet unsubscribe + (if history:false) Caller.Children removal.

A future branch driven by a real disposable use case can re-introduce the lifecycle with concrete requirements.

### Phase 10: Tests

PLang tests under `Tests/`:

1. **Depth tracking** — nested goal.call chain populates `%!callStack.Current%` walks correctly.
2. **Caller chain across cross-file goals** — error in deep-nested call across .goal files produces `error.CallFrames` reflecting full dynamic chain (the case `Goal.Parent` static walk gets wrong).
3. **Audit accumulator** — three handled errors + one unhandled in one foreach iteration produces `Audit.Count == 4`, with `Handled` flags reflecting recovery outcomes.
4. **%!error% nesting** — outer recovery body has inner `error.handle`; inner's `%!error%` reads inner caught; after inner closes, outer's `%!error%` still reads outer caught.
5. **%!error% null outside scope** — outside any Wrap, `%!error%` is null.
6. **Cause = errored on recovery body** — `%!callStack.Current.Cause.Action.Module%` inside recovery body resolves to the errored action's module.
7. **Variable diffs in diff mode** — set `%name%` in step 1, modify in step 2; assert `%!callStack.Current.Caller.Children[1].Diffs[0].Before == "ingi"`.
8. **OOM safety** — `--debug={callstack:{diff:true}}` over a 100-iteration loop touching a 1MB list does not exceed a memory threshold.
9. **Cancellation distinct** — cancel mid-foreach, error frame's `Errors[0]` is `OperationCanceledException`-shaped IError.
10. **Cycle detection** — goal A calls goal A directly throws `CallStackOverflowException`.

C# tests under `PLang.Tests/` (one concern per file):

- `Variables/CollectionEventsTests.cs` — `Variables.@this.OnSet` fires on rebind, `OnCreate` on initial set, `OnRemove` on delete; per-variable events still fire too (back-compat).
- `CallStack/CauseLinkageTests.cs` — recovery body Calls have `Cause = erroredCall`.
- `CallStack/AsyncLocalForkTests.cs` — `Task.WhenAll` of two branches; each branch's Push doesn't pollute the other.
- `CallStack/CycleDetectionTests.cs` — direct and indirect cycles caught.
- `CallStack/ItemsExtensionTests.cs` — `Get<T>` / `Set<T>` typed bag.
- `Errors/PushScopeTests.cs` — `app.Errors.Push` LIFO; nested scopes restore correctly.

### Phase 11: Renderer (out of scope, sketched only)

The error message renderer (where `Error.cs:118` formats the chain today) consumes `Error.CallFrames` as `IReadOnlyList<Call.@this>`. The compression rule — collapse consecutive sibling Calls sharing `(Caller, Action.Module, Action.Step.Index)`, break at differing `(Module, Step.Index)` or non-empty `Errors` — applies in the renderer, not the data layer. Cause-grouping (recovery siblings) is also a renderer concern.

Within this branch's scope: keep the existing renderer working with the renamed types so `plang p` runs don't visibly regress. New rendering features defer.

## Out of scope

- **Callback** — durable execution. Tracked at `Documentation/Runtime2/todos.md:136`. This branch is OBP-disciplined enough that Callback can build on top: callback-resumed Calls have `Cause = null` and `Items<CallbackInfo>`.
- **Time-travel resume** — replaying from a frame's pre-state. Adjacent to Callback.
- **Flamegraph projection** — same Call tree, different fold. Future view.
- **Causal-graph renderer** — for distributed flows once Callback is in place.
- **LLM-rejection rendering** — needs builder-side .pr.json enrichment.
- **Frame name on PLang side** — `%!callStack.Current%` is what PLang devs see. C# rename is canonical.
- **Errors folder OBP refactor** — moving `Error.cs`/`ServiceError.cs`/`ActionError.cs` under `App/Errors/Error/`. This branch only adds `App/Errors/this.cs`.

## File map

Modify:
- `PLang/App/this.cs` — App.Run push/pop logic (Phase 3); drop SnapshotVariables-in-finally; chain captured post-push.
- `PLang/App/CallStack/this.cs` — drop ConcurrentStack; AsyncLocal+tree; cycle detection; flag-driven population.
- `PLang/App/CallStack/CallFrame.cs` → `PLang/App/CallStack/Call/this.cs` — rename per OBP. IAsyncDisposable. Items<T>. SnapshotChain. New Cause field.
- `PLang/App/CallStack/SerializableCallStack.cs` — typed against `Call.@this`.
- `PLang/App/Actor/Context/this.cs` — drop `Error` property, drop `vars.Set("!error", ...)` direct registration, replace with `() => app.Errors.Error`.
- `PLang/App/Actor/this.cs` — drop unconditional CallStack construction at line 87.
- `PLang/App/Debug/this.cs` — add `CallStack` property; parse `--debug={callstack:{...}}` JSON into `CallStackFlags`. Construction site for the CallStack instance.
- `PLang/App/Variables/this.cs` — add three collection-level events (`OnSet`/`OnRemove`/`OnCreate`); fire them at existing fire sites (lines 87, 91, 109, 365). Per-variable events untouched.
- `PLang/App/modules/error/handle.cs` — recovery via Cause; drop Context.Error set/restore; mark Handled on success.
- `PLang/App/Errors/Error.cs` and `IError.cs` — `CallFrames` typed as `IReadOnlyList<Call.@this>`.
- `PLang/App/GlobalUsings.cs` — alias for `Call = App.CallStack.Call.@this` if used widely.

Create:
- `PLang/App/Errors/this.cs` — `@this` for the namespace: `Error : IError?`, `All : List<IError>`, `Push(error) : IDisposable`.
- `PLang/App/CallStack/Diff.cs` — `Diff` record (`Name`, `Before`, `At`).
- `PLang/App/modules/debug/tag.cs` — `tag` action handler.

Delete:
- `App/CallStack/CallFrame.cs` (after rename).
- `Context.Error` property and DynamicData registration.
- `CallStack.PushError`, `CallStack.IsEnabled`, `_frames : ConcurrentStack`.
- `CallFrame.EventId`, `Indent`, `Phase` enum, `Error` (single), `Variables` (snapshot dict).
- `CallFrame.AddDisposable`, `TransferDisposable`, `_disposables` field, disposal loop in `DisposeAsync` — verified zero callers outside CallFrame.cs itself.
- `error.handle.RunRecoveryWithErrorScope`.

## Risk register

- **`%!error%` regression in LlmFixer / error.handle paths** — the migration from `Context.Error` to `app.Errors.Error` is semantically identical when AsyncLocal flows correctly. Existing error.handle tests exercise the recovery scope; they must pass post-migration.
- **Variables.OnSet ordering** — subscribe in `Call` ctor (during Push), unsubscribe in `DisposeAsync` (during Pop). If Push returns before subscribe completes, initial sets in the action body are missed; if Dispose returns before unsubscribe runs, subscription leaks. Both are simple to get right but worth a focused test.
- **ServiceError chain composition tweak** — today's chain doesn't include the failing action; the new chain does (`SnapshotChain` includes `this` at index `[0]`). Renderers that compose "step + chain" must change to "chain[0] is the failing call." Audit the existing renderer in `Errors/Error.cs:118` area.
- **Items<T> retention** — typed metadata can hold arbitrarily large objects. Document that Items lives until the Call is GC'd (which may be long after Pop if any error envelope or descendant references it). Handler authors should put summary data in Items, not raw payloads.
- **Cause retention** — a recovery body Call referencing `Cause = erroredCall` keeps the errored Call alive for the descendant's lifetime. Acceptable, but means errors handled-and-recovered still consume memory until the recovery body's tree GCs.
- **Memory under deep loops with `deepDiff:true`** — the previous OOM scenario. Default scalar-only capture mitigates; `deepDiff` is opt-in. Document the trade-off.
- **Cycle detection false positives** — recursive goal patterns (a goal that legitimately calls itself N times) trigger `ContainsGoal`. `MaxDepth` is the right escape valve; document that recursive patterns must respect it.
- **AsyncLocal cost in deep async hot paths** — quoted ~50ns per push is right in isolation but each `_current.Value =` notifies registered ExecutionContext callbacks. In hot loops with many awaits, real cost can be higher. If profiling shows it matters, the AsyncLocal can be replaced with a `[ThreadStatic]` for non-parallel runs and AsyncLocal only for parallel goals.

## Suggested next bot

After this plan: **test-designer** to spec the test suites for Phase 10 (PLang `--test` and C# tests). Then **coder** to implement.
