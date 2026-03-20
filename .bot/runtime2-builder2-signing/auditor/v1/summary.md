# Auditor v1 — Summary

## What this is
Full security + correctness + coverage audit of the signing module, provider registry, identity refactor, and provider module.

## What was done
Three parallel analyses:
1. **Signing module deep-dive** — security (timing, nonces, signatures), edge cases, error handling
2. **Provider registry + identity** — thread safety, lifecycle, arbitrary code loading, null safety
3. **Test coverage gap analysis** — deletion test on all new code, comparing tests vs implementation

## Findings
- **4 critical**: future-timestamp expiry bypass, nonce replay after eviction, thread-unsafe `ToSigningBytes()`, arbitrary code exec via `provider.load`
- **5 high**: NPE in `verify.Run()`, unprotected casts in CreateAsync/VerifyAsync/GenerateIdentity, empty nonce accepted, TOCTOU in Register, rename partial-failure orphan
- **8 medium**: non-atomic SetDefault, algorithm confusion, type-lossy header comparison, unused load.Name, missing [Sensitive], silent deserialize null, unprotected ctor.Invoke, ExportAsync loses [Sensitive]
- **15 coverage gaps**: 6 security-critical (untested signature/hash/key guards), 3 major (ResolveType branches, UnknownType errors, list action), 6 moderate/minor

## Status
**FAIL** — Send back to coder. Recommended fix order in result.md.
