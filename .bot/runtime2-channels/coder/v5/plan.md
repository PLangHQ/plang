# v5 plan — codeanalyzer v1 fixes

Six findings, six fixes. Roughly in dependency order: F2 (Variables.Calls scope) is the deepest change and lands first because it requires a new type. F1 is independent. F3/F5 are surface-level. F4 is a deletion. F6 is one parameter and a default change.

## F2 — `Variables.Calls` scope + `GoalCall.Parameters` plumbing

**Symptom (today).** `GoalChannel.InvokeGoal` writes the inbound envelope by mutating actor-shared Variables: `ctx.Variables.Set("!data", data)`. Two concurrent writes on the same goal channel both target the same slot, last writer wins, the goal body reads whatever happened most recently — not its own input.

**Real fix.** Per-call AsyncLocal parameter scope on `Variables`. Reads consult the call's parameter frame first, fall back to the underlying dict. Writes (`set %x% = ...` from inside the goal body) continue to hit the underlying dict — keeps the existing "goal mutates actor state" semantic intact. Each concurrent call carries its own AsyncLocal slot, so frames are invisible across flows.

**Shape.**

- New type `App.Variables.Calls.@this` (sub-collection on `Variables`):
  - `private readonly AsyncLocal<Call.@this?> _current = new();`
  - `Current` accessor (innermost call, null when no Push happened on this flow).
  - `Push(IEnumerable<Data.@this>? parameters)` returns a `Call.@this` that is `IAsyncDisposable` (matches `CallStack.Call.@this`).
- New type `App.Variables.Calls.Call.@this`:
  - Holds an `ImmutableDictionary<string, Data.@this>` keyed by parameter name.
  - `TryGet(string name, out Data)` — case-insensitive lookup matching `Variables` semantics.
  - `Caller` link (previous `Current`) to chain frames; `DisposeAsync` calls `Calls.RestoreCurrent(this, Caller)` (no-op if `Current` already moved on — same trick `CallStack` uses).
- `Variables.@this` exposes `public Calls.@this Calls { get; } = new();`. Read paths (`Get`, `GetValue`, `Resolve`) consult `Calls.Current?.TryGet(name)` first, then fall back to the existing `_variables` dict. Write paths unchanged.
- `App.RunGoalAsync(GoalCall goalCall, ...)` replaces the parameter-injection loop:

  ```csharp
  // before:
  if (goalCall.Parameters != null)
      foreach (var param in goalCall.Parameters)
          context.Variables.Set(param.Name, param);

  // after:
  await using var _ = context.Variables.Calls.Push(goalCall.Parameters);
  ```

- `GoalChannel.InvokeGoal` no longer touches `Variables` directly — wraps the inbound `data` as a `Data` named `!data` and routes it through `GoalCall.Parameters`:

  ```csharp
  data.Name = "!data";
  var call = new GoalCall { Name = Goal.Name ?? "", PrPath = Goal.PrPath, Parameters = new() { data } };
  return await app.RunGoalAsync(call, ctx, ct);
  ```

  (Have to check that mutating `data.Name` is safe — if not, `data.WithName("!data")` or `Data.@this.Ok(data.Value, ...) { Name = "!data" }`. Read first.)

