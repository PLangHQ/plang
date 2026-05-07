# Coder v6 — Plan

## Context

Codeanalyzer v2 found B1+B2 (real bugs) plus L1/I1/I2. Discussion with Ingi
re-architected the model: the AsyncLocal scope belongs to the **fork
operator** (channel fire, parallel foreach, async task), not to the goal-call
boundary. Sequential `goal.call` is just a function call.

## Architecture

```
Variables (lives on Actor.Context — one per actor)
  └── Calls.@this (AsyncLocal<Call.@this?>)  ← overlay chain head
        └── Call.@this (mutable per-overlay dict, walks Caller for reads)

Get(name):  Calls.Current?.TryGet → Caller chain → actor dict
Set(name):  Calls.Current?.Set    → if no overlay, actor dict
```

A Call is no longer a read-only param frame. It's a real per-flow scope:

- Initial parameters seed it on `Push`.
- `set %x%` inside an active scope writes *into the current scope*.
- Reads cascade: current → caller → ... → actor dict.
- On dispose, the scope vanishes — its writes do not survive.

Fork operators push a scope; goal-call does not. So:

| Site | What it does |
|------|--------------|
| `App.RunGoalAsync(GoalCall)` | Set params into Variables (overlay-aware); no Push. |
| `GoalChannel.InvokeGoal` | Push scope, seed `%!data%` etc, run goal, dispose. |
| `loop.foreach` parallel | Per iteration: push scope, seed item/key, run sub-goal, dispose. |
| `loop.foreach` sequential | No fork, no scope. |
| Other concurrent boundaries | Same pattern. |

This dissolves L1 — `set %x% = 2` after `call Foo x=1` writes to the (no-overlay)
actor dict and reads return 2. Inside a forked flow, `set %x% = 2` writes to
the overlay and reads return 2 — still right, but isolated from siblings.

## Stages

### Stage A — Mutable Call.@this

`PLang/App/Variables/Calls/Call/this.cs`
- Replace `ImmutableDictionary<string, Data.@this>` with mutable
  `Dictionary<string, Data.@this>` (case-insensitive).
- Keep ctor seeded by parameters.
- Add `Set(string name, Data.@this value)` — writes into this overlay, fires
  no events at this layer (Variables fires).
- `TryGet` walks Caller chain unchanged.
- Remove the doc comment claiming "writes are not supported here".

### Stage B — Variables.Set respects overlay

`PLang/App/Variables/this.cs`
- Simple-name path (rootName == name): if `Calls.Current` exists, route the
  Set into the overlay (creates/updates a Data there). Underlying dict
  untouched. Events still fire.
- Dot/bracket paths: unchanged for now — they navigate an existing object,
  not bind a name. (Future work if needed: copy-on-write the parent into the
  overlay first.)
- `Variables.Get` already walks `Calls.Current?.TryGet` first; since `Set`
  now writes there, reads will find new writes naturally. **L1 dissolves.**
- `Contains` already checks overlay; OK.

### Stage C — B1: Channel null-ctx returns Ok

`PLang/App/Channels/Channel/this.cs:246-249`
- After `Debug.Write` diagnostic, `return Data.@this.Ok((object?)null);`
  instead of falling through. Drop the `ctx!` lie.

### Stage D — B2: Remove frame from RunGoalAsync(GoalCall); fix at fork sites

`PLang/App/this.cs:610-612`
- Keep the `foreach Set` (now overlay-aware via Stage B). No Push, no Pop.
  Sequential goal.call writes the params into whatever flow it's in — its
  caller's flow if no fork happened, or the fork's overlay if there was one.

`PLang/App/Channels/Channel/Goal/this.cs` (`GoalChannel.InvokeGoal`)
- Already pushes a Calls frame (v5). Keep it — channel fire is a fork.
  Verify the params (`%!data%` etc.) are seeded correctly into the now-mutable
  scope.

### Stage E — Parallel foreach pushes per-iteration overlay

`PLang/Runtime2/actions/loop/foreach.cs` (or wherever it lives)
- If parallelism is requested, each iteration awaits-using a fresh
  `context.Variables.Calls.Push(seedParams)`. `Task.WhenAll` joins.
- Sequential mode unchanged.
- TBD: confirm actual file location and current parallel support.

### Stage F — I1: real parallelism in CallsTests

`PLang.Tests/App/VariablesTests/CallsTests.cs:71-96`
- `Push_FrameInvisibleToParallelFlows`: replace ContinueWith chain with
  `Task.WhenAll`. Rename if appropriate.

### Stage G — I2: ChannelEvents type (OBP)

`PLang/App/Channels/Channel/Events/this.cs` (new)
- Encapsulates the bindings list, the recursion guard `_activeBindings`, and
  the iteration logic.
- `Channel.@this.Events` becomes a `ChannelEvents` instance, not a
  `public List<Binding>`.
- Mirrors `Goals.Goal.Events` shape.

### Stage H — Update CallsTests for new semantics

`PLang.Tests/App/VariablesTests/CallsTests.cs`
- The test asserting "set inside frame is visible to caller after dispose"
  (the LoadUser-leaky-pattern test) is now wrong: writes inside a frame stay
  in the frame and disappear on dispose. Replace with a test asserting that
  semantic.
- Add a new test: inside an active frame, `Set %x%` then `Get %x%` returns
  the new value (the L1 dissolution test).

### Stage I — Tests + summary + report

- `dotnet run --project PLang.Tests` clean.
- Clean rebuild + `cd Tests && plang --test` clean.
- Update summary.md and finalize report.json.
