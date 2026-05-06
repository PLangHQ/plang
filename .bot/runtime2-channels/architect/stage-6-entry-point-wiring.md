# Stage 6: Entry-point wiring (PlangConsole)

**Goal:** Move stream registration out of the runtime and into the entry point. PlangConsole registers six channels (User × 3 roles + System × 3 roles) at boot. App ctor and Channels ctor stop opening console streams. Foundational set freezes after entry-point setup, before goal execution.

**Scope:**
- Update `PlangConsole/Program.cs` (or equivalent main entry) to register channels via navigation.
- Remove `Console.OpenStandard*` from `App.Channels.@this` constructor and `App.@this` constructor.
- Drop the `App.Channels` shortcut entirely (it has no real job after this branch — see plan).
- Promote `Serializers` from `App.Channels.Serializers` to `App.Serializers` directly (since App.Channels no longer exists as a separate object).
- Add `App.@this.FreezeFoundational()` (called after entry-point setup, captures per-actor foundational sets — used by Stage 3's recursion rule).
- Add the App.Run() startup-time invariant check: every actor that does I/O has `Output`/`Error`/`Input` registered. Fail fast if not.
- **Excluded:** Service-related changes (Stage 7); events (Stage 8); web entry (deferred).

**Deliverables:**

1. **`PlangConsole/Program.cs`** (or wherever the entry main lives):
   ```csharp
   await using var app = new App(absolutePath);
   
   // User actor
   app.User.Channels.Register(new Channel.Stream("output", Console.OpenStandardOutput(), Role.Output));
   app.User.Channels.Register(new Channel.Stream("error",  Console.OpenStandardError(),  Role.Error));
   app.User.Channels.Register(new Channel.Stream("input",  Console.OpenStandardInput(),  Role.Input));
   
   // System actor
   app.System.Channels.Register(new Channel.Stream("output", Console.OpenStandardOutput(), Role.Output));
   app.System.Channels.Register(new Channel.Stream("error",  Console.OpenStandardError(),  Role.Error));
   app.System.Channels.Register(new Channel.Stream("input",  Console.OpenStandardInput(),  Role.Input));
   
   await app.Run();   // calls FreezeFoundational() internally before any goal runs
   ```

2. **`App.@this`** changes:
   - Remove the property `Channels` (the App-level shortcut).
   - Remove `Console.OpenStandard*` references from the ctor.
   - Add `Serializers : Serializers.@this` directly on App (was `Channels.Serializers`).
   - Add `FreezeFoundational()` method — called by `Run()` (or equivalent startup) before goal execution. For each actor, snapshot its current Channels into `actor.FoundationalChannels` (per Stage 3's design).

3. **`App.Channels.@this`** (the per-actor collection type) changes:
   - Remove `Console.OpenStandard*` from ctor.
   - Becomes a pure registry: empty on construction, populated by entry point or by `channel.add` / `channel.set` actions.

4. **App.Run startup invariant check** — before any goal runs, verify every actor that has Channels registered has at least Output, Error, Input. Missing → throw with a clear message ("Actor 'User' missing required channel: Error"). Fail fast.

5. Existing call sites of `app.Channels.Serializers` get rewritten to `app.Serializers`. Search-and-replace, no semantic change.

**Dependencies:** Stages 1, 2, 4. Needs Channel base, Stream channel concrete, and the IChannel update so existing actions keep working.

## Design

### Why entry point owns this

PlangConsole is the console wiring; PlangWindow / Mobile / etc. will be their own entry points. The runtime can't know which streams to use because it depends on the host process. Putting the wiring in the entry point matches the variation surface — the only thing that differs across platforms IS the streams.

The runtime's job: provide the registration API, enforce invariants, freeze foundational state.

### Foundational freeze

Per Stage 3 design: `actor.FoundationalChannels` is the immutable snapshot of channels as registered by the entry point. Goal channels (registered later) capture this snapshot for their recursion-safe writes.

Freeze happens *after* entry-point setup, *before* goal execution. The natural call site is `App.Run()`'s prologue:

```csharp
public async Task<Data.@this> Run(...)
{
    EnforceChannelInvariants();   // throws if Output/Error/Input missing
    foreach (var actor in [System, User]) actor.FreezeFoundational();
    // ... then run the goal
}
```

Programs that register channels AFTER `Run()` started (via `channel.add`, `channel.set`) only affect the actor's current Channels, not the foundational set. That's correct — you can rebind `output` mid-run, but the foundational stdout reference stays valid for goal-channel recursion.

### Why drop `app.Channels`

After this branch:
- Action handlers use the source-gen-resolved `Channel`, not the registry.
- `Serializers` is on App directly.
- Goal/Setup loaders that today use `app.Channels.Serializers` switch to `app.Serializers`.

Nothing left for `app.Channels` to do. Smell #3 (same logical thing exposed twice — once via App, once via per-actor) goes away.

### Web-entry teaser (not this stage)

For documentation: a future web entry follows the same pattern, with HTTP request/response streams instead of console:

```csharp
// per-request handler in webserver.start (out of scope this branch)
app.User.Channels.Register(new Channel.Stream("output", httpResponse.Body, Role.Output));
app.User.Channels.Register(new Channel.Stream("error",  httpResponse.Body, Role.Error));
app.User.Channels.Register(new Channel.Stream("input",  httpRequest.Body,  Role.Input));
```

Same shape, different streams. PlangConsole's wiring is the template.
