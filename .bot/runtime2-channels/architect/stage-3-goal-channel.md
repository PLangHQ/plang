# Stage 3: Goal channel + recursion rule

**Goal:** Channel backed by a Goal call, not a Stream. Writes invoke the goal with the Data as input. Implements the recursion rule so goal-channel writes inside the goal resolve to the original entry-point streams, not the current overlay.

**Scope:**
- New `App/Channels/Channel/Goal/this.cs` extending `Channel.Session.@this`.
- Recursion rule infrastructure: capture "fundamental" channels at Goal-channel registration time; goal execution sees those, not overlays.
- **Excluded:** event firing (Stage 8), `channel.add` PLang surface (Stage 5), Web channel (deferred).

**Deliverables:**
- `App/Channels/Channel/Goal/this.cs` — concrete, sealed.
  - Constructor takes `(name, goal: App.Goals.Goal.@this, role, direction)`. Captures a snapshot reference of the registering actor's *current* Channels at construction time — this becomes the "fundamental" set seen during goal execution.
  - `WriteCore(Data, ct)` — calls `app.Run(this.Goal, ...)` with the Data bound as input (likely as `%!data%` in the goal's Variables). Returns the goal's result Data.
  - `ReadCore(ct)` — Same shape: invokes the goal, expects it to produce Data. (Most goal channels are write-oriented; reads are valid but rare.)
  - `Ask(callback)` — Session-style: invokes goal with the AskCallback as input, expects answer Data back, returns it. The goal can do whatever it wants to produce the answer.
- Runtime mechanism so when a Goal channel's goal runs, channel writes inside it see the captured fundamental set (not the actor's current Channels). Likely: the goal's Context has its `Channels` overridden to point at the captured set for the duration of the call.

**Dependencies:** Stages 1, 2. (Stream channel needs to exist so the captured fundamental set has something concrete to point at — usually Stream channels.)

## Design

### Why Session, not Message

A Goal channel's behaviour from the *caller's* perspective is Session-like: write → goal runs → result returned, synchronous in the call. Even if the goal internally does long work, the caller blocks until it returns. Matches Session's "ask blocks, returns answer."

(If a goal wanted Message behaviour — return a callback to suspend the caller — that's expressible *inside* the goal: it would issue `ask user` itself, which already returns AskCallback. That suspend/resume is a goal-internal concern, not the channel's.)

### Recursion rule — "fundamental streams"

The problem: a goal channel's body might do `- write out %!data%`. If the channel that fired is the current `output`, the write loops back into itself — infinite recursion.

The fix: at Goal-channel registration time, capture a snapshot of the registering actor's Channels. Call this the **fundamental set** for that goal channel. When the goal runs, channel writes inside it resolve against the fundamental set, not the actor's current overlay.

```csharp
public sealed class @this : Session.@this
{
    private readonly App.Channels.@this _fundamental;   // captured at ctor

    public @this(string name, Goal goal, Role role, App.Channels.@this currentChannels)
    {
        _fundamental = currentChannels.Snapshot();   // see note
        // ...
    }

    public override async Task<Data.@this> WriteCore(Data.@this data, CancellationToken ct)
    {
        var goalContext = ...;
        // Override goalContext.Actor.Channels to point at _fundamental for this run
        return await app.Run(this.Goal, goalContext, data);
    }
}
```

`Snapshot()` is a shallow copy of the registry — same Channel instances, frozen at this moment. Channels added/removed/replaced after the goal channel was registered don't appear in the snapshot.

A Goal-of-Goal scenario: GoalA is registered as `output`, then GoalB is registered as `output`. GoalB's fundamental snapshot was taken when GoalA was current, so it sees GoalA. GoalB's `- write out` would call GoalA, which has its own (older) fundamental snapshot of pre-GoalA channels. Walks back to original streams. Each layer captures its predecessor — not a problem because the snapshot is frozen at registration; future replacements don't affect it.

Wait — but the plan says "writes inside use the original entry-point streams, not the overlay." If GoalB's snapshot shows GoalA, then GoalB's `- write out` goes to GoalA, not original stdout. That's an implicit chain.

Re-reading the plan: "Stacked overrides do **not** chain implicitly... GoalB's `- write out` goes to the *original* stdout, not to GoalA." So the rule is: capture the *original entry-point set* at App-init time, not the registering moment.

Meaning: the fundamental set is whatever the entry point registered at boot. It doesn't change over time. Goal channel registered at any point captures *the same* fundamental set (the boot-time one).

Implementation: App keeps an immutable record of the boot-time channel set per actor. Call it `actor.FoundationalChannels`. Goal channel ctor captures a reference to that. Goal execution overrides `Context.Actor.Channels` to be the foundational set for the duration of the run.

```csharp
// App/Actor/this.cs additions
public App.Channels.@this FoundationalChannels { get; private set; }
public void FreezeFoundational() { FoundationalChannels = Channels.Snapshot(); }
```

App.Run / engine startup calls `FreezeFoundational()` after the entry point has registered all initial channels but before goal execution starts.

### Why this design

- **Predictable.** No matter how many overlays the user installs, goal channels always write to the same place — original streams.
- **Composition is explicit.** If you want GoalB to call GoalA, `- call GoalA data=%!data%`. Implicit chaining hides flow; explicit `call` shows it.
- **Single source of truth.** The foundational set is a clear concept the user can reason about: "whatever PlangConsole.Main set up at start."

### Cool capability that emerges

The recursion rule turns goal-channel + write-out into a fan-out primitive (see plan.md). Document this in Stage 3 tests so the pattern is exercised: a Logger goal that writes to a file AND writes-out (going to fundamental stdout). Both effects happen, no recursion.
