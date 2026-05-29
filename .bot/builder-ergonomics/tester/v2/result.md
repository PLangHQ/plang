# Tester v2 result — builder-ergonomics

**Verdict: PASS**

Clean rebuild (stale-binary protocol; `dotnet build PlangConsole` exit 0). Both suites green.

| Suite | Total | Pass | Fail | Timeout |
|-------|-------|------|------|---------|
| C# (`dotnet run --project PLang.Tests`) | 3377 | 3377 | 0 | — |
| PLang (`cd Tests && plang --test`) | 234 | 234 | 0 | 0 |

(Http tests all passed this run — network was up. The v1 `UploadFile` 502 / coder's
`DownloadSkip` timeout were environmental, as classified.)

## F1 — false green removed ✓

`Tests/ConfidenceCheck/` (goal + mis-compiled `.pr`) is deleted. Verdict: correct call.
A trace-based PLang assert would either need a flaky inline rebuild of `compress` or a
committed trace fixture that can't catch live-emission regressions — both worse than
deletion. Removing a test that asserted nothing and shipped the bug it should catch is
strictly better than keeping it.

**Residual (observation, not a finding):** the confidence-per-step feature (P6) now has no
automated test in either suite. The coder verified `⚠ planner VeryLow` live in build output;
that's a real CLI-surface behaviour but it's pinned only by human observation. If P6 is
load-bearing, a future test that drives the confidence renderer from a fixture/mock plan
(decoupled from the live LLM) would close the gap. Not a blocker for this branch.

## F2 — cache test fixed ✓

`NormalizeTreeShapeTests.cs` now asserts the behaviour the filter owns:

```csharp
var first  = Tagged.PropertiesFor(t, View.Out);
var second = Tagged.PropertiesFor(t, View.Out);
await Assert.That(object.ReferenceEquals(first, second)).IsTrue();
```

Same `(Type, Mode)` key hands back the same `IReadOnlyList<Entry>` reference — what
`_cache.GetOrAdd` guarantees. No global counter, no parallel race. Would still catch a
real regression (caching removed → `PropertiesFor` recomputes → different reference → fail).
`ClearCacheForTests()`/`CacheSize` removed from `Tagged` and the `DebugModeBypassTests` call
site, so the global-clear flake source is gone. **C# suite is 3377/3377** — the v1 flake is
resolved.

## F3 — channel recursion guard now covered end-to-end ✓ (independently mutation-confirmed)

`Tests/Channels/GoalChannelRecursion/Start.test.goal`:

```
- set channel "echo" call EchoBack          # channel.set(Name="echo", Goal={EchoBack})
- write 'hello' to echo, on error call InspectError
- assert %errorKey% equals 'ChannelNotFound'
```

**Builder false-green check:** read the committed `start.test.pr` — every step's
`actions[0].module.action` matches its text (`channel.set`, `output.write | error.handle →
goal.call InspectError`, `assert.equals(Expected="ChannelNotFound", Actual=%errorKey%)`).
No action-index shift, no loose assertion.

**Independent mutation (announced, reverted, source clean):** I commented out
`_executing.Value = true` in `InvokeGoal`, rebuilt PlangConsole, re-ran the test:

```
[Fail] /Channels/GoalChannelRecursion/Start.test.goal
  Expected: "ChannelNotFound"
  Actual:   "CallStackOverflow"
```

This is the key result. Without the guard, the self-write recurses until the engine's
call-stack backstop fires (`CallStackOverflow`) — and the test fails because it asserts the
*specific* `ChannelNotFound` key. A weaker "any error" assert would have passed on the
backstop error (the coder's own first draft hit exactly this and was correctly strengthened).
Reverted, rebuilt, re-ran: back to 234/234, recursion test green.

So the guard-arming path that was invisible to all 7 v1 reflection-flip tests is now caught
by a real goal-channel self-write. F3 closed.

## F4 / F5 — deferred (accepted)

- **F4** `UploadFile` 502 (env, network) — separate ticket. All Http tests passed this run.
- **F5** `y/n/a` permission-prompt leak — fixture hygiene, separate ticket.

Both are environmental/hygiene, not branch-code defects. Reasonable to defer.

## Conclusion

All three actionable v1 findings (F1, F2, F3) are resolved and independently verified. Both
suites green from a clean rebuild. No new false greens, no builder action-mismatch, the
load-bearing recursion guard is mutation-proven covered. **PASS.**
