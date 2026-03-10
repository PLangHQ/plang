## v1

All 6 PLang test suites from the architect's plan already exist on runtime2 after merge. No new tests needed per architect's spec. See [v1/summary.md](v1/summary.md).

## v2

Independent analysis found 3 real gaps in onError modifier coverage: bare retry, timed retry (ms), GoalFirst order, and mixed strategies in one goal. Created 3 test suites (11 files). Also flagged that retry time should use ms not seconds. See [v2/summary.md](v2/summary.md).
