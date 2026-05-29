# Tester v1 result — builder-ergonomics

**Verdict: FAIL**

Clean rebuild from scratch (stale-binary protocol followed; `dotnet build PlangConsole` exit 0).

## Test runs

| Suite | Total | Pass | Fail |
|-------|-------|------|------|
| C# (`dotnet run --project PLang.Tests`) | 3377 | 3376 | **1** |
| PLang (`cd Tests && plang --test`) | 234 | 233 | **1** |

Both failures are real. One of them is a flaky shared-static test (passes alone, fails in
suite). Separately, one shipped test is a confirmed false green, and the branch's central
C# change has no end-to-end coverage (mutation-confirmed).

---

## Finding 1 — CRITICAL — false green: the P6 reproduction asserts nothing AND ships the bug it exists to catch

`Tests/ConfidenceCheck/UnknownVerb.test.goal` is the headline deliverable's reference
reproduction (P6, confidence-per-step). It reports `[Pass]` in `plang --test`. It is a false
green on three counts:

1. **It has no `assert`.** It's a `.test.goal` that only `set`s, "compresses", and `write out`s.
   The handoff admits this ("it doesn't assert — it always 'passes' if it builds"). A test that
   asserts nothing cannot fail for the right reason.

2. **Its committed `.pr` demonstrates the exact bug P6 exists to catch.** Step 1 —
   `compress %original%, write to %archived%` — compiled to **`variable.set`**. That is the
   silent intent-drop ("planner ignored `compress`") that the whole confidence/verb-coverage
   feature was built to surface. The reference reproduction ships bytecode that *is* the
   failure mode, runs it green, and asserts nothing about it.

   ```
   step 1  text: 'compress %original%, write to %archived%'
           action: variable / set          ← compress silently dropped
           confidence: null                 ← not persisted in the .pr at all
   ```

3. **Confidence is not in the `.pr`.** All three steps have `confidence: null`. The confidence
   values live only in the trace, so the committed bytecode carries no evidence the feature ran.

On top of that, the handoff's smoke-test #2 ("expect `⚠ planner VeryLow` / `⚠ compiler VeryLow`")
**does not reproduce**. Two clean rebuilds of this goal produced two *different* hard failures and
zero confidence warnings:

- run A: `ValidationErrors(400)` — "Step[1]: no actions. Every step must have at least one action."
- run B: `BuilderPlannerFailed(400)` — "step count didn't match the goal, retry didn't recover."

The build fails at the planner/validation stage **before** per-step confidence emission is
reached, so the documented observable never appears. (LLM non-determinism; no committed trace
to fall back on.)

**Impact:** the branch's flagship feature has no honest test. A regression that silently drops
a verb — the precise thing P6 targets — would keep this test green.

**Suggestion:** make it a real test. Build → read the trace → assert
`plan.steps[1].confidence == "VeryLow"` (and the compiler pass likewise), OR assert the rendered
build output contains the `⚠ planner` warning line. If the goal can't build deterministically,
drive the confidence renderer from a fixture/mock plan instead of the live planner. Either way,
stop shipping a `variable.set` `.pr` as the "unknown-verb" reference.

---

## Finding 2 — MAJOR — flaky test: assertion on a process-wide static cache under parallel execution

`PLang.Tests/App/DataTests/NormalizeTreeShapeTests.cs:129`
`Normalize_PropertyLookupCache_PopulatesOnFirstCall_HitsOnSecond`

This is the **1 C# failure**. It **passes in isolation, fails in the full suite** — the signature
of shared-mutable-static state under TUnit's parallel runner.

```csharp
Tagged.ClearCacheForTests();
var sizeBefore = Tagged.CacheSize;
new Data("", identity).Normalize();
var sizeAfter1 = Tagged.CacheSize;          // == 1 (Identity cached)
new Data("", identity).Normalize();
var sizeAfter2 = Tagged.CacheSize;          // expected == 1
await Assert.That(sizeAfter2).IsEqualTo(sizeAfter1);   // FAILS: actual > 1
```

`Tagged._cache` is `static readonly ConcurrentDictionary<(Type,View), …>`
(`PLang/app/channels/serializers/filters/Tagged.cs:33`). `CacheSize` is the **global** count.
Other Normalize tests running concurrently (`Normalize_DomainObject…`, `Normalize_RecordType…`,
etc.) add their own `(Type,View)` entries between the two reads, so `sizeAfter2 > sizeAfter1`.

Two independent defects in one test:
- **It asserts a global counter it doesn't own** — any concurrent type-normalization perturbs it.
- **`ClearCacheForTests()` is itself a global side-effect** — clearing the cache mid-flight can
  invalidate cache-hit assumptions in *other* parallel tests. This test can make its neighbours
  flaky, not just itself.

