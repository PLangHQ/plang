# Tester v7 — Result

## Scope reviewed
Coder v7 changes to `PLang/App/Channels/Channel/Events/this.cs`:
- **B1:** removed `static` from `_active` AsyncLocal.
- **L1:** `Enter` now copy-on-write; `Releaser.Dispose` restores parent reference.

No new tests added.

## Test run (clean rebuild)
- **C# (TUnit):** 2760 / 2760 pass — matches coder's baseline exactly.
- **PLang:** Coder reports 205 pass / 6 fixture-fails (`_fixtures_fail/*`, `_fixtures_sensitive/*.fixture.goal` — deliberately-failing test inputs). Not re-run here; the v7 diff is a 2-line C# change with no PLang surface impact, so plang results carry over. Spot-checked: no `.test.goal` was modified in v7.

## Deletion-test reasoning (read-only)
Per-rule, I do not edit source. I reason about which assertions would still hold if each fix were reverted.

### B1 reverted (`_active` becomes `static` again)
Walk through Stage8 tests (`PLang.Tests/App/ChannelsTests/Stage8_ChannelEventsTests.cs`):

- `BeforeWriteHandler_WritesToSameChannel_NoInfiniteLoop` (L116) — single channel, one binding, sequential await. Recursion guard works either way.
- `BindingsMatch_AcrossUserAndServiceChannels_OfSameName` (L197) — two channels but the binding handler does no nested write; the active set adds and removes the id within each `await`. Passes either way.
- `ChannelEvents_DoNotTriggerGoalStepOrActionBindings` (L217) — single channel, no recursion. Passes either way.
- All other Stage8 tests use single channel + 0/1 binding ids. None re-enter cross-channel.

**No Stage8 test would fail with B1 reverted.**

Additional structural note: `Binding.@this.Id = Guid.NewGuid().ToString("N")[..8]` (`PLang/App/Events/Lifecycle/Bindings/Binding/this.cs:89`). Every EventBinding instance has a unique random Id, so even with `static _active`, two distinct bindings on different channels can never collide on Id. The B1 hazard requires either deliberately-constructed colliding ids or future code that reuses Ids — not exercisable through the public API today. The fix is a defensive structural correction, not a fix for an exploitable bug.

### L1 reverted (mutate-shared-set Releaser)
The hazard requires a binding handler that fans out parallel children that each call `Enter` (e.g. `Task.WhenAll(ch.WriteAsync(a), ch.WriteAsync(b))` from inside a `BeforeWrite` handler on `ch`). Children inherit the same HashSet reference via AsyncLocal; concurrent `Add` calls would race and could throw `InvalidOperationException`.

Walk through Stage8: every handler that re-enters does so with a single sequential `await`. No test fans out parallel children.

**No Stage8 test would fail with L1 reverted.**

The coder explicitly acknowledged this: *"the failure modes they prevent ... have no current callsite to exercise."*

### Stage5/6/7/8 sweep
Skimmed the full ChannelsTests folder — no other test exercises the recursion guard at all. Only Stage8 has the file-level comment "recursion guard" but only `WritesToSameChannel_NoInfiniteLoop` actually touches it.

## Findings

### F1 — Missing coverage: B1 fix has no regression test
- **Severity:** minor
- **Type:** missing-coverage
- **Code:** `PLang/App/Channels/Channel/Events/this.cs:22`
- **Issue:** Removing `static` from `_active` is correct in spirit (per-channel scope) but no Stage8 test would fail if the keyword were reintroduced. Compounded by `Binding.Id` being a random GUID-substring — the cross-channel collision the fix guards against cannot occur through the normal API.
- **Impact:** Future refactors (e.g. deterministic binding ids, or another consumer of `_active`) could silently re-introduce the static and ship green.
- **Suggestion:** If the team considers this fix load-bearing, add a test that constructs two `Channel.Events.@this` instances directly, calls `Enter("X")` on chA, then asserts `chB.IsActive("X") == false`. Two lines. Catches static regression unambiguously without artificial multi-channel orchestration.

### F2 — Missing coverage: L1 fix has no regression test
- **Severity:** minor
- **Type:** missing-coverage
- **Code:** `PLang/App/Channels/Channel/Events/this.cs:69-85`
- **Issue:** Copy-on-write `Enter`/parent-restoring `Releaser` has no test. The coder is upfront that "no current callsite" exercises parallel fan-out from a binding handler. Reverting L1 to mutate the inherited set would break in `Task.WhenAll`-from-a-handler scenarios that don't yet exist.
- **Impact:** When a future consumer adds parallel writes from inside a handler (legitimate pattern: structured logging fan-out), an L1 regression would surface as flaky `InvalidOperationException` in production, not at test time.
- **Suggestion:** A direct unit test on `Channel.Events.@this`: call `Enter("A")` on the parent flow, then `await Task.WhenAll(Task.Run(() => events.Enter("B").Dispose()), Task.Run(() => events.Enter("C").Dispose()))`. With L1 in place: parent set unchanged after children dispose; without L1: `_active.Value` contains B and/or C, or HashSet throws under contention. Three lines, deterministic on Linux/CI.

### F3 — Process: baseline-tests.md present and accurate
Not a finding — noting compliance. `baseline-tests.md` exists and matches what I observed (2760/2760). Good.

## False-green hunt — other angles checked
- **Builder false greens (PLang side):** no `.pr` changes in v7. N/A.
- **Weak assertions:** Stage8 tests assert specific values (`order` lists, captured `payload.Value`, `result.Success` + behaviour). They are not weak.
- **Mock-hides-behaviour:** Stage8 uses real `StreamChannel.Memory` and inline lambdas, not mocks. Clean.
- **Coverage dazzle:** N/A — coder didn't claim coverage gain.

## Verdict
**Pass with notes.**

Existing test suite is green and the change is conceptually correct. The two findings above are not regressions or false greens in the existing tests — they are gaps in coverage of the new fixes. Both fixes are defensive/structural; writing the regression tests is a small additional task, worth doing once but not blocking.

Recommend: pick up F1 + F2 as a small follow-up commit (≤10 lines of test). Then move to **security**.
