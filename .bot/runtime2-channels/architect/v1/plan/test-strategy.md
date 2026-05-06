# Channels — test strategy

## Scope

The integration cuts below are the contract for end-to-end behaviour; per-topic and negative-path tests sit beneath them in `test-coverage.md`.

## Test layer mapping

Three layers; the rule is: **C# TUnit pins internal `@this` behaviour; PLang `.goal` tests pin developer-facing surfaces; integration cuts pin end-to-end flows that span both.**

| Layer | What it covers |
|-------|----------------|
| **C# TUnit** (in `PLang.Tests/`) | `Channel.@this` base contract, Session/Message abstracts, Stream/Goal concretes, `App.Services` collection, `Service` lifecycle, source-gen-emitted IChannel resolution, EventContext payload, recursion guards. |
| **PLang `.goal`** (in `Tests/Channels/`) | `channel.set`, `channel.add`, `channel.remove`, `channel.migrate` actions reaching their handlers and producing observable effects (writes that arrive, channels that disappear, errors that surface). Event bindings on channels firing in PLang's surface syntax. |
| **Integration cuts** (mostly C#) | The full path from "entry point registers Stream channels" through "action invokes Channel.WriteAsync" into "byte arrives on stream" with all the source-gen + serialiser + recursion-guard wiring exercised together. |

Per-behaviour assignment is in the matrix.

## Integration cuts

Three cuts. Each is a complete behaviour test that proves multiple stages cooperate correctly.

### Cut 1 — Console boot through write-out reaches stdout

Setup:
- New App with no programmatic stream registration in the runtime ctor.
- Test entry point registers all six channels (User × 3, System × 3) using Memory-backed Stream channels (so the test can inspect what arrived).
- App.Run() called with a goal containing `- write out "hello"`.

Capture:
- Bytes that landed on `app.User.Channels.Resolve(null)` (i.e., the Output role channel's MemoryStream).

Resume / verify:
- The MemoryStream contains "hello" (or the serialised representation per the channel's Mime).
- No bytes on Error or Input channels.
- App.Run completed without throwing.

Proves: Stage 1 (base contract), Stage 2 (Stream concrete), Stage 4 (Channel slot resolution + Write.Run via single resolved Channel), Stage 6 (entry-point wiring + foundational freeze + invariant check) all work together. The console end-to-end is the canonical happy path.

### Cut 2 — Goal channel fan-out hits two destinations

Setup:
- App with Memory-backed `output` Stream channel (the foundational stdout-equivalent).
- A goal `Logger` defined: `- write %!data% to file.txt; - write out %!data%` — where the file-write goes to a test-controllable file system and the second `write out` exercises the recursion rule.
- Use `channel.set` to replace `output` with `Channel.Goal(goal: Logger)`.
- `App.Run` a goal that does `- write out "hi"`.

Capture:
- File content (audit destination).
- The foundational Output Stream channel's MemoryStream content.

Resume / verify:
- File contains "hi".
- Foundational MemoryStream contains "hi".
- Logger goal ran exactly once (no recursion).

Proves: Stage 3 (Goal concrete + recursion rule + foundational set), Stage 5 (channel.set), Stage 6 (foundational freeze used by the recursion rule). This is the fan-out pattern documented in plan.md proven end-to-end.

### Cut 3 — Channel events fire around a write with abort + audit

Setup:
- Memory-backed `audit.external` channel (custom, `channel.add`).
- An `add before write on "audit.external" channel, call ApprovalGoal` event binding registered. ApprovalGoal inspects `%!event.data%`, returns Data.Error if data contains "REJECT".
- An `add after write on "audit.external" channel, call MetricsGoal` binding. MetricsGoal increments a counter via `channel.add`-registered `metrics` channel.
- App.Run a goal that does two writes: `- write "ok-payload" to audit.external`, then `- write "REJECT-this" to audit.external`.

Capture:
- Bytes on the audit.external MemoryStream.
- Counter on the metrics channel.
- Errors from the second write.

Resume / verify:
- audit.external contains "ok-payload" only (second write aborted by Before-handler).
- Counter incremented twice (After fires for both attempts, including the failed one).
- Second write's result is Data.Error with the abort reason.

Proves: Stage 8 (BeforeWrite abort, AfterWrite always-fires, EventContext payload, channel-name filter, registration order), Stage 5 (channel.add for both channels), Stage 1+2 (base + Stream).

## What's not covered by these cuts

Per-topic tests in `test-coverage.md`:

- Each Channel concrete's per-method round-trips (Stream.WriteCore against a real Stream, Goal.WriteCore actually calling the goal, etc.).
- Negative paths for each action (channel.remove on a role-channel refused; channel.add on duplicate name refused; resolve of unknown channel produces ChannelNotFound).
- Per-surface PLang tests that don't need to span stages (a `.goal` test that just registers a channel and verifies the registry).
- Service lifecycle in isolation (Stage 7) — none of the integration cuts exercise outbound calls because the retrofit is deferred. C# tests cover Service.New / Dispose and Channels-on-Service behaviour.
- Channel.Migrate envelope generation (Stage 9) — API stub, no transport, so just C# tests verifying envelope shape + signature.
- All the failure modes in `test-coverage.md`'s Failure matrix: ChannelNotFound, ChannelInvariantViolation, MissingRequiredChannelAtBoot, AskTimeout, etc.

The cuts cover the *end-to-end happy paths* (and one rich event abort case). The matrix covers everything else beneath.
