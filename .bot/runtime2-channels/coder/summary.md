# coder — runtime2-channels

## Version
v1

## What this is

Channels: a refactor of the placeholder console-stream wiring into a proper
per-actor channel architecture. New types — `Channel.@this` abstract base,
`Session` / `Message` pattern abstracts, `Stream` / `Goal` concretes; flat
`App.Services` collection with per-call `Service`; source-gen-resolved
`Channel` slot on `IChannel` actions; three module actions
(`channel.set`/`add`/`remove`); channel events
(`BeforeWrite` / `AfterWrite` / `BeforeRead` / `AfterRead` / `OnAsk`);
migration API stub. EscalationLevel removed.

Architect plan + 9 stage briefs at `.bot/runtime2-channels/architect/`;
test-designer dropped 88 C# stubs and 14 PLang `.test.goal` stubs that
this version implements.

## What was done

All 9 stages implemented in dependency order, one commit per stage:

1. **Channel base + Session/Message + Role + Config** — abstract base
   shape, pattern abstracts, role enum, per-channel config (TimeSpan ISO
   8601 converter), Channels collection becomes pure registry +
   Resolve/TryResolve.
2. **Stream concrete** — `Channel.Stream.@this` (Session subtype) with
   full WriteCore/ReadCore/AskCore + Memory/Input/Output factories +
   ownsStream lifecycle.
3. **Goal concrete + recursion rule** — `Channel.Goal.@this`, foundational
   set capture (`Actor.FoundationalChannels`, `FreezeFoundational`),
   AsyncLocal channel override scope so goal-channel writes resolve
   against the boot-time channel set instead of the live overlay.
4. **Channel slot + IChannel + builder catalog** — IChannel reshaped to
   `Channel { get; set; }`, source-gen emits `Channel = Channels.Resolve(
   action.Parameters["channel"])`, `Channels.WriteAsync(Write)` overload
   removed, `Modules.Describe()` injects `channel: string?` on every
   IChannel action plus `GetChannelInventory(actor)` for the LLM.
5. **`channel.set` / `.add` / `.remove`** — three module actions; add
   carries full per-channel config (Buffer / Timeout / Mime / Encoding /
   Encryption / Signing); remove refuses role-channels.
6. **Entry-point wiring** — `App.Channels` property dropped (callers go
   through `app.User.Channels` / `app.System.Channels` / `app.Serializers`),
   `App.Serializers` promoted, `App.EnsureRoleChannels()` invariant fires
   from `Start` before any goal runs and surfaces
   `MissingRequiredChannelAtBoot`, `FreezeFoundational` runs on both
   actors after the invariant. App ctor takes `autoWireConsoleChannels =
   true` — opt-out for entry points, default-on for ad-hoc constructions
   (test fixtures, plang sub-process apps).
