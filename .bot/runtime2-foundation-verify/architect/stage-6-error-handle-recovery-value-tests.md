# Stage 6: error.handle Recovery-Value Tests

**Goal:** Lock in the symmetry between `Order=GoalFirst` and `Order=RetryFirst` for the recovery-value flow, plus the "last action in a multi-action recovery is what propagates" semantic. Without these tests, a future "simplification" of `error.handle.Wrap` could regress the value path silently.

**Scope:** PLang `.test.goal` tests only. No C# code changes. No design changes. The behavior is already correct in `PLang/App/modules/error/handle.cs` (lines 109–134) — these tests pin it.

**Deliverables:** Three new `.test.goal` files under `Tests/Errors/` (or a new `Tests/Error/RecoveryValue/` subfolder if the test-designer prefers; tester picks). Each test should fail informatively if the value path regresses.

**Dependencies:** None. error.handle.Wrap is wired and working today.

## Design

Three behaviors to assert. Each gets its own minimal `.test.goal`:

### Test 1 — GoalFirst returns the recovery's value

`Order=GoalFirst` says "try the recovery actions first; if they succeed, return their value." When the recovery's last action produces a value (e.g. `read fallback.txt, write to %content%`), the *step result* must equal that recovery value — not null, not the original failed action's value, not the error.

Shape:

```
Start
- read 'missing.txt', on error, read 'fallback.txt', write to %content%, order: GoalFirst
- assert %content% equals "fallback contents"
```

Then a sibling test goal sets up `fallback.txt` and runs `Start`.

### Test 2 — RetryFirst returns the recovery's value (after retry exhausts)

Same as test 1 but with `Order=RetryFirst` and a retry count of 1 (or `Retry=1`). The retry exhausts (file is still missing), then recovery runs, and the recovery value must propagate.

```
Start
- read 'missing.txt', on error, read 'fallback.txt', write to %content%, order: RetryFirst, retry: 1
- assert %content% equals "fallback contents"
```

This is the case the 2026-04-27 symmetry fix unblocked — before that, RetryFirst returned `Ok()` instead of `recoveryResult`. The test exists specifically so a regression to `Ok()` would fail loudly.

### Test 3 — Last action in a multi-action recovery is what propagates

Recovery is a chain. The final action in the chain is what `RunRecovery` returns (line 184 in `handle.cs`). Test pins that semantic.

```
Start
- read 'missing.txt', on error, write out "log: trying fallback", read 'fallback.txt', write to %content%
- assert %content% equals "fallback contents"
```

Three actions in the recovery chain (`output.write`, `file.read`, `variable.set`). The first two run and discard their results; the chain's final returned Data is the `variable.set`. The test asserts `%content%` got the value from the chain's last write — proving the final-action semantic.

## Notes for test-designer / coder

- Each test should be **self-contained** — no shared fixtures. Create `fallback.txt` inline if needed (via `file.write`), then exercise the recovery, then assert.
- Use `assert` from the assert module — these are negative-path tests, so the assertions are what catches regression.
- Follow the existing `Tests/Errors/SimpleRecovery/` shape — one `Start.goal` per behavior, one `.test.goal` next to it asserting the post-state.
- Builder may need a fresh `plang build` if the on-error / order / retry syntax surfaces new builder paths.
- These three tests are small (~20 lines of .goal each). One `coder` session can knock all three out.

## What this is NOT

- Not a redesign of `error.handle`. The behavior is correct today.
- Not a stage that touches C#. Pure test addition.
- Not a verification of every error.handle parameter combination (StatusCode, Key, Message, IgnoreError, etc.) — only the recovery-value path, which is the symmetric one the 2026-04-27 fix unblocked.
