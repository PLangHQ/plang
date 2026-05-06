# Channels — test coverage matrix

## Coverage matrix

Organised by topic file / stage. One row per behaviour the design commits to. Test-designer reads top-to-bottom and writes one test per row.

### Stage 1 — Channel base + Session/Message + Role + Config

| Behaviour | Layer | Sense |
|-----------|-------|-------|
| Channel base properties (Name, Role, Direction, Buffer, Timeout, Mime, Encoding) round-trip on a concrete subtype | C# | green |
| Default values match the table (Buffer=4096, Timeout=30s, Mime="text/plain", Encoding="utf-8") | C# | green |
| TimeSpan JsonConverter writes "PT30S" for 30-second TimeSpan | C# | green |
| TimeSpan JsonConverter reads "PT5M" and produces 5-minute TimeSpan | C# | green |
| TimeSpan JsonConverter rejects malformed input ("not-a-duration") | C# | negative |
| Channel base abstract methods enforced (subtype must implement WriteCore/ReadCore/AskCore) | C# (compile) | green |
| Role enum values: Output, Error, Input | C# | green |
| Encryption/Signing default null/auto | C# | green |

### Stage 2 — Stream channel

| Behaviour | Layer | Sense |
|-----------|-------|-------|
| Channel.Stream WriteCore writes Data to underlying Stream via Serializer | C# | green |
| Channel.Stream ReadCore reads bytes, deserialises via Mime | C# | green |
| Channel.Stream.Memory factory creates write+read-able channel | C# | green |
| Channel.Stream.Output factory creates write-only channel | C# | green |
| Channel.Stream.Input factory creates read-only channel | C# | green |
| WriteCore fails with WriteError when underlying Stream throws | C# | negative |
| ReadCore fails with ReadError when underlying Stream throws | C# | negative |
| Ask blocks on stdin-style Stream until input arrives | C# | green |
| Ask times out per channel.Timeout config | C# | negative |
| ownsStream=true disposes underlying Stream on Channel disposal | C# | green |
| ownsStream=false leaves underlying Stream open on Channel disposal | C# | green |

### Stage 3 — Goal channel + recursion rule

| Behaviour | Layer | Sense |
|-----------|-------|-------|
| Channel.Goal WriteCore invokes the goal with Data bound | C# | green |
| Channel.Goal WriteCore returns the goal's result Data | C# | green |
| Goal channel registered before App.Run.Freeze captures pre-freeze foundational set | C# | green |
| Goal channel writes inside its goal use foundational channels, not current overlay | C# | green |
| Goal channel registered as "output", goal's `- write out` reaches foundational stdout | C# / integration | green |
| Stacked overrides: GoalA → GoalB; GoalB's `- write out` goes to foundational, not GoalA | C# | green |
| Fan-out via composition: goal writes to file + writes-out → both happen, no recursion | C# (Cut 2) | green |
| Goal channel Ask invokes goal, returns its answer | C# | green |
| Channel.Goal disposal does not dispose the underlying goal | C# | green |

### Stage 4 — Write Channel slot + IChannel + builder

| Behaviour | Layer | Sense |
|-----------|-------|-------|
| Source-gen emits Channel resolution code for IChannel actions | C# | green |
| Channels.Resolve(null) returns the Output role channel | C# | green |
| Channels.Resolve(name) returns the named channel | C# | green |
| Channels.Resolve(unknown name) throws ChannelNotFound | C# | negative |
| Write.Run with no channel slot writes to default Output | C# / goal | green |
| Write.Run with channel slot writes to that channel | C# / goal | green |
| Write passes full Data envelope to Channel.WriteAsync (not Data.Value) | C# | green |
| Builder catalog teaches LLM the channel parameter on IChannel actions | C# (builder test) | green |
| Builder catalog passes per-actor channel inventory at build time | C# (builder test) | green |
| Builder maps user intent ("to logger") to Channel="logger" using inventory, not pattern parsing | goal (builder integration) | green |
| WriteAsync(Write action) overload removed from Channels.@this | C# (compile) | green |

### Stage 5 — channel.set / .add / .remove

| Behaviour | Layer | Sense |
|-----------|-------|-------|
| channel.set replaces a role-channel with a Goal-backed channel | goal | green |
| channel.set with explicit actor targets that actor (`set system output ...`) | goal | green |
| channel.set without explicit actor uses Context.Actor | goal | green |
| channel.add registers a new named Goal channel | goal | green |
| channel.add with config (buffer/timeout/etc.) honours those values | goal | green |
| channel.add on a duplicate name returns Data.Error (use set to replace) | goal | negative |
| channel.remove unregisters a custom-named channel | goal | green |
| channel.remove on a standard role channel (output/error/input) returns Data.Error (ChannelInvariantViolation) | goal | negative |
| Builder catalog includes the three actions with all parameters | C# (builder test) | green |

