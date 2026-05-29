# Security result — builder-ergonomics v1

**Verdict:** PASS
**Phase:** blue + red
**Posture:** Net change is a security improvement. One Low finding (latent recursion
hole via `AppChannels.Channel(name)` bypass).

## Attack-surface mapping

### Channel recursion guard (load-bearing)

**What was removed.** `Actor.FoundationalChannels`, `Actor.FreezeFoundational`,
`Actor.PushChannelsOverride`, `AppChannels.Snapshot`, `FoundationalChannels` lazy
init, and the `AsyncLocal<AppChannels?> _channelsOverride` indirection. The override
layer that routed writes inside a goal-body back to a frozen channel set is gone.

**What replaces it.** `channel.goal.@this` holds a private `AsyncLocal<bool>
_executing`. `InvokeGoal` sets it to `true` before `await Actor.App.RunGoalAsync(Goal,
ctx, ct)` and restores `prev` in `finally`. `AppChannels.Get(name)` checks
`channel is channel.goal.@this g && g.IsExecuting` and returns `null` when true.
`GetChannel` then surfaces `ChannelNotFound`.

**Why this is sound.**

- AsyncLocal value flows down the await chain to `RunGoalAsync` and all descendants
  on the same async context. `_executing` is observable to the registry from inside
  the goal body.
- A Task.Run / Task.Factory.StartNew spawned inside the body inherits the AsyncLocal
  *snapshot* via copy-on-write, so the spawned task still sees `_executing = true`
  and recursion is still blocked. A write back into the goal body via the spawned
  task cannot break out of the guard.
- The `try/finally` restores `prev` even on catchable exceptions; the `when` clause
  excludes only `NRE/OOM/SOF` — same unrecoverable filter used elsewhere — so the
  flag never leaks `true` post-call on a recoverable failure.
- Cycle A→B→A: A executes, sets A.IsExecuting=true; A.body writes to B; Get("B")
  returns B (B.IsExecuting=false); B executes, sets B.IsExecuting=true; B.body
  writes to A; Get("A") returns null because A.IsExecuting=true on the *same async
  context*. Surfaces ChannelNotFound at B's write call. Sound.
- Per-channel flag (not per-actor or per-app) — concurrent calls into *different*
  goal channels do not collide. Multiple concurrent calls to the *same* goal
  channel each set their own AsyncLocal slot; each call's read sees `true` for
  itself and isolates correctly.
- `_executing` is `private`; `IsExecuting` is get-only. No external mutation.

**Mutation evidence (independent re-run reported by tester v2 and consistent with
the regression-test shape):** delete `_executing.Value = true` →
`RecursionDepthExceeded` / call-stack backstop fires instead of the specific
`ChannelNotFound`. The test asserts on the exact `errorKey`, so the guard is
genuinely covered, not "any error" handwaving.

### Removed-API straggler check

`grep -rn "FreezeFoundational\|FoundationalChannels\|PushChannelsOverride\|
ChannelsOverride\|_channelsOverride\|Channels.Snapshot"` across `PLang/`,
`PLang.Tests/`, `PlangConsole/` returns only stale generated test metadata under
`PLang.Tests/obj/Debug/net10.0/generated/…` — source-tree references all replaced.
Stale metadata clears on next test-project rebuild; harmless for security.

### Error chaining (Conversion.cs)

`PrimitiveConversionFailed` now sets `Exception = ex` and, when the inbound `value`
is itself an `errors.Error`, appends `convErr` to `value.ErrorChain` and returns
`value` rather than the new error. No new sensitive data egress paths — the
message format (`'{value}' ({sourceType.Name}) to {targetType.Name}: {ex.Message}`)
is identical to pre-change. `ErrorChain.Add` mutates the inbound Error directly; no
shared accumulator, no cross-actor concurrency. Sound.

### Tagged.cs

Removed `internal static ClearCacheForTests()` / `internal static int CacheSize`.
Test-only surface; no production code paths affected. The `_cache` is still
unbounded — that is a *pre-existing* latent DoS, noted in branch memory
(`pattern_tagged_transparent_fallback.md`), not a regression on this branch.

### Builder pipeline

`os/system/builder/**` regenerated `.pr` files, new `BuilderChannel.goal`,
`EmitBuildEvent.goal`, LLM prompt tweaks. All build-time orchestration, no
untrusted-data ingress. BuilderChannel.goal body is `- write out %!data%` — does
not write back to "builder", so no recursion. EmitBuildEvent.goal is wired in the
build pipeline only.

### Semgrep

`scripts/semgrep-scan.sh` → 15 findings, all on the known serializer-hygiene
baseline (PathHttp:448, Fluid:141, etc.). No new architectural-rule hits from this
branch's edits.

## Findings

### F1 — `AppChannels.Channel(name)` bypasses IsExecuting guard

- **Severity:** **Low** (latent, single existing caller is safe today).
- **Category:** resource-exhaustion / logic.
- **Vector.** `AppChannels.Channel(string name)` (the NoOp-fallback variant used for
  opportunistic writes) calls `_channels.TryGetValue` directly and returns the raw
  channel — it does **not** consult `IsExecuting`. If any caller writes via
  `Channels.Channel(name)` from inside a goal-channel whose name is `name`, the
  recursion guard is silently bypassed; the goal re-invokes itself until the
  engine's call-stack backstop (`RecursionDepthExceeded`) fires.
- **Preconditions today.** Only one caller exists:
  `PLang/app/modules/file/read.cs:76` writing a missing-file warning to
  `"builder"`. `BuilderChannel.goal`'s body is `- write out %!data%` and does not
  invoke `file.read`, so no recursion path closes today. The vector is **latent,
  not exploitable in the current source.**
- **Impact when armed.** Goal-channel body that triggers a `Channel()`-bypass write
  back to its own name → stack growth bounded only by `RecursionDepthExceeded`.
  Local DoS / call-frame churn during a build. No code execution, no data egress.
- **Why Low.**
  - No reachable closing edge in current source.
  - CallStack backstop is unrecoverable but bounded; same posture as other
    depth-guarded paths.
  - Attacker has no inbound channel; trigger requires the user to write a goal
    whose body composes through `Channel()`-bypassing module code.
- **Proposed fix.** Mirror the guard in `Channel(name)` — if the resolved channel
  is a goal-channel and `IsExecuting`, return `NoOp` (the existing fail-open sink),
  not the channel. Two-line change in `PLang/app/channels/this.cs`. Closes the
  parity gap and removes a "next maintainer adds a caller and ships a recursion
  bomb" footgun. Not a blocker; safe to land any time.
- **Status:** open, accepted as Low. Recorded to memory for any future
  channel-routing review.

No Critical / High / Medium findings.

## Summary

Per-channel `AsyncLocal<bool>` recursion guard is the right shape: smaller surface
than the deleted Snapshot/override layer, observed at the resolution party
(`AppChannels.Get`), no cross-actor state, no allocation per call. AsyncLocal
semantics — flow-down to descendants, copy-on-write to Task.Run children, restore
in `finally` — all hold. The single bypass route (`AppChannels.Channel(name)`) is
not reachable through current callers; flagged Low so the parity gap is on record.

Removals are clean: no source-tree references to the deleted APIs remain. Error
chaining is local, non-cyclic, doesn't change the message-leakage posture. Builder
pipeline changes are orchestration. Semgrep is at baseline.

**Verdict: PASS. No critical/high findings open.**
