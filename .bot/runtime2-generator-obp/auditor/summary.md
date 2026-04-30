# auditor — runtime2-generator-obp

## v1 — 2026-04-30

First auditor pass. The branch had been through architect → coder/v1–v5 →
codeanalyzer/v3 (CLEAN on coder/v3) → tester/v4 (APPROVED on coder/v4) →
security/v1 (PASS on coder/v4). I'm the first reviewer of coder/v5
(security #1+#3 closures: `[Sensitive]` masking in __SnapshotParams +
cycle/depth-trip → ServiceError).

**Verdict: PASS** — 1 minor + 3 nit + 1 observation, no critical/major.
C# 2468/2468 green.

The dominant finding (Minor #1, cross-file) is that v5's new contract
(`Data.AsT_Impl` returns ServiceError on cycle/depth) is honored by legacy
property emission (`__Resolve<T>` → `__resolutionError` → ExecuteAsync
short-circuit) but **not** by Data<T> property emission, which assigns the
FromError-Data directly to the backing field. The handler's `Run()` reads
`.Value` (default(T)) and proceeds without seeing the error. The Data<T>
path is the forward-looking emission (Phase 5 will delete legacy), so the
inconsistency is more than cosmetic. Fix: mirror the legacy pattern
(single-line addition per Data<T> emission branch). Missed by security
(declared #3 fixed without checking the emission consumers honor it).

See [v1/summary.md](v1/summary.md) and [v1/result.md](v1/result.md).
