# test-designer — runtime2-channels

## Version
v1

## What this is

Channels: a refactor of the placeholder console-stream wiring inside the runtime
into a proper per-actor channel architecture. New types: `Channel.@this` base,
`Session`/`Message` abstracts, `Stream` and `Goal` concretes; `App.Services`
flat collection with `Service` per outbound call; source-gen-resolved single
`Channel` slot on `IChannel` actions; three module actions
(`channel.set`/`add`/`remove`); channel events (`BeforeWrite`/`AfterWrite`/
`BeforeRead`/`AfterRead`/`OnAsk`); migration API stub.

Architect's plan + 9 stage briefs + test strategy + coverage matrix
landed at `.bot/runtime2-channels/architect/`. My job: translate that into
test stubs that pin behaviour for the coder.

## What was done

**~92 C# TUnit stubs** in `PLang.Tests/App/ChannelsTests/` (folder name uses
the `*Tests` suffix to avoid shadowing the global `Channel` /
`EngineChannels` aliases per `/PLang.Tests/CLAUDE.md`):

- `Stage1_ChannelBaseTests.cs` (8) — base properties, defaults, Role enum, abstracts.
- `Stage1_TimeSpanJsonConverterTests.cs` (3) — ISO 8601 round-trip + reject.
- `Stage2_StreamChannelTests.cs` (11) — Stream concrete: WriteCore/ReadCore/Ask, factories, ownsStream semantics.
- `Stage3_GoalChannelTests.cs` (9) — Goal concrete + recursion rule + foundational set + fan-out.
- `Stage4_ChannelResolutionTests.cs` (8) — Resolve + IChannel slot + Write.Run.
- `Stage4_BuilderCatalogTests.cs` (3) — catalog inventory + intent-not-pattern.
- `Stage5_ChannelActionsBuilderCatalogTests.cs` (1) — three actions covered.
- `Stage6_EntryPointWiringTests.cs` (10) — App ctor no longer opens console, invariant checks, FreezeFoundational.
- `Stage7_AppServicesTests.cs` (8) — Services collection, Service lifecycle, Actor cleanup, EscalationLevel removal.
- `Stage8_ChannelEventsTests.cs` (16) — types, EventContext, firing semantics, recursion guard, no-cross-fire.
- `Stage9_ChannelMigrateTests.cs` (8) — envelope + signing + NotMigratable + FromMigration stub.
- `Integration/IntegrationCutsTests.cs` (3) — Cut 1 (boot→stdout), Cut 2 (fan-out), Cut 3 (events abort+audit).

**14 PLang `.goal` stubs** under `Tests/Channels/<Behaviour>/Start.test.goal`,
covering the developer-facing `channel.*` action surface, channel-name
intent mapping by the builder, the `events` module's channel filter, and
`channel.migrate` happy + error paths.

**`PLang.Tests` builds clean** (0 errors, only pre-existing warnings).

**Behavioural decisions made on Ingi's behalf** (10 items in
`v1/test-plan.md` "Behavioural decisions taken on behalf of the language"
section). The notable ones:

- `Channels.Resolve(null)` and `Resolve("")` both return the Output role channel.
- `channel.add` duplicate name → typed `DuplicateChannelName`.
- `channel.remove` of role channel → typed `ChannelInvariantViolation`.
- `Channel.Stream` `ownsStream` defaults: `false` for entry-point-supplied
  console streams; `true` for Memory factory.
- `AfterWrite` after a Before-abort: I planted Cut 3's strategy-doc reading
  (AfterWrite always fires) and flagged the conflicting Stage 8 stub for
  coder to flip if needed.

## Files modified / created

Tracked in the test-designer session of `.bot/runtime2-channels/report.json`.

## Code example

C# stub style — all bodies identical, behaviour pinned in the name + comment:

```csharp
[Test]
public async Task ChannelsResolve_UnknownName_ThrowsChannelNotFound()
{
    // Resolve("dbg") when "dbg" never registered → typed ChannelNotFound error.
    // Failure matrix: ChannelNotFound at C# / goal layers.
    await Task.CompletedTask;
    Assert.Fail("Not implemented");
}
```

PLang `.goal` stub style — spec on the `/` lines, body is one throw:

```
Start
/ channel.add with a name already registered returns Data.Error of type
/ DuplicateChannelName. Use channel.set to replace.
/ - add channel "logger" call Logger
/ - add channel "logger" call Logger2     # this step should error
/ Verify: second step's Data.Error.Type == "DuplicateChannelName".
- throw "not implemented"
```

## Next step

Hand to coder. Stage briefs are in `.bot/runtime2-channels/architect/stage-N-*.md`.
Recommended order = stage order (1→9) since the dependencies match the matrix.
Integration cuts come last — they only pass once stages 1+2+4+6 (cut 1),
3+5+6 (cut 2), and 1+2+5+8 (cut 3) are green.
