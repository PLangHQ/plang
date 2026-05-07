# Channel events — deep dive

## What it is

A `Channel.@this` exposes an `Events` property — same shape as `Goal.Events`, `Step.Events`, `Action.Events`. Bindings registered globally on the User Context's event registry are looked up by channel name when a channel operation runs. The Channel base class wraps each operation in before/after firing.

This makes channels first-class event-bindable objects in PLang, on equal footing with goals/steps/actions.

## The shape

### EventType additions

Added to `App.Events.EventType` enum:

| EventType     | Fires                                              |
|---------------|----------------------------------------------------|
| `BeforeWrite` | Before each `Channel.WriteAsync` body executes     |
| `AfterWrite`  | After each `Channel.WriteAsync` body, regardless of success/failure |
| `BeforeRead`  | Before each `Channel.ReadAsync` body executes      |
| `AfterRead`   | After each `Channel.ReadAsync` body                |
| `OnAsk`       | When `Channel.Ask` resolves a callback (Session: just before reading the answer; Message: when serialising the callback to the wire) |

`OnError` (existing) also fires for channel errors, with a Channel reference in the EventContext — no new EventType needed for the error case.

### EventBinding filter addition

`EventBinding` today filters by `goalName`, `stepText`, `module`, `actionName`. Add:

```csharp
public string? ChannelName { get; init; }
```

Bindings without `ChannelName` set don't match channel events (so existing goal/step/action bindings aren't accidentally triggered by channel operations). Bindings with `ChannelName` set match operations on that channel by name.

### EventContext carries channel + data

`EventContext` today carries `Step` + `Phase`. For channel events it additionally needs:

```csharp
public Channel.@this? Channel { get; init; }      // the channel firing
public Data.@this? Data { get; init; }            // the Data being written/read; null for OnAsk
public AskCallback? Ask { get; init; }            // populated for OnAsk; null otherwise
```

The handler goal accesses these via `%!event.channel%`, `%!event.data%`, `%!event.ask%`.

### Firing site — Channel base wraps every operation

Implemented once on `Channel.@this`, inherited by every concrete channel (Stream, Goal, Web). The concrete subtypes don't think about events — they implement `WriteAsync`, `ReadAsync`, `Ask` and the base wraps:

```csharp
// Pseudocode on Channel.@this
public async Task<Data.@this> WriteAsync(Data.@this data, CancellationToken ct = default)
{
    var beforeCtx = new EventContext { Channel = this, Data = data, Phase = Before };
    await Events.Before.Run(beforeCtx);   // may throw → caller sees abort
    
    Data.@this result;
    try
    {
        result = await WriteCore(data, ct);   // abstract — subtype implements
    }
    finally
    {
        var afterCtx = new EventContext { Channel = this, Data = data, Phase = After };
        await Events.After.Run(afterCtx);     // fires regardless of success/failure
    }
    return result;
}
```

`WriteCore` / `ReadCore` / `AskCore` are the new abstract methods on Channel.@this; today's `WriteAsync` / `ReadAsync` / `Ask` become non-virtual sealed wrappers that fire events.

## Semantics that must be settled (and committed)

### 1. Before-handlers can abort, cannot mutate

A Before-handler that throws (or returns Data.Error) aborts the operation. The Core method never runs. After-handlers don't fire (because the operation didn't happen — the throw is the result). Caller sees the thrown error.

A Before-handler **cannot mutate** the Data being written. The handler sees `%!event.data%` as read-only. Reasons:

- Mutation would invalidate signatures already on the Data.
- Channels with mutation pipelines is what *Goal channels* are for (transformation via composition). Events are for cross-cutting hooks (validation, logging, alerting) — not transformation.
- Read-only-with-abort covers the validation case cleanly: validate the data, throw if bad, write proceeds otherwise.

If a developer needs to transform: use a Goal channel.

### 2. After-handlers always fire

Even if WriteCore threw, After fires. The handler sees the result via `%!event.data%` and can inspect what was attempted. Useful for metrics ("count attempts, not just successes") and audit ("log every attempt, mark which failed").

If an After-handler itself throws, it does NOT change the operation's result — the original outcome stands. After-handler errors get logged but suppressed (probably surface to error channel separately).

### 3. Recursion guard via existing mechanism

Today's `Context._activeEventBindings` (a `ConcurrentDictionary<string, byte>`) prevents re-entering a binding while it's already active. Channel events plug into the same guard — `Events.Before.Run` checks active set before firing each binding, skips if already active.

Concrete: a BeforeWrite handler that itself writes to the same channel won't infinite-loop. The second write's BeforeWrite event sees the binding already active and skips. The write proceeds without re-running the handler.

### 4. Ordering — registration order

Multiple bindings matching the same event fire in registration order (first-registered runs first). Same as today's goal/step/action events. If A registers BeforeWrite, then B registers BeforeWrite, A fires before B on every write.

If A throws, B doesn't fire. The first thrower wins.

### 5. OnAsk timing differs by channel kind

