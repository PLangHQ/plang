# Channels — test plan v1

Translation of architect `v1/plan/test-coverage.md` + `test-strategy.md` into
~93 stub tests across 9 stages plus 3 integration cuts.

## Layout

**C#** — `PLang.Tests/App/ChannelsTests/` (new). Folder uses `*Tests` suffix
to avoid shadowing the global `Channel` / `EngineChannels` alias
(see `/PLang.Tests/CLAUDE.md`).

**PLang `.goal`** — `Tests/Channels/<Behaviour>/Start.test.goal`, one folder
per behaviour, matching `Tests/Callback/...` style. Body is `- throw "not implemented"`.

## Convention used in stubs

- C# bodies: `await Task.CompletedTask; Assert.Fail("Not implemented");`
- No references to not-yet-existing types in test bodies — keeps the project
  compilable. The behaviour is captured in the test name + the comment above.
- PLang bodies: `- throw "not implemented"`. Spec lives in the `/` comments
  above the throw.

## Files

### C#

| File | Stage | Count |
|------|-------|-------|
| `Stage1_ChannelBaseTests.cs` | 1 — base + Role + Config defaults | 8 |
| `Stage1_TimeSpanJsonConverterTests.cs` | 1 — ISO 8601 round-trip + reject | 3 |
| `Stage2_StreamChannelTests.cs` | 2 — Stream concrete | 11 |
| `Stage3_GoalChannelTests.cs` | 3 — Goal concrete + recursion + fan-out | 9 |
| `Stage4_ChannelResolutionTests.cs` | 4 — Resolve + IChannel + Write.Run | 8 |
| `Stage4_BuilderCatalogTests.cs` | 4 — catalog inventory + intent mapping | 3 |
| `Stage5_ChannelActionsBuilderCatalogTests.cs` | 5 — builder coverage of three actions | 1 |
| `Stage6_EntryPointWiringTests.cs` | 6 — invariants + freeze + drop-app-channels | 10 |
| `Stage7_AppServicesTests.cs` | 7 — Services collection + Service lifecycle + Actor cleanup | 8 |
| `Stage8_ChannelEventsTests.cs` | 8 — events: types, payload, firing, recursion guard | 16 |
| `Stage9_ChannelMigrateTests.cs` | 9 — envelope + signing + NotMigratable | 7 |
| `Integration/IntegrationCutsTests.cs` | cuts 1-3 | 3 |

### PLang `.goal`

| Folder | Stage | Spec |
|--------|-------|------|
| `Tests/Channels/Set/ReplaceOutputWithGoal/` | 5 | `channel.set` replaces role channel with Goal channel |
| `Tests/Channels/Set/ExplicitActor/` | 5 | `set system output channel ...` targets System actor |
| `Tests/Channels/Set/ContextActor/` | 5 | `set output channel ...` uses `Context.Actor` |
| `Tests/Channels/Add/Basic/` | 5 | `channel.add` registers new named Goal channel |
| `Tests/Channels/Add/WithConfig/` | 5 | `channel.add` honours `buffer:`, `timeout:` config |
| `Tests/Channels/Add/DuplicateName/` | 5 | duplicate `channel.add` returns Data.Error |
| `Tests/Channels/Remove/Custom/` | 5 | `channel.remove` unregisters custom channel |
| `Tests/Channels/Remove/RoleChannelRefused/` | 5 | `channel.remove` on role channel returns ChannelInvariantViolation |
| `Tests/Channels/WriteToCustomChannel/` | 4 | `- write 'hi' to logger` builds a Channel="logger" slot and writes there |
| `Tests/Channels/WriteToUnknownChannel/` | 4 | write to unregistered channel surfaces ChannelNotFound on error channel |
| `Tests/Channels/Events/AddBeforeWrite/` | 8 | `- add before write on "X" channel, call Y` builds correctly |
| `Tests/Channels/Events/AddOnAsk/` | 8 | `- add on ask on "input" channel, call Y` builds correctly |
| `Tests/Channels/Migrate/SessionOk/` | 9 | `channel.migrate` on Session channel returns envelope |
| `Tests/Channels/Migrate/MessageError/` | 9 | `channel.migrate` on Message channel returns Data.Error |

## Behavioural decisions taken on behalf of the language

The architect's matrix has a few rows whose precise contract isn't pinned. I made
calls and dropped them as comments inline so coder can adopt or correct.

1. **`Channels.Resolve(null)` returns the Output role channel.** Matches
   plan.md L177 — "If no name was emitted, falls back to the actor's `Output`
   role channel." Treated as a hard contract: even with multiple Output-role
   channels, Resolve(null) returns the role-canonical one (the channel
   registered under the literal name `"output"`). Other Output-role channels
   are reachable only by name.

2. **`Channels.Resolve("")` is treated identical to `Resolve(null)`.** Empty-
   string slot must not produce ChannelNotFound — it's just "no name emitted."

3. **`channel.add` duplicate name → `Data.Error` of error type
   `DuplicateChannelName`** (matches Failure matrix). Set must be used for
   replacement.

4. **`channel.remove` of a role channel (`output`/`error`/`input`) returns
   `Data.Error` of error type `ChannelInvariantViolation`** — the registry
   refuses, the runtime invariant survives. Removing a custom channel
   succeeds even mid-goal.

5. **`Channel.Stream` `ownsStream` flag.** Architect lists `ownsStream=true`
   and `=false` semantics but doesn't pin the default. I defaulted tests
   to assume `ownsStream=false` for entry-point-supplied console streams
   (Console.OpenStandard* should NOT be closed by Channel disposal — process
   owns them). Memory factory test expects `ownsStream=true` (the channel
   created the stream). Coder: tests pin both — pick whatever the impl does
   and update one expectation if needed.

6. **`Channel.@this.FromMigration`** stub throws `NotImplementedException`
   per the matrix. Test only asserts that calling it raises that type.

7. **`AfterWrite` handler error suppression** — test asserts that an
   AfterWrite handler that throws does NOT change the original write's
   outcome. Coder may decide whether to surface the suppressed error via a
   side channel (logging) or fully swallow; the test only pins the visible
   contract on the write Result.

8. **Channel events do NOT trigger goal/step/action bindings** — covered by
   a dedicated test. The runtime fires Channel events via the same
   `_activeEventBindings` recursion guard but Channel binding owners are
   `Channel.@this`, never `Goal`/`Step`/`Action`. Test asserts a goal-bound
   `BeforeRun` handler is NOT fired by a `Channel.WriteAsync` invocation.

9. **`Service.Identity`** test asserts `service.Identity` is reference-equal
   to `app.System.Identity`. If the impl chooses to copy/clone, change to
   value-equal — the contract is "same identity," not "same instance."

10. **PLang test goal structure.** Each `Test` goal has the spec on lines
    of `/` comments above the `- throw "not implemented"`. The comment IS
    the test contract — coder's implementation must satisfy what's described.

## Open items for coder

None blocking — all decisions documented above. If any of the assumptions
above contradict implementation choices the coder makes, the matching test
expectation is the easier thing to flip; flag it in `coder/summary.md` so
docs picks up the contract change.
