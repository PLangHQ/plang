# Code Analysis v2 — Re-review after coder fixes

## Scope
Re-review of 4 files changed by coder in response to v1 findings + Ingi's feedback.

## PLang/App/modules/crypto/providers/ICryptoProvider.cs

Returns `Data` now. Clean.

### Verdict: CLEAN

---

## PLang/App/modules/crypto/providers/DefaultProvider.cs

All v1 findings resolved:
- Returns `Data` instead of throwing — correct
- `Verify` simplified — calls `Hash()`, checks `.Success`, clean flow
- No redundant algorithm validation — single source of truth in `Hash()`
- No double `ToLowerInvariant()` — only one call in `Hash()` switch

### Verdict: CLEAN

---

## PLang/App/modules/crypto/hash.cs

v1 findings status:
- **Provider exceptions → Data errors**: Fixed at the interface level. Handler no longer needs try/catch for provider errors. Checks `.Success` on provider result.
- **`new DefaultProvider()` per call (line 54)**: Still present. Low priority — stateless, not hot path.
- **`Data` property shadowing**: Still present. Design trade-off, not a bug.

No new issues introduced by the fix.

### Verdict: CLEAN
Remaining items are low-priority design trade-offs, not correctness issues.

---

## PLang/App/modules/crypto/verify.cs

Clean. The only remaining `try/catch` is for `Convert.FromHexString` — a .NET framework call that throws `FormatException`. This is a correct system boundary catch.

Provider result flows through directly: `return provider.Verify(bytes, hashBytes, Algorithm)`.

### Verdict: CLEAN

---

## Test Changes

Tests properly updated:
- `ThrowingCryptoProvider` → `FailingCryptoProvider` returning `Data.FromError`
- Tests assert error keys (`"ProviderError"`) not exception types
- `DefaultProviderTests` updated to check `result.Success` and cast `result.Value`
- `Verify_UnknownAlgorithm_ReturnsError` checks `result.Success` is false + error key, not `Throws<>`

### Verdict: CLEAN

---

## Overall Verdict: PASS

All findings from v1 and Ingi's review are resolved. The crypto module is clean — providers return `Data`, errors flow through the pipeline, tests verify error propagation. The two remaining low-priority items (`new DefaultProvider()` per call, `Data` property name shadowing) are acknowledged design trade-offs.
