# coder — runtime2-channels

## Version
v4 (continuation of v3 — channel architecture cleanup + event firing fix)

## What this is

v3 collapsed the `Channel.Role` enum, merged `set`/`add` into one
upserting verb, replaced `EnsureRoleChannels` with `Channels.Verify`,
retyped channel handler slots properly, and made `Channels.Resolve`
return non-throwing nullable. v4 closes out the last channel test
failure: BeforeWrite event bindings registered via `event.on` weren't
firing on writes.

## v4 — event firing fix

**Bug:** `event.on` registers bindings on `targetActor.Context.Events`
but `Channel.@this.MatchingBindings` was reading from `App.Events`. Two
separate registries — bindings invisible to the firing path.

**Fix:** stamp the owning actor onto each Channel via
`Channels.Register`, and have `MatchingBindings` consult both
`Actor.Context.Events` (where `event.on` writes) and `App.Events`
(preserves cross-actor matching for the same channel name on User +
Service).

Concretely:

- `Channel.@this.Actor` — new property; stamped by `Channels.Register`
  using a back-reference set on `Channels.@this.Actor` (Actor sets it
  right after constructing its Channels collection). Replaces
  `Channel.Goal.@this.RegisteringActor` (renamed to `Actor` and
  pulled up to base — same concept, smaller name, OBP-correct: the
  receiver carries the noun).
- `MatchingBindings` reads from per-channel Events list →
  `Actor.Context.Events` → `App.Events`.
- `InvokeChannelHandler` passes `Actor.Context` instead of `null!` so
  the handler can dispatch via `ctx.App.RunGoalAsync`.

The wider events architecture (one `AppEvents` registry on App, one
per actor on Context — same concept twice) is logged as a separate
todo in `Documentation/v0.2/todos.md` for a future pass.

## v3 architecture cleanup

- **`Channel.Role` enum + `Channel.@this.Role` property — deleted.**
  No enum, no `IsDefault`/`IsCore`/`Direction` flag — just channels.
- **`Channels.@this.Defaults`** — static `["output", "error", "input"]`.
- **`Channels.@this.Verify()`** — boot invariant; replaces
  `App.EnsureRoleChannels`. Surfaces `MissingRequiredChannelAtBoot`.
- **`Channels.@this.Resolve(name)`** — non-throwing, returns
  `Channel.@this?`. Empty name → channel named `"output"`.
- **`ChannelNotFoundException` — deleted.** Source-gen-emitted IChannel
  slot adopts early-return pattern.
- **`channel.set` rewritten:** Name + `Data<GoalCall>` Goal +
  `Data<Actor.@this>?` Actor + config slots. Always upserts. Builder
  stamps `GoalCall.PrPath` at build time — runtime no longer reaches
  for the `Goals.Get(name)` registry-only shortcut.
- **`channel.add` — deleted.** `set` does both jobs.
- **`channel.remove`** — refuses for names in `Channels.Defaults`.
- **`MigrationEnvelope.Role` — deleted.** Signature trimmed to
  `(Name, Direction, identity)`.
- **`Channel.@this.Verify(envelope)` → `VerifyEnvelope(envelope)`.**
  Disambiguates from `Channels.@this.Verify()`.
- **`NormalizeParameterTypes`** — skips `[Choices]`-bearing types so
  Actor stays as a string in the .pr; runtime resolves via
  `App.GetActor`.
- **`Step.RunAsync` catch** — preserves the exception's class identity
  as the error Key (trims trailing `Exception`). Bare `Exception` →
  `"StepError"`. Lets `on error key:"X"` handlers match typed runtime
  failures even when they slip through as exceptions.

## Code example

```csharp
public sealed class @this    // Channels collection
{
    internal global::App.Actor.@this? Actor { get; set; }

    public void Register(Channel.@this channel)
    {
        channel.App = _app;
        if (channel.Actor == null) channel.Actor = Actor;   // stamp
        _channels[channel.Name] = channel;
    }
}
```

```csharp
private IEnumerable<Binding.@this> MatchingBindings(EventType type)
{
    foreach (var b in Events) if (b.Type == type && ...) yield return b;
    if (Actor != null)
        foreach (var b in Actor.Context.Events.GetBindings(type))
            if (string.Equals(b.ChannelName, Name, ...)) yield return b;
    if (App != null)
        foreach (var b in App.Events.GetBindings(type))
            if (string.Equals(b.ChannelName, Name, ...)) yield return b;
}
```

## Test status

- **C#**: 2744 pass, 0 fail. Flat through v3 and v4.
- **PLang**: **200 pass, 0 fail, 5 stale** (was 188/0/18 at branch
  start; was 191/10/5 after v2; was 199/1/5 after v3).
  - All channel scenarios pass.
  - 5 stale: 4 pre-existing Callback (out of scope) + 1
    `Add/WithConfig` (LLM step-splitting on long modifier-chain line —
    separate concern noted in v3 plan).

## What's next (out of scope here)

1. **`Add/WithConfig` step-splitting** — LLM-noise issue.
2. **Events architecture pass** — `App.Events` vs `Context.Events`
   are two registries for one concept. Logged in
   `Documentation/v0.2/todos.md`.
3. **Dynamic `[Choices]`** — build-time-cumulative vocabulary so
   declared channel/variable/service names extend the LLM's choice
   list for subsequent steps. Designed in v3, deferred.
