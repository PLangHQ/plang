# auditor v2 — plan

## What changed since v1

v1 reviewed coder/v5 (security closures: `[Sensitive]` masking + cycle/depth →
ServiceError) and passed with 1 minor + 3 nit. Since then:

- **v6** — auditor/v1 finding #1 closed (Data<T> property emission now surfaces
  cycle/depth FromError into `__resolutionError`)
- **architect/v5 → coder/v7 (3 commits + cleanup + commit 4)** — the
  Variable + IRawNameResolvable migration. Replaces `[VariableName] string`
  with `Data<Variable>` across 22 handlers, deletes the Legacy property
  emitter + `__StripPercent` / `__Resolve<T>` / `__HasParam` helpers + the
  `RawScalarValidations` block + the `[VariableName]` attribute.
- **codeanalyzer/v4** — PASS with 3 MINOR + 7 NIT (no MAJOR).
- **tester/v7** — PASS, 4 minor findings (missing C# coverage for variable.set
  CopyProperties; IRawNameResolvable contract trap untested; misnamed PLNG001
  test; WasPercentWrapped value-only pinning).
- **security/v2** — PASS, 4 low findings (the dominant one: 19/22 handlers
  unguarded against null/missing Name slots → NRE post-v7 vs graceful
  ServiceError pre-v7).

## What I'll focus on (cross-cutting integrity)

The v7 migration deleted a safety net (`RawScalarValidations`) on the
architect's claim that `[IsNotNull]` would cover the missing-parameter case.
Security flagged that 3 of 22 handlers carry `[IsNotNull]` and rated it
**low**. That framing suggests "almost all are guarded" — worth verifying.

Codeanalyzer didn't trace this contract gap because their pass is
file-by-file (the spec was the architect's plan, not in any single file's
boundaries). Tester didn't write a regression test for the missing-parameter
path. Security caught the shape but understated severity.

The auditor's question: **Does pre-v7's `MissingParameter` ServiceError
diagnostic survive at all post-v7, and at what fidelity?** The fix is
generator-side (one shot — emit a not-null check on `Data<T>` whose
`T : IRawNameResolvable`) or per-handler (`[IsNotNull]` ×22, manual). Either
the safety-net migration is complete or it isn't.

### Specific checks
1. Confirm the carve-out's null path empirically: construct a Data with
   raw=null, call `.As<Variable>(ctx)`, observe the resulting
   Data<Variable>'s Success, Value, error.
2. Audit the 22 migrated handlers — count exactly how many declare
   `[IsNotNull]` on the `Data<Variable>` slot specifically (security's "3 of
   22" counts handlers with `[IsNotNull]` anywhere, not on the Variable slot).
3. Trace the NRE path: handler.Run() → op_Implicit → null deref → which
   catch fires? App.Run line 415 excludes NRE; Step.RunAsync line 157 does
   NOT exclude NRE. Confirms the regression's user-visible shape (less
   informative ServiceError vs. uncaught crash).
4. Verify `tester/v7`'s test counts (2550 / 166).

## Hand-off plan

If I find a MAJOR (likely on the [IsNotNull] count + missing diagnostic):
verdict **fail**, hand to coder for either generator-side fix or per-handler
[IsNotNull]. If everything checks out (security accurately reflects state,
codeanalyzer caught the right things, tester adequately covered):
verdict **pass**, hand to docs.
