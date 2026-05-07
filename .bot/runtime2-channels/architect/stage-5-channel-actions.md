# Stage 5: `channel.set`, `channel.add`, `channel.remove` actions

**Goal:** PLang surface for managing channels — replacing role-channels, adding custom channels, removing them. New `channel` module containing three actions.

**Scope:**
- New module folder `App/modules/channel/`.
- Three action handlers: `set.cs`, `add.cs`, `remove.cs`.
- Module description and catalog hooks.
- Builder catalog updates so the LLM emits the right JSON for these actions.
- **Excluded:** entry-point wiring (Stage 6); events (Stage 8); migrate (Stage 9).

**Deliverables:**

1. **`App/modules/channel/set.cs`** — replaces an existing role-channel with a Goal-backed channel.
   ```csharp
   [Action("set")]
   public partial class Set : IContext, IChannel
   {
       public partial Data.@this<Role> Role { get; init; }
       public partial Data.@this<App.Variables.Variable>? Actor { get; init; }   // optional, defaults to current
       public partial Data.@this<App.Variables.Variable> Goal { get; init; }     // the goal name to call
       
       public Task<Data.@this> Run() { /* see below */ }
   }
   ```
   Run logic: resolve target actor (default = `Context.Actor`), look up Role channel, replace it with a `Channel.Goal` pointing at `Goal`.

2. **`App/modules/channel/add.cs`** — registers a new custom-named channel.
   ```csharp
   [Action("add")]
   public partial class Add : IContext, IChannel
   {
       public partial Data.@this<string> Name { get; init; }
       public partial Data.@this<App.Variables.Variable>? Actor { get; init; }
       public partial Data.@this<App.Variables.Variable> Goal { get; init; }
       public partial Data.@this<long>? Buffer { get; init; }
       public partial Data.@this<TimeSpan>? Timeout { get; init; }
       public partial Data.@this<string>? Mime { get; init; }
       public partial Data.@this<string>? Encoding { get; init; }
       public partial Data.@this<App.Variables.Variable>? Encryption { get; init; }
       public partial Data.@this<App.Variables.Variable>? Signing { get; init; }
       
       public Task<Data.@this> Run() { /* see below */ }
   }
   ```
   Run logic: build a `Channel.Goal` with the provided config; register on the target actor's Channels under `Name`. Reject if a channel with that name already exists (use `set` to replace).

3. **`App/modules/channel/remove.cs`** — unregisters a channel by name.
   ```csharp
   [Action("remove")]
   public partial class Remove : IContext, IChannel
   {
       public partial Data.@this<string> Name { get; init; }
       public partial Data.@this<App.Variables.Variable>? Actor { get; init; }
       
       public Task<Data.@this> Run() { /* see below */ }
   }
   ```
   Run logic: if a standard role-channel (output/error/input), refuse — those are entry-point invariants and can't be removed (only replaced via `set`). Otherwise, remove and dispose.

4. **Module description** in `App/modules/channel/types.cs` (matching the existing modules' pattern) so the builder catalog knows about the module.

5. **Builder catalog updates** so the LLM emits correct JSON for these actions, including the optional config parameters from `add`. `Buffer` is integer bytes; `Timeout` is ISO 8601 duration string (handled by Stage 1's converter).

**Dependencies:** Stages 1, 2, 3. Needs Channel base, Stream channel (for default fundamental), and Goal channel (since these actions register Goal channels).

## Design

### Why three separate actions, not one

`channel.set` (replace by role) and `channel.add` (register by name) are conceptually different operations:
- `set` is invariant-preserving: the role-channel still exists with that role; just its backing changed.
- `add` is namespace expansion: a new name appears in the registry.
- `remove` is the inverse of `add`.

Conflating `set` and `add` would muddle whether "set output channel as X" creates a NEW channel called "output" or REPLACES the existing one. With separate actions the intent is unambiguous from the verb.

### Actor parameter

`Actor: Data<Variable>?` — optional. If unset, defaults to `Context.Actor` (whoever's currently executing). If set, target the named actor's Channels.

The PLang text for setting a non-current-actor's channel:
```
- set system output channel as OutputGoal
```

LLM extracts `Actor = "system"`, `Role = Output`, `Goal = OutputGoal`.

### Why config goes only on `add`, not `set`

`set` replaces a role-channel with a Goal channel. The Goal channel's config comes from the goal's behaviour, not from the action call. If you need finer-grained config on the goal channel, `set` could grow the same parameters as `add` later — but for the first cut, keep `set` minimal.

### Refusing to remove role-channels

Per plan invariant: every actor that does I/O has all three role-channels. `channel.remove` enforces this — removing `output`/`error`/`input` returns Data.Error with `ChannelInvariantViolation`. To replace a role-channel use `set`, not `remove`+`add`.
