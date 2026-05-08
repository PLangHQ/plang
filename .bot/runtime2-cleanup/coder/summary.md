# coder — runtime2-cleanup

## Version

v1 — Stage 1 (`serializers-single-home`).

## What this is

First commit on `runtime2-cleanup` after the architect carved Stage 1.
Establishes per-actor `Channels.@this.Serializers` as the single home for the
serializer registry by deleting the two surfaces that bypassed it:

- `App.@this.Serializers` — the App-root shortcut that bypassed the actor entirely.
- `Channel.Stream.@this._serializers` — a third copy lazily allocated per stream
  that wasn't shared with its parent Channels.

Adds a `Channels` back-ref on `Channel.@this` (alongside the existing `App`
back-ref) so Stream's `WriteCore` can navigate to its parent Channels'
Serializers. The `Channels` property is set in `Channels.Register(channel)`,
the single registration path.

## What was done

### Production (5 files modified)

- `PLang/App/this.cs` — deleted the `Serializers { get; } = new Serializers()` property.
- `PLang/App/Channels/Channel/this.cs` — added `public global::App.Channels.@this? Channels { get; internal set; }` alongside `App`.
- `PLang/App/Channels/Channel/Stream/this.cs` — removed `_serializers` field and the lazy `Serializers` property. `WriteCore` now calls `Channels!.Serializers.SerializeAsync(...)`.
- `PLang/App/Channels/this.cs`:
  - `Register(channel)` now sets `channel.Channels = this`.
  - The two internal sites in `WriteAsync(channelName, ...)` (line 176) and `ReadChannelAsync<T>` (line 204) — `sc.Serializers.X` → `Serializers.X` (this collection's own).
  - Stale "Stage 6 promotes…" doc-comment replaced with a per-actor description.
- Caller sweep:
  - `PLang/App/Goals/this.cs:320, 325` → `app.System.Channels.Serializers`
  - `PLang/App/Goals/Setup/this.cs:56` → `app.System.Channels.Serializers`
  - `PLang/App/modules/file/providers/DefaultFileProvider.cs:99` → `action.Context.Actor.Channels.Serializers`
  - `PLang/App/Actor/Context/this.cs:172` (`!serializers` DynamicData) → `() => Actor!.Channels.Serializers`

### Tests (6 production-test files modified)

`app.Serializers` / `engine.Serializers` → per-actor `*.User.Channels.Serializers` in:
- `PLang.Tests/App/Serializers/JsonSerializerRoundTripTests.cs` (4 sites)
- `PLang.Tests/App/Serializers/MimeRegistrationTests.cs` (5 sites)
- `PLang.Tests/App/Serializers/PlangDataSerializerRoundTripTests.cs` (5 sites)
- `PLang.Tests/App/CallbackTests/FailureMatrixTests.cs` (1 site)
- `PLang.Tests/App/Core/EngineTests.cs` (3 sites)
- `PLang.Tests/App/ChannelsTests/Stage6_EntryPointWiringTests.cs` — renamed `AppThis_SerializersExists_AtAppLevel` → `AppThis_SerializersExists_PerActor` and now asserts both `app.User.Channels.Serializers` and `app.System.Channels.Serializers` are non-null.

### Tests (3 unit-level files updated for new contract)

The brief flagged the boot-ordering risk: `Channels` on a Stream is null until
`Channels.Register(channel)` runs. Existing unit tests that constructed a
StreamChannel directly and then called `WriteCore`/`WriteAsync` (which now
dereferences `Channels!.Serializers`) hit NRE — exactly the case to update.
Each failing test now constructs a small App with `autoWireConsoleChannels:
false` and registers the channel via `app.User.Channels.Register(...)` before
exercising it:

- `Stage1_ChannelBaseTests.ChannelBase_Mime_DefaultDrivesSerializerSelection` — both Mime variants get registered.
- `Stage2_StreamChannelTests.StreamChannel_WriteCore_WritesDataViaSerializer`.
- `Stage2_StreamChannelTests.StreamChannel_WriteCore_FailsWithWriteError_OnUnderlyingStreamThrow`.
- `Stage8_ChannelEventsTests.BeforeWriteHandler_ReceivesCorrectData`.
- `Stage8_ChannelEventsTests.AfterWriteHandler_FiresWhenWriteCoreSucceeds`.
- `Stage8_ChannelEventsTests.AfterWriteHandler_ThrowingIsSuppressed_OriginalOutcomeStands`.
- `Stage8_ChannelEventsTests.MultipleBindings_FireInRegistrationOrder`.

The Stage8 tests where the BeforeWrite handler aborts (so `WriteCore` is never
reached) were left untouched — they don't need a registered Channels.

### Verification

- `grep -rn "app\.Serializers\b\|App\.Serializers\b" PLang/ --include='*.cs'` → 0
- `grep -rn "_serializers" PLang/App/Channels/ --include='*.cs'` → 0
- `grep -rn "sc\.Serializers" PLang/ --include='*.cs'` → 0
- `dotnet build PlangConsole` clean (0 errors).
- `dotnet run --project PLang.Tests` → **2755/2755 pass** (matches baseline).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` → **199/199 pass** (matches baseline).

## Code example — the realignment

Before:

```csharp
// App.this.cs
public Serializers Serializers { get; } = new Serializers();

// Channel/Stream/this.cs
private Serializers.@this? _serializers;
public Serializers.@this Serializers
{
    get => _serializers ??= new Serializers.@this();
    init => _serializers = value;
}

// WriteCore
await Serializers.SerializeAsync(...);
```

After:

```csharp
// App.this.cs — property gone.

// Channel/this.cs — Channels back-ref added (alongside App).
public global::App.Channels.@this? Channels { get; internal set; }

// Channels/this.cs Register
public void Register(Channel.@this channel)
{
    channel.App = _app;
    channel.Channels = this;        // NEW: single navigation point
    if (channel.Actor == null) channel.Actor = Actor;
    _channels[channel.Name] = channel;
}

// Channel/Stream/this.cs WriteCore
await Channels!.Serializers.SerializeAsync(...);
```

External callers — same destination, but addressed *through the actor* now:

```csharp
// before:  app.Serializers.Deserialize<...>(...)
// after:   app.System.Channels.Serializers.Deserialize<...>(...)
```

## Stage-1 notes for the architect / next-stage briefs

Nothing surprised me in the touched files. The brief's caller-sweep list was
accurate (5 production sites). The only thing the brief didn't enumerate was
the test-side sweep, which I handled inline (kept on the per-actor model
chosen for production: `app.User.Channels.Serializers` for tests that already
write through User; the EngineTests' "registry exists" assertion remains
valid against the per-actor instance).

The boot-ordering risk played out exactly as the brief predicted in the unit
tests — none of the affected production paths break (everything goes through
`Channels.Register` before write). All seven test failures came from
construct-then-write-without-register, which the brief explicitly told me to
verify and update.

For Stage 20 (`channel-app-backref-drop`): I noticed `Channel.@this.App` is
still read at `Channel.this.cs:186, 188, 241` (event-binding match against
app-level events, and the diagnostic write). All three could navigate via
`Channels?.App` once the back-ref was always set; nothing on the branch
depends on `App` being set independently of `Channels`. Looks teed up for
when that stage is carved.
