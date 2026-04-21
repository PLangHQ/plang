# Tester v4 — Plan

## Context

Coder v2 (commit 730bfce0) addressed all 17 of my v3 findings. Full suite claim: 2267/2268 passing, +24 new tests, 0 regressions. My job: fresh-eyes validation.

## Approach — fresh eyes, not just verification

My v3 suggested specific fix shapes. The coder followed them. That's the **highest risk of false greens** — review-driven code is where the coder knows the exact assertion pattern the reviewer will run.

So rather than just "did they add the tests I asked for?" I'll:

1. **Suite run** — C# + PLang, record pass/fail, compare to v3 baseline.
2. **Coverage delta** — re-run `dotnet-coverage` on changed files; my v3 captured cobertura, diff it.
3. **Read EVERY new/modified test with deletion-test discipline** — "if I deleted the assertion block, would anything fail?"
4. **Trace ChildAppCreated hook** — this is production code added for tests. Must not leak subscribers, must not break production paths. Audit where it's raised and who subscribes.
5. **Audit the Executor.Configure split** — internal-for-tests. Verify no behavior change for `Run`. Check `Configure` returns error correctly without side effects.
6. **Check the 19 .test.goal stubs** — they are still stubs (coder admitted). How does the runner handle them? Will `plang --test` mark them as Stale? If Stale counts as "ok", we have another class of false green.
7. **SplitAtConditions guard test** — this is the critical finding #2. Does the new OrchestrateBranchCoverageTests actually reproduce the phantom-site scenario? If I revert `d05c138d` (the fix), does the test fail?
8. **Cross-check findings mapping** — each v3 finding → which v2 commit/test addresses it. Flag any that were claimed-addressed but actually punted.

## Risks to watch

- **ChildAppCreated is a static event** — TUnit parallel tests mean subscribers from previous tests can still be alive; did the coder unsubscribe? A leaked subscriber that throws would surface as an unrelated test failure.
- **Renamed .goal files** — if any had `.pr.json` already, those are orphaned now. If some tests referenced the old name explicitly, they'd break.
- **The `Configure/Start` split** — `internal` means it's accessible from `PLang.Tests` (InternalsVisibleTo). But: was `Run` previously doing things in order that `Configure` breaks? E.g. if `Run` did `Configure → check something → Start` but `Start` now assumes Configure was successful without re-checking?
- **Tautology fixes** — the v2 summary says "probe via `childApp.AbsolutePath.StartsWith(_tempDir)`". If the probe only fires for tests that actually spawn a child, but the original bug was about inheritance, the probe is contingent on the very thing being tested. Verify the probe proves inheritance under failure.

## Deliverables

- `v4/coverage.cobertura.xml` — fresh coverage
- `v4/result.md` — detailed findings  
- `v4/summary.md` — session summary
- `v4/verdict.json` — pass/fail
- `v4/changes.patch` — git diff runtime2..HEAD
- `.bot/runtime2-test-module/test-report.json` — overwrite with v4 results
- Bot root `summary.md` — append v4 line

## Verdict policy

- **pass** if: every v3 finding is genuinely addressed (fix works AND would fail if the bug returned), no regressions, no new false greens introduced.
- **needs-fixes** if: any finding was claimed-addressed but the test is a tautology/false-green; any regression; any new false-green in the v2 tests.

I expect to be tough on the tautology-fix tests (findings #5-7) since the coder was told exactly what shape I wanted — if they followed the letter but not the spirit, I'll call it.