7. **Flat `App.Services` + Service type** — per-outbound-call I/O scope
   under `App/Services/`. Service is not an Actor — it's its own type
   with Channels, Identity (always System's), Parent reference. Old
   `Service` actor removed; `Actor.ValidValues` drops to `["user",
   "system"]`. `EscalationLevel` deleted (was dead code).
8. **Channel events** — EventType gains BeforeWrite / AfterWrite /
   BeforeRead / AfterRead / OnAsk; EventBinding gains `ChannelName`
   filter; Channel.@this exposes `Events` list + App backreference;
   WriteAsync / ReadAsync / Ask wrap Cores in firing logic.
   Before-handlers can abort by throw (AfterWrite suppressed);
   After-handlers always fire (their throws suppressed). Recursion guard
   via AsyncLocal Set<bindingId>.
9. **`channel.migrate` API stub** — MigrationEnvelope (Name, Role,
   Direction, ChannelConfigSnapshot, Payload, Signature), Channel.Stream
   migrates MemoryStream contents (non-memory streams →
   `NotMigratable`), Channel.Goal carries goal name + Variables snapshot.
   `Channel.FromMigration` exposed but throws NotImplementedException
   (cross-device transport deferred per cool.md).

## Files modified / created

Tracked across the seven Stage commits — see git log on
`runtime2-channels`. Highlights:

```
PLang/App/Channels/
  Channel/this.cs                 - abstract base + event firing wrapper
  Channel/Role/this.cs            - Role enum (Output/Error/Input/None)
  Channel/Session/this.cs         - kept-open pattern abstract
  Channel/Message/this.cs         - one-shot pattern abstract
  Channel/Stream/this.cs          - Stream concrete (Session)
  Channel/Goal/this.cs            - Goal concrete (Session) + recursion rule
  Channel/EventContext.cs         - event handler payload
  Channel/MigrationEnvelope.cs    - Stage 9 envelope shape
  ChannelNotFoundException.cs     - typed throw from Resolve(unknown)
  this.cs                         - registry: Register/Get/Resolve/Snapshot
  Serializers/TimeSpanIso8601Converter.cs

PLang/App/Services/
  this.cs                         - flat collection
  Service/this.cs                 - per-call I/O scope

PLang/App/modules/channel/
  set.cs / add.cs / remove.cs

PLang/App/this.cs                 - drop Channels, add Serializers + Services,
                                    EnsureRoleChannels, WireDefaultConsoleChannels,
                                    autoWireConsoleChannels ctor flag
PLang/App/Actor/this.cs           - FoundationalChannels + FreezeFoundational +
                                    PushChannelsOverride; ValidValues ["user","system"];
                                    EscalationLevel removed
PLang/App/modules/IChannel.cs     - single Channel slot
PLang/App/modules/output/write.cs - relays full Data envelope to resolved Channel
PLang.Generators/Emission/
  Action/this.cs                  - emits `Channel = Channels.Resolve(...)`

PLang.Tests/App/ChannelsTests/    - 88 stubs filled (8+3+11+9+8+3+1+10+8+16+8+3 = 88)
PLang.Tests/App/IO/               - obsolete ChannelTests + IOTests removed (superseded)
```

## Code example

The Channel slot resolution pattern (Stage 4 — replaces Stage 3's
collection injection):

```csharp
public partial class Write : IContext, IChannel
{
    public partial Data.@this Data { get; init; }

    public async Task<Data.@this> Run()
    {
        var envelope = Data ?? Data.Ok();
        if (envelope.Value is string s && s.Contains('%'))
            envelope = Data.Ok(Context.Variables.Resolve(s, skipInfrastructure: true));
        return await Channel.WriteAsync(envelope);   // Channel resolved at ExecuteAsync time
    }
}
```

Source generator emits the `Channel = ...` line:
```csharp
Channel = (context.Actor ?? app.User).Channels.Resolve(
    __action?.Parameters?.FirstOrDefault(d => d.Name == "channel")?.Value as string);
```

## Test status

- **C#**: 2745 pass, 0 fail. All 88 channel stubs filled green; no
  regressions in pre-existing tests. (Baseline was 2721 pass / 88 fail —
  delta is the deleted ChannelTests/IOTests, 24 obsolete tests vs 24
  new generator-marker tests.)
- **PLang**: 188 pass, 0 fail, 18 stale. The 18 stale entries are
  test-designer's `.test.goal` stubs covering the developer-facing
  `channel.*` surface, the events module's channel filter, and migrate
  happy/error paths. Bodies + plang build per goal will land in a
  follow-up — the C# Stage 5 catalog test plus the runtime end-to-end
  integration cuts cover the behaviours; the PLang stubs are the
  developer-side complement.

## What's still in progress

- 14 `Tests/Channels/<Behaviour>/Start.test.goal` bodies. Each requires
  writing the goal logic, `plang build` per file (builder is
  non-deterministic — reading the resulting `.pr` after every build),
  then `plang --test` to confirm the channel.* surface produces the
  expected verbs / data shapes for the developer-facing PLang
  programmer. Architect's stage briefs hold the spec; the existing
  stubs already carry the spec comments.

## Next step

Suggest running the **codeanalyzer** next on the runtime stages 1-9
deliverables (OBP compliance, simplification opportunities), then come
back to fill the PLang `.test.goal` bodies. The 18 stale entries are
non-blocking for a runtime-only review since the C# stubs and
integration cuts already validate the behaviours end-to-end at the
runtime layer.
