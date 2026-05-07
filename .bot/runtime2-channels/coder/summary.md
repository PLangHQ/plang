# coder — runtime2-channels

## Version
v5 — codeanalyzer v1 fixes (response to FAIL verdict on the v3+v4 channel work).

## What this is

Codeanalyzer v1 reviewed v3+v4 (channel architecture cleanup, event
firing fix) and returned a FAIL verdict with six findings: two real
bugs, one latent crash, one dead type, one silent encoding bug, one
design gap. v5 closes all six.

The non-trivial one is F2 (concurrent goal-channel writes racing on
shared `Variables`). The fix grew into a new primitive (`Variables.Calls`,
an AsyncLocal parameter scope) and a small architectural rule:
**concurrency boundaries — not regular goal calls — push the scope.**
Sequential goal calls keep today's leaky semantics (e.g. `LoadUser` setting
`%user%` for the caller to read). Only fork sites isolate.

## What was done — fix-by-fix

**F1 — `Services` atomic Remove.** Replaced
`ConcurrentBag<Service>` (no atomic Remove; the old drain-and-rebuild
silently dropped services racing concurrent `New()`) with
`ConcurrentDictionary<Guid, Service>`. `Service.Id` is the stable key.
- Files: `PLang/App/Services/this.cs`,
  `PLang/App/Services/Service/this.cs`.
- Test: `Stage7_AppServicesTests.Services_ConcurrentNewAndRemove_NoServiceDropped`
  — 200 concurrent New + half-dispose; asserts no survivor lost.

**F2 — `Variables.Calls` AsyncLocal scope, pushed at `GoalChannel.WriteAsync`.**
The deepest fix. New types under `PLang/App/Variables/Calls/`:
- `Calls.@this` — collection holding `AsyncLocal<Call?>`. Mirrors
  `CallStack.@this` shape (Push returns `IAsyncDisposable`,
  `RestoreCurrent` no-ops if `Current` already moved on).
- `Call.@this` — one frame's parameter bindings as
  `ImmutableDictionary<string, Data>`; walks `Caller` chain on lookup
  so inner shadows outer.

`Variables.Get` and `Contains` consult `Calls.Current?.TryGet(name)`
*before* the underlying dict — frame wins on reads. `Variables.Set`
unchanged: writes always go to underlying. So inside a frame,
`%!data%` reads from the frame and `set %lastMessage%` writes to the
actor as before.

`GoalChannel.InvokeGoal` pushes a frame around `RunGoalAsync` carrying
`!data`. `RunGoalAsync(GoalCall)` itself does **not** push (sequential
calls stay leaky on purpose).

Wider goal-body concurrency (e.g. `set %x%` racing across parallel
branches) is logged in `Documentation/Runtime2/todos.md` as a
follow-up; the design likely splits into `Variables.Calls`
(parameter-only) and `Variables.Branches` (full read+write isolation)
when parallel foreach lands.

- Files: `PLang/App/Variables/Calls/{this.cs,Call/this.cs}`,
  `PLang/App/Variables/this.cs` (Get/Contains/Calls property),
  `PLang/App/Channels/Channel/Goal/this.cs` (use Push instead of
  Variables.Set), `PLang/App/this.cs` (revert RunGoalAsync param
  injection unchanged from baseline).
- Tests: 10 new in `PLang.Tests/App/VariablesTests/CallsTests.cs`
  covering nesting, dispose-order, Set-write-through,
  AsyncLocal isolation across `Task.Run`, and the 200-task concurrent
  push race.

**F3 — `InvokeChannelHandler` null-Actor handling.** Removed the
`ctx!` lie on `Actor?.Context!`. New behavior: pass `Actor?.Context`
(possibly null) and emit a `Debug.Write` diagnostic. Handlers that need
ctx must guard themselves; tests that construct channels in isolation
without an Actor still work (their handlers ignore ctx).
- File: `PLang/App/Channels/Channel/this.cs:234`.

