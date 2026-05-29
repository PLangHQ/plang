# Auditor v1 — builder-ergonomics

**Verdict:** PASS (2 minor, no critical/major)
**Base:** runtime2 (merge `b55d44614`)
**Branch-local commits audited:** channels recursion guard (`827d34e19`),
P4 root-cause-first conversion (`4c37ad582`), builder confidence-per-step +
builder channel routing (`27ad03927`), planner verb rule + Actor-from-step
(`6e210f4c5`), BuildStep rebuild (`9f53f1809`), coder v2 tester fixes
(`b86539c99`).

Data-normalize commits riding into this branch via the runtime2 merge
were already audited under `auditor v1 on data-normalize` (`1fadeb67b`);
this pass focuses on what is **new on `builder-ergonomics` since the
merge**.

## What I cross-traced

### Channel `IsExecuting` guard — sound under cross-file tracing

Three sites form one rule. Verified all three line up:

1. **`PLang/app/channels/channel/goal/this.cs:InvokeGoal`** — arms the
   `AsyncLocal<bool> _executing` before `await Actor.App.RunGoalAsync(...)`,
   restores in `finally`. Save-prev/restore pattern is correct (nested
   self-call via a sibling that re-enters this channel later in the same
   async flow stays armed once, restores once).
2. **`PLang/app/channels/this.cs:Get`** — `if (channel is channel.goal.@this g && g.IsExecuting) return null;`. Only `Get` consults the flag.
3. **End-to-end test** — `Tests/Channels/GoalChannelRecursion/` exercises
   the real `InvokeGoal` path. Verified `.pr` actions match step text
   (start.pr: channel.set + output.write + assert.equals; echoback.pr:
   output.write). No silent false-green like the deleted
   `ConfidenceCheck/UnknownVerb.test.goal` had. Tester mutation-confirmed.

The guard's containment story (cycle A→B→A surfaces `ChannelNotFound`,
sibling channels stay visible, `Task.Run` inherits the flag) holds in
the code as shipped.

### P4 root-cause-first error chaining — semantically right, mutation noted

`Conversion.cs:401-414` change: when `Convert.ChangeType` throws and the
source value is itself an `errors.Error`, the conversion wrapper is
appended to **`sourceErr.ErrorChain`** and `sourceErr` becomes the result.
Verified the user-feedback scenario: `NullReferenceException` at
`validateStepActions` now displays at the top; `PrimitiveConversionFailed`
demoted to a chain entry. Regression test
`PLang.Tests/App/ErrorBuryingReproTest.cs` asserts both the key ordering
and the `Format()` byte-offset ordering — robust to incidental wording
changes but catches a flip.

Minor smell: mutating the caller-provided `Error` instance (appending
to its `ErrorChain`) couples the conversion call to whoever owns that
error. In practice the source `Error` here is a one-shot pipeline
artefact, so the side effect is invisible. Flag as A2 below for
visibility, not blocking.

### Builder pipeline — confidence + verb rule + Actor discipline

