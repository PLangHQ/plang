# Auditor v2 Summary — runtime2-builder2-signing

## What this is
Re-audit after coder v3 fixed the critical finding from auditor v1 (IdentityData silent error swallowing).

## What was done
Reviewed the fix in `IdentityData.ResolveDefault()` and the test in `IdentityErrorPathTests`. Ran all 1827 tests (pass).

### Fix Assessment
The coder chose to **throw** `InvalidOperationException` rather than store on `Data.Error`. This is the right call:
- Throwing fails loud at the point of use — no silent nulls
- Storing on `Data.Error` would still give callers null `Value`, which is what we were trying to prevent
- Identity resolution is a critical system invariant — if it fails, the engine shouldn't silently continue
- The exception message includes the error key and message from the underlying failure, giving full diagnostic context

### Test Coverage
- `IdentityData_ResolveDefault_SaveFails_Throws` — verifies path #2 (DataSource save failure during auto-create), asserts message contains "Identity resolution failed" and "IOError"
- Tester v3 noted path #1 (no provider registered) is untested — acceptable since `IIdentityProvider` is always registered in Engine constructor

### 2 nits from v1 remain acceptable as-is
- NowUtc cast without null guard — safe in practice
- ToSigningBytes save-mutate-restore — safe in PLang's single-threaded model

## Verdict
**PASS** — Ready for docs bot.
