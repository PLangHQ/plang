# Coder summary — runtime2-channels

## Version

**v6** (current). v1–v5 history kept in this branch's report.json.

## What v6 is

Address codeanalyzer v2 (FAIL) on top of coder v5. Two of v5's "fixes" landed
incorrectly (B1 and B2), and the wider design conversation with Ingi
reframed the underlying model so future parallel work has a clean home.

The prior model (v5) said: every `goal.call` boundary pushes a per-call
parameter scope. That scope was a *read-only* overlay: parameter names
shadowed underlying writes, and `set %x% = 2` inside the called goal was
invisible to subsequent `get %x%` in the same goal — an unintuitive trap
(L1 in codeanalyzer's report). It also didn't actually solve the race that
motivated it: `RunGoalAsync(GoalCall)` was still injecting parameters into
the actor-shared dict before falling through.

The v6 model: **Push iff fork.** A fork is what *spawns concurrency* —
parallel foreach iteration, future explicit async-call operator, future
inbound-listener accept loop. Sequential `goal.call`, channel write,
callback dispatch, and sequential foreach iter all pass through whatever
flow they were called in. Inside a fork overlay, *every* `Variables.Set`
is captured (not just parameter shadows) so siblings stay isolated.
Reads cascade overlay → caller chain → actor dict. Communication out of
a fork is via explicit return collection (`append to %results%`), not
shared mutation.

This dissolves L1 (sequential `goal.call` has no overlay → `set %x% = 2`
hits actor dict → `get %x%` returns 2 immediately and after the goal
returns), preserves the LoadUser leak pattern (called goal sets `%user%`
and caller sees it), and gives parallel foreach a precise home for the
moment it lands.

## What was done

### B1 — Channel null-Actor handler

`PLang/App/Channels/Channel/this.cs` — codeanalyzer's prescribed fix
(`return Data.Ok(null)` early when ctx is null) breaks the Stage 8
channel-event tests, which construct channels without an Actor and rely
on their handlers firing. Reverted to v5's "log diagnostic + forward
null" shape; comment updated to make the contract explicit. The only
production handler that *requires* ctx is the `event.on` goal-dispatch
handler — it can guard locally.

### B2 — Variables.Set overlay-aware + no Push at goal.call

`PLang/App/Variables/this.cs` — `Set` now routes simple-name writes into
`Calls.Current` if an overlay is active, else to the actor-shared dict.
Inside an overlay, writes for a name that already lives in this overlay
mutate in place; writes for new names mint a fresh local entry that
shadows whatever was inherited via the Caller chain — never bleed up.

`PLang/App/Variables/Calls/Call/this.cs` — `Call.@this` is now a mutable
overlay, not a read-only param frame. Holds its own `Dictionary` and
exposes `Set`, `TryGet`, and `ContainsLocal`. Reads walk the Caller chain
unchanged.

`PLang/App/this.cs` `RunGoalAsync(GoalCall)` no longer pushes a frame.
Comment updated to describe the new policy: caller-flow inheritance via
AsyncLocal handles isolation when (and only when) something forked.

`PLang/App/Channels/Channel/Goal/this.cs` — `GoalChannel.InvokeGoal` no
longer pushes either. Channels are pass-through plumbing; `write out`
shouldn't silently wrap the bound goal in an isolating scope. The fork
lives at whoever spawned concurrency above.

### I1 — `Push_FrameInvisibleToParallelFlows` actually parallel now

`PLang.Tests/App/VariablesTests/CallsTests.cs` — converted the
`ContinueWith` chain to `Task.WhenAll`, renamed
`Push_ParallelFlows_EachSeesOwnBinding`. Added four more tests for the
new mutable-overlay semantics:

- `SetInsideOverlay_IsVisibleToSubsequentGet` — the L1-dissolution test.
- `SetInsideOverlay_DoesNotLeakToUnderlying` — writes inside vanish on dispose.
- `SetInsideOverlay_NewName_DoesNotEscape` — same for fresh names.
- `SetInsideOverlay_DoesNotLeakToSiblingOverlay` — the production race
  the v5 model was meant to solve, now expressed as a parallel test.

### I2 — `Channel.Events` encapsulated

`PLang/App/Channels/Channel/Events/this.cs` (new) — owns the bindings
list, the lock, the `Match(type, channelName)` filter, and the AsyncLocal
recursion guard (`IsActive`/`Enter`). Channel keeps the cross-source
orchestration (per-channel → per-actor → app-level) since it spans three
owners. Test surface preserved (`ch.Events.Add(...)`, `ch.Events.Count`).

### PLang scenario locked in

`Tests/Modules/Variable/Scoping/VariableScoping.test.goal` extended with
the param-leak scenario:

```plang
- call InnerWithParam id=42
- assert %id% equals 999
- assert %user% equals "alice"

InnerWithParam
- set %id% = 999
- set %user% = "alice"
```

Both `%id%` (param mutation) and `%user%` (fresh name) leak back to the
caller — sequential `goal.call` is fully transparent.

## Test results

- C# **2760 / 2760 pass** (was 2757; +3 new CallsTests).
- PLang **201 / 201 pass**, 0 fail, 0 stale.

Both clean.

## Code example — the rule, one sentence

```csharp
// Variables.Set, simple-name path:
var frame = Calls.Current;
if (frame != null) { /* write into innermost overlay (capture) */ }
else                { /* write into actor dict (LoadUser leak)  */ }
```

```csharp
// Variables.Get walks: Calls.Current.TryGet → Caller chain → actor dict.
```

```plang
// PLang developer's view (no fork above):
Start
- call LoadUser id=42
- write out %id%    / 999
- write out %user%  / 'alice'

LoadUser
- set %id% = 999
- set %user% = 'alice'
```

## What v7 would do

- `loop.foreach` parallel mode: when added, each iteration awaits-using
  `ctx.Variables.Calls.Push(seedParams)` and joins via `Task.WhenAll`.
  The `Variables.Set` overlay routing already supports this — no change
  needed in the variables layer when the foreach feature lands.
- Optionally: rename `%__data__%` → `%!data%` (Ingi noted as a future
  cleanup — not in scope for this branch).

## For reviewer

Suggest **codeanalyzer v3** to verify findings closed and check the new
`Variables.Set` overlay routing (especially the corner where a write to
a fresh name inside an overlay mints a local entry rather than mutating
an inherited Data — that's the corner that bit me once and got fixed
mid-v6).