**Impact:** non-deterministic suite. CI red/green flips with thread scheduling; a real cache
regression would be indistinguishable from this noise.

**Suggestion:** assert behaviour the test owns, not the shared count. e.g. expose a
per-key hit indicator and assert the *specific* `(typeof(Identity), View.X)` entry is reused
(same reference returned, no recompute) — or count only that key's presence before/after, not
`_cache.Count`. If a count assertion is unavoidable, mark the test `[NotInParallel]` AND drop the
global `ClearCacheForTests()` (or scope the cache per-test).

---

## Finding 3 — MAJOR — missing coverage: the channel recursion guard is never armed in any test (mutation-confirmed)

The branch's central C# change replaces the foundational-snapshot mechanism with a per-channel
`IsExecuting` AsyncLocal guard. `InvokeGoal` arms it:

```csharp
var prev = _executing.Value;
_executing.Value = true;     // ← the guard
try { return await Actor.App.RunGoalAsync(Goal, ctx, ct); }
finally { _executing.Value = prev; }
```

**Mutation test (announced, reverted, source clean):** I commented out `_executing.Value = true`
and reran `Stage3_GoalChannelTests` — **all 7 tests passed.**

The reason: every test that exercises the guard
(`Channels_Get_TreatsExecutingGoalChannelAsNotFound`,
`Channels_Get_LateRegisteredChannel_VisibleEverywhere`) flips the AsyncLocal **directly via
reflection** (`_executing` field), never through a real `InvokeGoal`. `GoalChannel_IsExecuting_
IsFalseBeforeAndAfterWrite` only checks before/after (both false regardless of the `finally`
restore). So no test asserts that *running a goal-channel write actually arms the guard*.

**Impact:** if a refactor removed or broke the arming line, a goal-channel whose body writes to
its own name would loop back into itself → stack overflow in production, suite stays green. This
is the precise bug (`foundational-channels-snapshot-bug.md`) the change exists to prevent, and it
has no end-to-end test.

**Suggestion:** add a test with a *real* goal body that writes to its own channel name, asserting
the inner write surfaces `ChannelNotFound` (404) rather than recursing — i.e. register a
goal-channel `"echo"` whose goal does `write out %!data% channel: "echo"`, `Write` to it, and
assert the returned Data carries `ChannelNotFound`. That exercises the arm-during-execution path
the reflection-flip tests skip.

---

## Finding 4 — MINOR (red, environmental) — `UploadFile.test.goal` → 502 Bad Gateway

The **1 PLang failure**. `/Modules/Http/UploadFile/UploadFile.test.goal` fails with
`Error: 502 Bad Gateway` from an external upload endpoint. Nothing on this branch touches HTTP
(channels/conversion/builder only), so this is not a regression — it's a network-dependent test
hitting a down/proxied gateway. Red is red (it counts against the run), but the cause is the
environment, not the coder's change. Flagged so it isn't mistaken for a code regression.

**Suggestion (orthogonal to this branch):** network-dependent tests should mock the transport or
be quarantined behind an integration tag so a 502 doesn't redden the unit suite.

---

## Finding 5 — MINOR — interactive `y/n/a` prompt leaks into the C# suite

During `dotnet run --project PLang.Tests`, the run printed
`Allow User to execute //nonexistent/ghost-provider.dll? (y/n/a)` and waited on stdin. It
completed (some test isn't redirecting input / pre-answering the permission gate). Not a
failure, but a hygiene issue — in a non-interactive CI without a TTY this could hang or consume
unintended input.

**Suggestion:** the test triggering the dll-execution permission gate should supply a redirected
input channel (auto-deny) rather than falling through to the console prompt.

---

## Observations (not findings)

- `EmitBuildEvent.goal`'s doc comment documents `event="…"` params, but the code and template use
  `kind="…"` (`{% case kind %}`, `kind="build-path"`). Stale comment; cosmetic.
- The builder-channel routing **is** live (EmitBuildEvent writes `channel: "builder"`), contrary
  to the workaround note in `foundational-channels-snapshot-bug.md` — the coder's `IsExecuting`
  fix landed and the workaround was reverted. The two handoff docs disagree; the source is the
  truth here. The `Channels_Get_LateRegisteredChannel_VisibleEverywhere` test does pin the
  regression at the registry level (good), even though the InvokeGoal arming is untested (F3).

## What's solid

- P4 (`ErrorBuryingReproTest`) is a genuinely good test: it asserts the *order* of headers in
  `Format()` (root `NullReferenceException` above the `PrimitiveConversionFailed` footer) and the
  chain shape, not just success/failure. It would catch a flip back to scaffolding-first display.
- `channels/this.cs.Get` registry logic (treat-executing-as-not-found, late-registered-visible)
  is well covered at the unit level — the gap is purely the *arming* path (F3).