- **Plan.llm verb-rule rewrite** — replaces the event.on-specific
  paragraph with a generic principle ("action set comes from the verb,
  argument values never decide action selection"). Three worked
  examples cover the three classes of failure (`call X kind="goalError"`
  → goal.call; `set %x% = %a%+%b%` → set + math.add peers; `write out
  "deleting %file%"` → output.write only). Closes the recurring rebuild-
  drift that misclassified `goal.call kind="goalError"` as `event.on`.
- **goal.call.notes.md "Actor must come from step text"** — codifies the
  fix that coder hand-patched in `827d34e19`. Three anti-patterns called
  out. Notes-md routing (per-action, only when planner picked the action)
  means this prose lands in `goal.call` Compile rounds without polluting
  the cross-cutting kernel.
- **`list<T>` schema canonicalisation** across 5 llm.query schemas —
  no behaviour change, just consistency with the type-name catalog in
  `Compile.llm:235`.
- **Confidence-per-step** — emitted in step results; observable via the
  CLI surface (`⚠ planner VeryLow` etc.). Tester's call to delete the
  trace-asserting `.test.goal` was correct — the CLI line is the assert.
- **BuildStep + EmitBuildEvent + BuilderChannel** — orchestration goals
  added under `os/system/builder/`. Builder channel's body is
  `- write out %!data%` (writes to the actor's *own* `output`, not back
  to `"builder"`) — no recursion path closes through it today.

### Tagged cache test fix (F2) — cross-file deletion clean

Coder removed `Tagged.ClearCacheForTests()` + `Tagged.CacheSize` and
their one remaining call site in `DebugModeBypassTests`. Grep-verified no
production caller; only the deleted test reached the global. The new test
`NormalizeTreeShapeTests.cs` asserts `ReferenceEquals(PropertiesFor(t,Out),
PropertiesFor(t,Out))` — per-key identity, no global counter, no parallel
race. Sound.

### Data-normalize merge — no regression on already-audited shape

Spot-checked: `Tagged.cs` no longer carries the test-only API; `Wire`,
`Normalize`, `IWriter`, `Tagged.PropertiesFor` cache, View threading,
[Out] discipline all match what auditor v1 on data-normalize confirmed.
No new sites violate the System.IO ban (PLNG002), no new `Console.*`
egress, no envelope-shape regression.

## Findings

### A1 — Latent: `AppChannels.Channel(string)` bypasses `IsExecuting` (minor, concur with security F1)

`PLang/app/channels/this.cs:Channel` (the opportunistic-write variant
with no-op fallback) does **not** consult `IsExecuting`:

```csharp
public channel.@this Channel(string name)
    => _channels.TryGetValue(name, out var channel) ? channel : NoOp;
```

vs. `Get` which does:

```csharp
public channel.@this? Get(string name)
{
    if (!_channels.TryGetValue(name, out var channel)) return null;
    if (channel is channel.goal.@this g && g.IsExecuting) return null;
    return channel;
}
```

Today the only production caller is `PLang/app/modules/file/read.cs:76`
writing a `builder.warning` to channel `"builder"`. The builder channel
body is `- write out %!data%` — which writes to the actor's `output`
channel, not back to `"builder"` — so no recursion path closes through
this site.

The smell is shape, not behaviour: two near-identical lookup methods
where only one carries the guard discipline. A future caller using
`Channel(name)` whose name is registered as a goal-channel with a body
that triggers `file.read` (or any path through `Channel(name)` on the
same name) closes the loop without warning.

**Suggested:** mirror the `Get`-side guard in `Channel`:

```csharp
public channel.@this Channel(string name)
{
    if (_channels.TryGetValue(name, out var channel))
    {
        if (channel is channel.goal.@this g && g.IsExecuting) return NoOp;
        return channel;
    }
    return NoOp;
}
```

Two lines. Keeps the guard discipline on **the type that owns the
channel registry**, not on every caller.

**Severity:** Minor (latent). Not a blocker — same call as security F1.

### A2 — Subtle: `Conversion.TryConvertTo` mutates caller's `ErrorChain` (minor)

`PLang/app/types/Conversion.cs:401-414` — when the source value to
convert is itself an `errors.Error`, the conversion-failure wrapper is
appended to `sourceErr.ErrorChain` and `sourceErr` is returned. The
caller never asked for their input to be mutated.

Today this is invisible: the source `Error` in the demoed scenario is a
pipeline artefact owned by the call chain that just produced it, and
nothing else reads `ErrorChain` after the conversion attempt fails. But
the pattern — mutate-the-input-as-side-effect-of-a-pure-looking-conversion
— is the kind of thing that bites when callers start retrying, caching,
or comparing errors by identity.

**Suggested (if revisited):** clone the source `Error` before
appending to its `ErrorChain`, or return a fresh outer `Error` whose
`Cause` (rather than `ErrorChain`) points at the source — preserving
the "primary stays primary" ordering at `Format()` time without
mutating shared state.

**Severity:** Minor. Behaviour is correct for the demoed scenario; the
shape is what's noted.

## Not-findings (checked, clean)

- **`FreezeFoundational` / `FoundationalChannels` / `PushChannelsOverride` /
  `Snapshot`** — source-tree grep clean (security already verified).
  Confirmed no stale documentation pointers either.
- **Channel(name) callers** — grep returned exactly one
  (`file/read.cs:76`). No surprise consumers in `PLang/app/modules/` or
  builder source.
- **GoalChannelRecursion .pr files** — actions match step text; no
  false-green of the type tester v1 found in `UnknownVerb`.
- **`error.throw Message=%!error%`** call shape (the trigger of P4 in the
  builder) — unchanged at the source; the fix is purely on the conversion
  side, no caller had to adopt new shape.
- **System.IO / Console.*** — no new violations introduced on
  branch-local commits.

## Verdict

**PASS.** Both findings are minor and latent. Branch is sound, the
recursion guard is correctly placed on the channel type (not on every
caller), and the planner verb rule + Actor-from-step rule are real
codifications of patterns the coder had been hand-patching.

A1 (`Channel(name)` bypass) is the same call security made and the same
two-line fix; could ride a later channel-routing pass.

A2 (Conversion mutates input ErrorChain) is shape-only — flag and move
on.

**Next:** `docs` — wire up the user-feedback recipe and the merge story.
