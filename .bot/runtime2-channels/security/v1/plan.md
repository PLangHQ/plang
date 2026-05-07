# Security plan — runtime2-channels v1

## Scope

This branch reshapes I/O around per-actor channels. Stages 1-9 introduce a
`Channel.@this` base + `Stream`/`Goal` concretes, an `IChannel` capability
interface that source-gen resolves at action invocation, three module actions
(`channel.set`, `channel.remove`, `channel.migrate`), entry-point wiring of
the three role channels, a flat `App.Services` collection, channel events
(BeforeWrite/AfterWrite/BeforeRead/AfterRead/OnAsk), and a Stage-9 migration
**API stub** (no receive side, no transport).

Bots already passed: codeanalyzer v4 (PASS) and tester v7 (PASS, 2 minor
missing-coverage findings now covered by coder v8 probe tests).

## Threat-model framing

Per `discipline.md`: the trust boundary in PLang is cryptographic signatures
on `Data` envelopes; `.pr` is user-authored and trusted; "the user owns
their software". For this branch the questions are:

1. **Channel registration** (`channel.set`) — does it broaden privilege
   beyond what the calling actor already holds? *No: the goal runs under the
   registering actor's context. `Actor: "system"` is a `.pr` author choice
   gated by the same trust model as any other .pr decision.*

2. **Channel resolution at runtime** (`IChannel` source-gen) — could a wire
   payload pick a channel name and trigger writes? *The channel name comes
   from `action.Parameters["channel"]` which is .pr-authored. Variable
   resolution can substitute user input, but that's the .pr author's choice.*

3. **Channel events** — recursion guard (codeanalyzer v3 closed B1/L1) +
   handler abort semantics. *Per-channel `_active` is now instance-scoped
   AsyncLocal with copy-on-write `Enter`. Coder v8 added probe tests. Wire
   surface is unchanged.*

4. **Wire surfaces introduced or changed** — `Channel.Stream` reads from
   arbitrary streams; `Channel.Goal` migrates carrying `Variables.Snapshot()`;
   `MigrationEnvelope` claims to be "signed". *These are the high-yield
   targets for this pass.*

5. **`[Sensitive]` propagation** — channel write paths route through
   `JsonStreamSerializer` / `PlangDataSerializer` which both apply
   `SensitivePropertyFilter.Strip`. *Confirmed for the wire boundary; gap
   remains for the standing `Variables.Snapshot()` finding (no [Sensitive]
   stripping, plaintext-string user variables also leak).*

## Audit checklist (v1)

- [x] Stage 9 `MigrationEnvelope.Signature`: real signature or stub?
- [x] `Channel.VerifyEnvelope` — what guarantee does `true` give?
- [x] `Channel.Stream.ReadCore` / `ReadAllBytesAsync`: size limit honoured?
- [x] `Channel.Goal.Migrate` payload contents — `[Sensitive]` propagation?
- [x] `MigrationEnvelope.Payload` is `object?` — polymorphic deserialize risk?
- [x] Channel events recursion guard — re-verify codeanalyzer v3 closures.
- [x] `channel.set` can target `system` actor — privilege escalation? *No, .pr is the gate.*
- [x] `Channel.@this.Encoding` lookup — silent fallback to UTF-8 — DoS or mismatch? *Note only, config error.*
- [x] `JsonStreamSerializer` / `PlangDataSerializer` apply `SensitivePropertyFilter.Strip`? *Confirmed both do.*
- [x] Test baseline holds after clean rebuild.

## Verification

- Clean rebuild (per CLAUDE.md stale-binary protocol): `rm -rf bin obj` for
  PlangConsole / PLang / PLang.Tests / PLang.Generators, then
  `dotnet build PlangConsole`. 0 errors, 454 warnings (same as v8 baseline).
- C# TUnit: **2762 / 2762** (matches coder v8).
- PLang: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`
  → 205 pass + 6 `_fixtures_fail` / `_fixtures_sensitive` deliberate fails
  (matches tester baseline).

## Findings

See `summary.md`. One Medium (Stream channel size limit), one Low
(MigrationEnvelope misleading signature), two Notes.