### Stage 6 — Entry-point wiring

| Behaviour | Layer | Sense |
|-----------|-------|-------|
| App.@this ctor no longer opens Console.OpenStandard* | C# (smoke: construct App, verify no console fd opened) | green |
| App.@this no longer has a `Channels` property (the shortcut) | C# (compile) | green |
| App.Serializers exists at App level | C# | green |
| App.Run() invariant check fails if any actor missing Output role channel | C# | negative |
| App.Run() invariant check fails if any actor missing Error role channel | C# | negative |
| App.Run() invariant check fails if any actor missing Input role channel | C# | negative |
| App.Run() invariant check passes when all role-channels registered for an I/O actor | C# | green |
| FreezeFoundational captures snapshot per actor before goal runs | C# | green |
| Channels registered after FreezeFoundational don't appear in foundational set | C# | green |
| End-to-end console-style: register six Memory channels, run goal, verify writes arrive | C# (Cut 1) | green |

### Stage 7 — App.Services + Service

| Behaviour | Layer | Sense |
|-----------|-------|-------|
| app.Services.New(parent) creates Service, adds to collection | C# | green |
| Service.Channels is empty on construction | C# | green |
| Service.Identity navigates to App.System.Identity | C# | green |
| Service.Parent set to whoever was passed | C# | green |
| `await using` disposes Service: removes from collection, disposes Channels | C# | green |
| Two parallel Services don't collide on channel names (each has own Channels) | C# | green |
| Actor.ValidValues drops to ["user", "system"] | C# | green |
| Actor.@this no longer has EscalationLevel property | C# (compile) | green |

### Stage 8 — Channel events

| Behaviour | Layer | Sense |
|-----------|-------|-------|
| EventType has new values: BeforeWrite, AfterWrite, BeforeRead, AfterRead, OnAsk | C# | green |
| EventBinding accepts ChannelName filter | C# | green |
| EventContext exposes Channel, Data, Ask properties | C# | green |
| Channel.@this exposes Events property | C# | green |
| BeforeWrite handler receives correct Channel and Data via EventContext | C# / goal | green |
| BeforeWrite handler throwing aborts the write; AfterWrite does NOT fire | C# | negative |
| AfterWrite fires when WriteCore succeeds | C# | green |
| AfterWrite fires when WriteCore throws | C# | green |
| AfterWrite handler throwing is suppressed; original outcome stands | C# | negative-handled |
| Recursion: BeforeWrite handler that writes to same channel doesn't loop | C# | negative-handled |
| Multiple bindings fire in registration order | C# | green |
| First throwing binding stops subsequent bindings | C# | negative-handled |
| OnAsk on Session channel fires post-answer | C# | green |
| OnAsk on Message channel fires pre-serialise | C# | green |
| Bindings match channels-of-the-same-name across User and Service | C# | green |
| Channel events do NOT trigger goal/step/action bindings | C# | green |
| End-to-end: BeforeWrite abort + AfterWrite metric on two writes | C# (Cut 3) | green |
| Builder catalog includes new EventType values + channelName filter | C# (builder test) | green |
| `- add before write on "X" channel, call Y` builds correctly | goal | green |

### Stage 9 — channel.migrate

| Behaviour | Layer | Sense |
|-----------|-------|-------|
| channel.migrate on a Session channel returns a MigrationEnvelope | goal | green |
| channel.migrate on a Message channel returns Data.Error (NotMigratable) | goal | negative |
| MigrationEnvelope contains channel name, role, direction, config | C# | green |
| MigrationEnvelope is signed by source's System identity | C# | green |
| MigrationEnvelope signature is verifiable | C# | green |
| Channel.Stream backed by Console stream returns NotMigratable when migrated | C# | negative |
| Channel.Stream backed by Memory stream produces a complete envelope | C# | green |
| Channel.Goal envelope includes goal name + Variables snapshot | C# | green |
| Channel.@this.FromMigration is present but throws NotImplemented (receive side deferred) | C# | green |

## Failure matrix

Consolidated negative paths. Each row is a way the system *should* fail; the test asserts the failure is hard, typed, and at the right layer.

