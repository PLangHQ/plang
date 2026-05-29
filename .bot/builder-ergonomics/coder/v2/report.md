# Coder v2 report — builder-ergonomics

**Verdict:** addresses tester v1 FAIL findings. Both suites green (modulo
environmental Http timeout).

## What I did

Three actionable tester findings (F1/F2/F3). F4/F5 environmental, deferred.

### F1 — Remove false-green `UnknownVerb.test.goal`

Discussed with Ingi: a trace-based PLang assert needs either an inline
rebuild (flaky on `compress`) or a committed trace fixture (doesn't
catch live emission regressions). Both worse than deletion. The branch's
confidence-per-step feature is observable in build output (`⚠ planner
VeryLow`) — verified live during this work — that's a CLI-surface
assertion, not a `.test.goal` job.

Deleted `Tests/ConfidenceCheck/` (goal + committed mis-compiled `.pr`).

### F2 — Tagged property-lookup cache test

Was:

```csharp
Tagged.ClearCacheForTests();
var sizeBefore = Tagged.CacheSize;
new Data("", identity).Normalize();
var sizeAfter1 = Tagged.CacheSize;
new Data("", identity).Normalize();
var sizeAfter2 = Tagged.CacheSize;
await Assert.That(sizeAfter2).IsEqualTo(sizeAfter1);
```

Asserts a process-wide counter under TUnit parallel — racing Normalize
tests insert their own `(Type, View)` keys between reads. Replaced with
behaviour-the-test-owns:

```csharp
var first = Tagged.PropertiesFor(t, View.Out);
var second = Tagged.PropertiesFor(t, View.Out);
await Assert.That(object.ReferenceEquals(first, second)).IsTrue();
```

Same `(Type, Mode)` returns the same `IReadOnlyList<Entry>` reference —
exactly what `_cache.GetOrAdd` guarantees.

Also dropped `ClearCacheForTests()` and `CacheSize` from `Tagged` and
the one remaining call site in `DebugModeBypassTests` — the cache is
idempotent, no test needs to clear it, and the global clear was itself
a flake source for parallel neighbours.

### F3 — Channel recursion guard end-to-end test

Tester mutation-confirmed: deleting `_executing.Value = true` in
`InvokeGoal` left the existing 7 channel tests green, because each
flips the AsyncLocal via reflection rather than through a real
`InvokeGoal`. Added a PLang test:

```
Tests/Channels/GoalChannelRecursion/
├── Start.test.goal     # set channel "echo" call EchoBack; write to echo
│                        # on error → InspectError; assert errorKey
├── EchoBack.goal       # body: write %!data% to echo (recurses without guard)
└── InspectError.goal   # set %errorKey% = %!error.Key%
```

Test asserts `errorKey == 'ChannelNotFound'` (not just "any error") to
distinguish the guard from the engine's `RecursionDepthExceeded`
backstop.

**Mutation-confirmed both ways:**
- Initial draft used `set %guarded% = true` on error — passed under
  mutation (recursion-depth limit was the real catcher). Strengthened
  to assert on the specific error key.
- Final form: with `_executing.Value = true` → Pass. Commented out →
  Fail. Reverted; final code clean.

## Test runs

| Suite | Total | Pass | Fail | Notes |
|-------|-------|------|------|-------|
| C# (`dotnet run --project PLang.Tests`) | 3377 | 3377 | 0 | |
| PLang (`cd Tests && plang --test`) | 234 | 233 | 0 | 1 timeout — `DownloadSkip.test.goal` (Http, environmental) |

## F4 / F5 — not addressed

- **F4** `UploadFile.test.goal` 502: environmental. Same family as the
  `DownloadSkip` timeout in this run. Network-dependent Http tests need
  transport mocking or an integration-only tag — separate ticket.
- **F5** `y/n/a` permission prompt leak: hygiene issue in a permission-gate
  test fixture. Separate ticket.

## Files

- `PLang/app/channels/serializers/filters/Tagged.cs` — removed test-only API
- `PLang.Tests/App/DataTests/NormalizeTreeShapeTests.cs` — F2 fix
- `PLang.Tests/App/Serialization/DebugModeBypassTests.cs` — drop redundant clear
- `Tests/ConfidenceCheck/` — deleted (F1)
- `Tests/Channels/GoalChannelRecursion/` — added (F3, 3 files + .build/)

## Commit

`b86539c99 coder v2: address tester v1 findings on builder-ergonomics`

## Hand back to

Tester — verify F1 deletion is the right call (vs. some other reproduction
shape), F2's per-key reference test pins what it claims, and F3 catches
the guard regression under the same mutation protocol.