**Tests (C#).**

- `Variables.Calls`: Push/Pop nesting, Get-from-frame-first, fall-through to underlying, dispose-out-of-order (no-op restore), AsyncLocal isolation across `Task.Run` branches.
- `RunGoalAsync(GoalCall)`: parameters resolve inside the goal, are *not* present in `context.Variables` after the call returns.
- `GoalChannel.WriteAsync`: concurrent writes — Task.WhenAll of N writes, each verifies the goal saw its own envelope. This is the behavioural regression test for the original bug.

**Tests (PLang).** `Tests/Channels/GoalChannelConcurrentWrites.test.goal` — fire two `goalChannel.WriteAsync` calls concurrently, assert each goal invocation observed its own input.

## F1 — `Services` atomic Remove

`ConcurrentBag` has no Remove, the drain-and-rebuild isn't atomic, concurrent `New()` is silently lost. Replace the underlying store with `ConcurrentDictionary<Guid, Service>`.

- `Service.@this` gains `public Guid Id { get; } = Guid.NewGuid();`.
- `Services.@this`: `_services` becomes `ConcurrentDictionary<Guid, Service.@this>`. `New()` does `TryAdd(service.Id, service)`. `Remove(service)` does `TryRemove(service.Id, out _)`. `Count` and `GetEnumerator()` read `Values`.

**Test (C#).** Concurrent `New()` + `Remove()` interleaving — verify no service is dropped silently.

## F3 — guard `InvokeChannelHandler` against null Actor

```csharp
// today: Actor?.Context!  →  ctx is null  →  Handler call NREs
private async Task<Data.@this> InvokeChannelHandler(Binding.@this binding, Data.@this data, AskCallback? ask)
{
    if (Actor == null)
    {
        _ = App?.Debug?.Write($"[Channel '{Name}'] binding fired with null Actor — skipping");
        return Data.@this.Ok((object?)null);
    }
    return await binding.Handler(Actor.Context, null, data);
}
```

`Actor` becoming null is a setup bug (binding fired on a channel that was never registered). Diagnostic, no throw. After-handlers already swallow throws by design; before-handlers should also degrade gracefully here rather than turning a misconfiguration into an NRE.

**Test (C#).** Construct a `Channel` directly (no `Channels.Register`), add a binding that touches `ctx.App`, fire — verify diagnostic written and no exception.

## F4 — delete `App.Channels.Channel.EventContext`

Type is never constructed by the firing path. Deleting it. Stage 8 test that constructs it in isolation either gets deleted (if the only assertion was its shape) or rewritten to match the actual handler contract (`(ctx, action=null, data)`).

## F5 — honor `Encoding` in `Stream.ReadAllTextAsync` / `WriteTextAsync`

```csharp
private System.Text.Encoding ResolveEncoding() =>
    string.IsNullOrEmpty(Encoding)
        ? System.Text.Encoding.UTF8
        : System.Text.Encoding.GetEncoding(Encoding);

public async Task<string> ReadAllTextAsync(CancellationToken ct = default)
{
    var bytes = await ReadAllBytesAsync(ct);
    return ResolveEncoding().GetString(bytes);
}

public async Task WriteTextAsync(string text, CancellationToken ct = default)
{
    var bytes = ResolveEncoding().GetBytes(text);
    await WriteBytesAsync(bytes, ct);
}
```

**Test (C#).** Set `Encoding = "iso-8859-1"`, write a string with a non-ASCII character, read bytes back — verify the byte sequence matches the latin1 encoding, not utf-8.

## F6 — `channel.set` direction

Today: `name == "input"` → Input, everything else → Output. `GoalChannel` extends `Session` and supports `AskCore`, so a "chat" channel with `Direction = Output` reports `CanRead = false` while structurally being able to read.

Fix: default goal channels to `Bidirectional`, keep the input/output name shortcut, and accept an explicit `Direction` parameter.

```csharp
public partial Data.@this<string>? Direction { get; init; }   // optional: "input" | "output" | "bidirectional"

// in Run():
var direction = ResolveDirection(name, Direction?.Value);
//   - Direction explicit → parse it
//   - else if name == "input" → Input
//   - else if name == "output" → Output
//   - else → Bidirectional
```

**Test (C#).** `channel.set` with name "chat" produces a Bidirectional channel that AskAsync works on.
**Test (PLang).** A `Tests/Channels/SetBidirectionalGoalChannel.test.goal` that asks the channel and asserts the answer.

## Out of scope (logged separately)

- Goal-body mutations (`set %x% = 1`) on actor-shared `Variables` under concurrent calls. v5 fixes parameter passing only; goal-body mutation concurrency is a wider conversation about goal-as-method-on-actor vs goal-as-function. Logged in `Documentation/v0.2/todos.md`.

## Verification

- C# tests stay at 2744 pass + new Variables.Calls / Services / Channel tests added.
- PLang tests stay at 201 pass + new GoalChannelConcurrentWrites + SetBidirectionalGoalChannel.
- No regressions on either suite.