- **Session channel:** OnAsk fires just *after* the channel receives the answer to an Ask — handler can inspect or rewrite (no, read-only — see rule 1) the answer. Useful for logging "what did the user say" or auditing.
- **Message channel:** OnAsk fires when the channel is about to serialise the callback to its outbound surface — handler sees the AskCallback before it leaves the process. Useful for adding auth headers, tracing IDs, etc., to the outbound representation. (The handler still can't mutate; it can only inspect or abort. If a Message channel needs callback transformation, use a Goal channel wrapping the Message channel.)

### 6. Service channels participate

Bindings with a channel-name filter match channels owned by ANY actor — including Services. So `- add before write on "input" channel, call LogInbound` triggers for User's input writes AND for Service's per-call input writes (the response stream of an outbound HTTP call).

If you want actor-specific filtering, the binding needs an additional actor filter — but that's a future enhancement, not this branch. Today: by name only.

### 7. Bindings live on the User Context registry

Same place existing event bindings live. `event.add` (or whatever the action is named) registers; `event.remove` unregisters. No new storage; existing infrastructure handles it.

For System-side bindings (set by /system/error.goal or boot scripts), they go on System's context. The lookup checks both. This matches today's behaviour.

## EventContext payload contract

```csharp
public class EventContext
{
    // Existing
    public Step.@this? Step { get; init; }
    public EventPhase Phase { get; init; }

    // New for channel events
    public Channel.@this? Channel { get; init; }   // null when fired by goal/step/action
    public Data.@this? Data { get; init; }         // populated for Write/Read events
    public AskCallback? Ask { get; init; }         // populated for OnAsk only
}
```

Handler goals see `%!event.channel%`, `%!event.data%`, `%!event.ask%`. Existing dot-path resolution covers the access — no special wiring needed beyond exposing the new properties.

`Step` is null for channel events (channel ops aren't bound to a specific step the way action events are). If a handler wants to know "which step caused this write," it can walk `Channel.Parent` (Service has a parent reference; need a similar back-reference for channels — TBD if needed).

## What this does NOT do

- **No mutation of in-flight Data.** Read-only inspection plus abort. Mutation belongs in Goal channels.
- **No "OnRegister" / "OnRemove" lifecycle events.** Channels register/remove rarely; not worth the surface. Goal-channel composition covers any "intercept registration" need.
- **No per-actor binding filter.** Channels-of-the-same-name across actors all match. Adding an actor filter to EventBinding is a future enhancement.
- **No mid-write progress events.** A Stream's chunked write fires BeforeWrite once and AfterWrite once, not per chunk. If progress matters, the goal channel pattern handles it.

## PLang surface

Existing `events` module gets new event types and a channel-name filter. Examples:

```
# Validation
- add before write on "user.created" channel, call ValidateUser

# Audit
- add after write on "audit.external" channel, call LogAttempt

# Sudo-for-I/O
- add before write on "audit.external" channel, call AskCompliance

# Captcha on input
- add on ask on "input" channel, call CheckCaptcha

# Metrics
- add after write on "logger" channel, call EmitWriteMetric
```

The builder catalog learns:
- New EventType values (BeforeWrite/AfterWrite/BeforeRead/AfterRead/OnAsk).
- The "on '<name>' channel" syntax fragment maps to the `channelName` filter.
- Same intent-based mapping as `to <name>` — LLM uses the channel inventory to pick the right name.

## Test surface for the coder/test-designer

Behaviours that must be pinned by tests:

1. BeforeWrite handler receives correct `%!event.data%` and `%!event.channel%`.
2. BeforeWrite throwing aborts the write; AfterWrite does NOT fire.
3. AfterWrite fires even when WriteCore throws (with the failure visible in EventContext).
4. After-handler throwing is suppressed; original operation result stands.
5. Recursion guard: BeforeWrite handler that writes to same channel doesn't infinite-loop.
6. Multiple bindings fire in registration order.
7. First binding to throw stops subsequent bindings from running.
8. OnAsk on Session channel fires post-answer; OnAsk on Message channel fires pre-serialise.
9. Bindings match across actors (User and Service channels with same name both trigger).
10. Channel events do NOT fire for goal/step/action bindings (existing event types are unchanged in scope).

## Stage 8 deliverables (concrete)

Coder produces:

- `App.Events.EventType` enum: 5 new values.
- `App.Events.Lifecycle.Bindings.Binding.@this`: add `ChannelName` filter property.
- `App.modules.EventContext`: add `Channel`, `Data`, `Ask` properties.
- `App.Channels.Channel.@this`: add `Events` property; refactor `WriteAsync` / `ReadAsync` / `Ask` into sealed wrappers + abstract `WriteCore` / `ReadCore` / `AskCore`. Concrete subtypes (Stream, Goal) move their bodies to the Core method.
- `App.Actor.Context.@this.GetEventBindings`: extend the owner-switch to handle `Channel.@this` (uses `BeforeWrite`/`AfterWrite` etc., filters by `ChannelName`).
- `App.modules.events.add`: extended PLang surface for the new event types and channel filter.
- Builder catalog updates: new EventType values + `channelName` filter described.
- Tests covering points 1-10 above.
