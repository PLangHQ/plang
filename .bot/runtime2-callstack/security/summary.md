# security — runtime2-callstack

## v2 — 2026-05-04 — PASS (3 v1 closed, 1 partial, 1 by-design open, 2 new low)

Re-audit after coder's response. Coder closed the v1 medium and 2 of
3 v1 lows cleanly via OBP refactor — `Audit`, `Errors.Trail`,
`Call.Errors`, `Call.Children` are all new domain classes wrapping
`List<T>` with private `_lock` + snapshot iteration. Tag fix is one
`lock(_tagsLock)` covering both lazy alloc and write. Stale `_root`
now reassigns on every top-level Push.

Partial: v1#3 — `Children` is fully fixed but `Diffs` is still
exposed as raw `public List<Diff>?` while writes go through a private
`_diffsLock`. Readers iterating during sibling-branch OnSet writes
race (F1 below).

Two new low residuals: F1 (Diffs reader race — recommend coder one
more pass to promote `Diffs` to a domain class mirroring the four
siblings, ~30-line mirror); F2 (`CallStack.Flags` torn read on
record-struct reassignment from Debug.Apply — accept as documented).

Zero medium open. Branch is mergeable; F1 is a polish pass for
consistency before parallel `goal.call` ships.

Details: [v2/summary.md](v2/summary.md). Findings:
[security-report.json](../security-report.json).

---

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

Details: [v1/summary.md](v1/summary.md).