| Failure mode | Detected by | Error type | Layer |
|--------------|-------------|------------|-------|
| Write to channel name not registered | Channels.Resolve | ChannelNotFound | C# / goal |
| Read from channel name not registered | Channels.Resolve | ChannelNotFound | C# / goal |
| Channel name registered but write fails (underlying Stream broken) | Channel.Stream.WriteCore | WriteError | C# |
| Read from output-only channel | Channel.@this (CanRead check) | ChannelWriteOnly | C# |
| Write to input-only channel | Channel.@this (CanWrite check) | ChannelReadOnly | C# |
| App.Run with actor missing Output role | App invariant check | MissingRequiredChannelAtBoot | C# |
| App.Run with actor missing Error role | App invariant check | MissingRequiredChannelAtBoot | C# |
| App.Run with actor missing Input role | App invariant check | MissingRequiredChannelAtBoot | C# |
| channel.remove on a standard role-channel | Channels.Remove | ChannelInvariantViolation | goal |
| channel.add on a duplicate name | Channels.Register | DuplicateChannelName | goal |
| Ask times out (no answer within Channel.Timeout) | Channel.Stream.Ask | AskTimeout | C# |
| BeforeWrite handler throws | Events.Before.Run | (handler's typed error) | C# |
| Goal channel registered after FreezeFoundational tries to use foundational | (allowed; binding is dynamic) | n/a — works | n/a |
| TimeSpan JsonConverter rejects malformed string | JsonConverter | JsonException | C# |
| channel.migrate on non-Session channel | channel.migrate handler | NotMigratable | goal |
| channel.migrate on console-stream-backed channel | Channel.Stream.Migrate | NotMigratable | C# |
| Receive-side migration | Channel.@this.FromMigration | NotImplemented | C# |
| Service.Channels write to unregistered name | Channels.Resolve | ChannelNotFound | C# |

## New surfaces this branch introduces

### Interfaces and types (NEW)

- `App.Channels.Channel.@this` — abstract base. Was concrete Stream-wrapper; refactor shifts behaviour to subtypes.
- `App.Channels.Channel.Session.@this` — abstract.
- `App.Channels.Channel.Message.@this` — abstract.
- `App.Channels.Channel.Stream.@this` — concrete, sealed. Wraps `System.IO.Stream`.
- `App.Channels.Channel.Goal.@this` — concrete, sealed. Wraps a Goal reference with foundational-set capture.
- `App.Channels.Channel.Role` (enum) — `Output / Error / Input`.
- `App.Services.@this` — flat collection on App.
- `App.Services.Service.@this` — single Service.
- `App.Services.Service.Migration.MigrationEnvelope` — signed migration payload (Stage 9).

### New methods on existing types

- `App.@this.Services` — property. Ctor initialises.
- `App.@this.Serializers` — promoted from `App.Channels.Serializers`.
- `App.@this.FreezeFoundational()` — called by Run.
- `App.@this.Run()` — adds invariant check + freeze before goal execution.
- `Actor.@this.FoundationalChannels` — readonly property; populated by Freeze.
- `App.Channels.@this.Resolve(string?)` — returns channel by name or default Output.
- `App.Channels.@this.Snapshot()` — returns immutable shallow copy for foundational use.
- `App.Channels.@this.Channel.@this.Events` — events collection (Stage 8).
- `App.Channels.@this.Channel.@this.Migrate(target)` — Stage 9.
- `Actor.@this.ValidValues` — drops to ["user", "system"].
- `Actor.@this.EscalationLevel` — REMOVED.

### New PLang actions

- `channel.set` (`App/modules/channel/set.cs`)
- `channel.add` (`App/modules/channel/add.cs`)
- `channel.remove` (`App/modules/channel/remove.cs`)
- `channel.migrate` (`App/modules/channel/migrate.cs`)

### Existing surfaces this branch touches by reference

- `App/modules/IChannel.cs` — declaration changes from `Channels` to `Channel`.
- `App/modules/output/write.cs` — body simplifies to `Channel.WriteAsync(Data)`.
- `App/Modules/this.cs` — Describe path for channel-aware catalog (per-actor channel inventory passed to LLM).
- `PLang.Generators/Emission/Action/this.cs` — emission for IChannel changes from registry-injection to single-channel resolution.
- `App.Events.EventType` enum — five new values.
- `App.Events.Lifecycle.Bindings.Binding.@this` — `ChannelName` filter.
- `App.Actor.Context.@this.GetEventBindings` — extends owner-switch for Channel.@this.
- `App/modules/events.add` (or wherever existing event registration lives) — supports the new EventType + ChannelName filter via builder catalog.
- `PlangConsole/Program.cs` — registers the six channels at boot.
- `App.@this` — drops `Channels` property; adds `Services`, `Serializers`.
- `App.Channels.@this` — pure registry; ctor stops opening console; adds `Resolve`, `Snapshot`.

### Registrations (MIME types, etc.)

- A globally-registered `JsonConverter` for `TimeSpan` ↔ ISO 8601 string. Used everywhere TimeSpan appears in actions.
