# coder — runtime2-channels

## Version
v3

## What this is

Channel architecture cleanup — drops the `Channel.Role` enum, collapses
`channel.set` and `channel.add` into one always-upserting `set` verb,
replaces `EnsureRoleChannels` with `Channels.Verify`, retypes the goal
and actor slots properly so the builder pre-resolves PrPath, makes
`Channels.Resolve` return non-throwing nullable instead of throwing
`ChannelNotFoundException`, and preserves typed exception keys through
`Step.RunAsync`'s catch.

The architectural reset behind it: there was no behaviour requiring the
"role channel" / "custom channel" split. The names `output`, `error`,
`input` are pre-registered defaults; everything else is just channels.

## What was done

- **`Channel.Role` enum + `Channel.@this.Role` property — deleted.** No
  enum, no `IsDefault` / `IsCore` / `Direction` flag — just channels.
- **`Channels.@this.Defaults`** — static `["output", "error", "input"]`.
- **`Channels.@this.Verify()`** — boot invariant; replaces
  `App.EnsureRoleChannels`. Surfaces `MissingRequiredChannelAtBoot`.
- **`Channels.@this.Resolve(name)`** — non-throwing, returns
  `Channel.@this?`. Empty name → channel named `"output"`.
- **`ChannelNotFoundException` — deleted.** Source-gen-emitted IChannel
  slot adopts early-return pattern: resolve, if null surface a
  `ChannelNotFound` Data error directly.
- **`channel.set` rewritten:** Name + `Data<GoalCall>` Goal +
  `Data<Actor.@this>?` Actor + config slots. Always upserts. Builder
  stamps `GoalCall.PrPath` at build time (no more `Goals.Get(name)`
  registry-only lookup). Actor slot validates against `[Choices]` from
  v2.
- **`channel.add` — deleted.** `set` does both jobs.
- **`channel.remove`** — refuses for names in `Channels.Defaults`. Actor
  slot retyped to `Data<Actor.@this>?`.
- **`channel.migrate`** — same Actor retyping; calls `Channels.Resolve`.
- **`MigrationEnvelope.Role` — deleted.** Signature trimmed to
  `(Name, Direction, identity)`.
- **`Channel.@this.Verify(envelope)` → `VerifyEnvelope(envelope)`.**
  Disambiguates from `Channels.@this.Verify()` (boot invariant). Type
  carries the noun; method names verb only.
- **`NormalizeParameterTypes`** — skips `[Choices]`-bearing types. Same
  carve-out as scalar PlangTypes. Actor stays as a string in the .pr,
  runtime resolves via `App.GetActor` / static `Resolve(string,
  Context)`.
- **`Step.RunAsync` catch** — preserves the exception's class identity
  as the error Key (trimming trailing `Exception`). Bare `Exception`
  still falls back to `"StepError"`. Lets `on error key:"X"` handlers
  match typed runtime failures even when they slip through as
  exceptions rather than Data errors.

Test files updated for the new shape; `Stage5_ChannelActionsBuilderCatalogTests`
asserts `add` is gone, `Stage6_EntryPointWiringTests` calls
`Channels.Verify()`, `Stage9_ChannelMigrateTests` drops `Role` from
envelope assertions, etc. `Tests/Channels/Add/DuplicateName` scenario
deleted (the duplicate-rejection rule no longer exists). All goal files
under `Tests/Channels/` rewritten from `- add channel ...` to
`- set channel ...`.

## Code example

The new `channel.set` shape:

```csharp
public partial class Set : IContext
{
    public partial Data.@this<string> Name { get; init; }
    public partial Data.@this<GoalCall> Goal { get; init; }
    public partial Data.@this<global::App.Actor.@this>? Actor { get; init; }
    public partial Data.@this<long>? Buffer { get; init; }
    public partial Data.@this<TimeSpan>? Timeout { get; init; }
    public partial Data.@this<string>? Mime { get; init; }
    public partial Data.@this<string>? Encoding { get; init; }
    public partial Data.@this<App.Variables.Variable>? Encryption { get; init; }
    public partial Data.@this<App.Variables.Variable>? Signing { get; init; }
    // ... Goal pre-resolved by the builder; runtime calls GetGoalAsync.
}
```

And the source-gen IChannel slot resolution:

```csharp
{
    var __channelName = __action?.Parameters?.FirstOrDefault(...)?.Value as string;
    Channel = (context.Actor ?? app.User).Channels.Resolve(__channelName);
    if (Channel == null)
        return Data.@this.FromError(new ServiceError(
            $"Channel '{__channelName ?? "output"}' not found", __step,
            __callFrames, "ChannelNotFound", 404));
}
```

## Test status

- **C#**: 2744 pass, 0 fail. Stayed flat through the cleanup.
- **PLang**: 199 pass, 1 fail, 5 stale.
  - Baseline (start of v3): 191 pass, 10 fail, 5 stale.
  - **+8 channel scenarios now pass.** Helper-goal-not-found,
    actor-hallucination, ChannelNotFound-key-flattened, role-vs-custom
    confusion — all gone.
  - **1 channel scenario still fails** — `Events/AddBeforeWrite`
    "%approvalCalled% is null". The build is correct
    (`channel.set` + `event.on`) and runs without exception, but the
    BeforeWrite event handler doesn't fire. This is event-binding
    execution, not channel architecture — separate concern from v3.
  - **5 stale**: 4 pre-existing Callback (not this branch's scope) +
    1 `Add/WithConfig` (LLM step-splitting on long modifier-chain
    line — separate concern, noted in v3 plan as out of scope).

Total deleted scenario: `Tests/Channels/Add/DuplicateName` (the
DuplicateChannelName error no longer exists; nothing left to test).

## What's next — possible v4

1. **`Events/AddBeforeWrite` BeforeWrite firing** — debug why the
   binding doesn't fire on `write 'payload' to audit`. Likely either:
   the binding's `ChannelName` filter isn't matching the resolved
   channel name in the event-firing path, or the firing path itself
   isn't wrapping `WriteCore` correctly for BeforeWrite. Stage 8 work
   from v1 needs verification end-to-end.
2. **`Add/WithConfig` step-splitting** — LLM consistently splits the
   long modifier-chain line. Either re-shape the goal text or harden
   the builder's step-counter prompt.
3. **Dynamic `[Choices]`** — extension idea from the v3 design
   discussion: build-time-cumulative vocabulary so channel names
   declared by `set` join the choices list for subsequent steps.
   Generalizes to other declare-then-use patterns. Decision deferred
   from v3.
