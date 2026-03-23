# Security Bot — runtime2-builder-v2-http

## v1 — Initial Security Audit (2026-03-23)

Full blue team + red team audit. **FAIL** — 1 high (unbounded response body), 3 medium (ToSigningBytes thread safety, SSE buffer, error body). Sent to coder. See [v1/summary.md](v1/summary.md).

## v2 — Re-audit After Fixes (2026-03-23)

Verified all 4 fixes: size-limited reads (MaxResponseSize 100MB), SSE buffer cap (10MB), error body truncation (4KB), thread-safe ToSigningBytes (TypeInfoResolver modifier). **PASS** — 0 critical, 0 high, 0 medium open. 5 low accepted-risk. See [v2/summary.md](v2/summary.md).
