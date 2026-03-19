# Auditor v1 Plan — Crypto Module

## Prior Reviewer Verdicts
- **Codeanalyzer v2**: PASS — all findings resolved, providers return Data
- **Tester v4**: PASS — all error paths covered, 1701 tests green
- **Security v1**: PASS — 1 medium (timing side-channel), 3 low (accepted-risk)

## What I Won't Re-check
- OBP compliance per-file (codeanalyzer did thorough per-file analysis)
- Test assertion quality (tester did 4 rounds including fresh-eyes)
- Attack surface mapping (security did blue/red team)

## What I Will Focus On

### 1. Cross-File Contracts
- Does `Engine.Providers` integrate correctly with `Engine.@this`? Is disposal handled?
- Does `Hash.ResolveProvider()` share correctly with `Verify`? Any divergence risk?
- Do the static helper methods on `Hash` (`SerializeData`, `FormatHash`, `ResolveProvider`) create coupling issues?
- Identity module: does `IdentityData.ResolveDefault()` → `GetOrCreateDefaultAsync()` → `SaveAsync()` chain handle ALL error paths end-to-end?

### 2. Architectural Fit
- Engine.Providers: is `ConcurrentDictionary<Type, object>` the right choice? Does the `new DefaultProvider()` allocation on every call in `ResolveProvider()` matter?
- Are crypto handlers following the same patterns as identity handlers?
- Does `HashedData` as a plain class (not Data) fit the runtime's type system?

### 3. Review Quality Assessment
- Did codeanalyzer's initial miss (providers throwing instead of Data) indicate a pattern gap?
- Did the tester's v2 null Hash finding expose a wider class of null-input bugs?
- Did security correctly rate the timing side-channel given PLang's threat model?

### 4. Foundation Ripple
- `Engine.Providers` is new infrastructure on Engine. What's the disposal story?
- `Data.Envelope._envelopeJsonOptions` now includes `SensitivePropertyFilter` — does this affect Compress/Decompress round-trips?

### 5. Timing Side-Channel (Security Finding 1)
- Verify the fix was NOT applied. Security said PASS with recommendation. Is that the right call?

## Deliverables
- `auditor-report.json` at `.bot/runtime2-builder-v2-crypto/`
- `verdict.json` at `.bot/runtime2-builder-v2-crypto/auditor/v1/`
- `summary.md` at `.bot/runtime2-builder-v2-crypto/auditor/v1/`
- `summary.md` at `.bot/runtime2-builder-v2-crypto/auditor/` (cross-session)
