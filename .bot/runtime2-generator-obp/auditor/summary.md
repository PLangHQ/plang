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

## v2 — 2026-05-01

Cumulative audit on the Variable + IRawNameResolvable migration (coder/v6
auditor closure + architect/v5 → coder/v7 commits 1–4). Reviewers prior:
codeanalyzer/v4 (PASS, 3 MINOR + 7 NIT), tester/v7 (PASS, 4 minor),
security/v2 (PASS, 4 low). All file-level work is honest; the gap is
between the architect's plan and the per-handler `[IsNotNull]` distribution.

**Verdict: FAIL** — 1 major + 2 minor. C# 2550/2550 green; plang 166/166
green.

Major #1 (cross-file): The deleted `RawScalarValidations` block (which
emitted `MissingParameter` ServiceError for null/empty `[VariableName]`
slots pre-v7) was supposed to be replaced by `[IsNotNull]` per the
architect/v5 plan. Reality: **0 of 22** Data<Variable> slots carry
`[IsNotNull]` (security/v2 said "3 of 22" but those decorations are on
OTHER properties in the same handlers). Post-v7 a missing or null Name
slot resolves to `Data<Variable>{ Value=null, Success=true }`,
`__resolutionError` is never set, handler reads `Name.Value` (null
Variable), `op_Implicit` dereferences null, NullReferenceException — caught
not by App.Run (whose catch excludes NRE) but by Step.RunAsync as
`ServiceError("Object reference not set...", "StepError", 400)` — without
parameter name, step text, or Params snapshot. Empirically reproduced.

Fix is generator-side (~10 lines + 1 Discovery flag in
`Emission/Property/Data/this.cs:EmitProperty`, mirroring `IsSensitive`
plumbing).

Minors #2 + #3 are review-gap findings explaining how the major slipped
through. Hand-off: coder.

See [v2/summary.md](v2/summary.md) and [v2/result.md](v2/result.md).
