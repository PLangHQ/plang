# security — runtime2-callstack

## v1 — 2026-05-04 — PASS (5 findings)

First security pass on the callstack refactor (architect/v1 plan,
landed across coder/v1 + coder/v2). Verdict **pass**: 5 findings, none
critical or high.

Key issue is medium: `stack.Audit.Add` and `app.Errors.All.Add` are
shared, run-wide, and unsynchronized — concurrent failures under
`Task.WhenAll` on goal.call (the architect plan's stated parallelism
target) race the same List<T>.Add. Should be fixed before parallel
goal.call ships. Also: Tag dict races, public-list lock targets,
unbounded growth in audit accumulators, stale `_root` across runs.

Two pre-existing standing findings carried forward without widening
(`FormatVerboseValue` doesn't strip [Sensitive]; `AssertSnapshot`
captures raw Variables onto AssertionError).

Details: [v1/summary.md](v1/summary.md). Findings:
[security-report.json](../security-report.json).