**F4 — delete dead `App.Channels.Channel.EventContext`.** Type was
declared, never constructed by the firing path. Stage 8 had one
shape-test of it in isolation — deleted that test, fixed the comment
and one test name that referenced "ViaEventContext".
- Files: removed `PLang/App/Channels/Channel/EventContext.cs`;
  edits in `PLang.Tests/App/ChannelsTests/Stage8_ChannelEventsTests.cs`.

**F5 — Stream `Encoding` honored.** `ReadAllTextAsync` /
`WriteTextAsync` previously hardcoded UTF-8, ignoring the channel's
`Encoding` property. Added `ResolveEncoding()` —
`Encoding.GetEncoding(Encoding)` with UTF-8 fallback for null/empty
or unknown names.
- Files: `PLang/App/Channels/Channel/Stream/this.cs:157-180`.
- Tests: 3 new in `Stage2_StreamChannelTests` — latin-1 round-trip
  read/write, unknown-encoding fallback.

**F6 — goal channels default to Bidirectional + accept explicit
`Direction`.** `channel.set` previously stamped every goal channel as
`Output` unless the name was literally `"input"`. But `GoalChannel`
extends `Session` (Ask-capable), so a channel named `"chat"` could
answer Ask while reporting `CanRead = false` — inconsistent. Added
optional `Direction` parameter (`"input"` | `"output"` |
`"bidirectional"`); precedence: explicit > name shortcut
(input/output) > Bidirectional default.
- File: `PLang/App/modules/channel/set.cs`.

## Code example

The shape of F2 — push at the fork site, not the call site:

```csharp
// PLang/App/Channels/Channel/Goal/this.cs
private async Task<Data.@this> InvokeGoal(Data.@this data, CancellationToken ct)
{
    if (!IsOpen) return Data.@this.FromError(...);

    var ctx = Actor.Context;
    using var __chans = Actor.PushChannelsOverride(Actor.FoundationalChannels);

    // Concurrency boundary: each WriteAsync gets its own frame.
    var paramData = new Data.@this("!data", data.Value, data.Type);
    await using var __frame = ctx.Variables.Calls.Push(new[] { paramData });

    return await app.RunGoalAsync(Goal, ctx, ct);
}
```

```csharp
// PLang/App/Variables/this.cs — Get consults frame first, falls back to actor dict
if (Calls.Current is { } frame && frame.TryGet(rootName, out var framed))
    root = framed;
else if (!_variables.TryGetValue(rootName, out root))
    return Data.@this.NotFound(name);
```

## v4 → v5 — what reviewer flagged and how it was addressed

| Finding | Fix |
|---|---|
| F1 ConcurrentBag race | ConcurrentDictionary + Service.Id |
| F2 GoalChannel %!data% race | Variables.Calls AsyncLocal scope, pushed at WriteAsync |
| F3 NRE on null Actor | Drop the `!` lie, diagnostic + null-tolerant handler contract |
| F4 EventContext dead | Deleted |
| F5 Stream encoding ignored | ResolveEncoding() honors property + UTF-8 fallback |
| F6 Bidirectional unreachable | Default goal channels to Bidirectional + accept Direction |

**Design clarification surfaced during review:** Ingi reframed F2 — the
boundary belongs at concurrency creators (goal channel writes,
parallel foreach, fire-and-forget), not at every goal call. Sequential
parameter leak is intentional (`LoadUser` pattern). v5 implements that
narrower scoping; the wider parallel-branch isolation is logged as
todo for the parallel-foreach work.

## Test status

- **C#**: 2757 pass / 0 fail (was 2744 — 13 new tests across F1, F2, F5).
- **PLang**: 201 / 201 pass / 0 fail / 0 stale.

## What's next

Suggest **codeanalyzer** runs again on this branch to verify the v1
findings are closed. No new modules or actions added (just a new
optional `Direction` parameter on `channel.set`); builder rebuild
not strictly required, but a builder pass would be worth doing if
the new `Direction` shortcut should make it into the LLM-visible
catalog descriptions.
