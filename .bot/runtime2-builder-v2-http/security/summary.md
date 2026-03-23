# Security Bot — runtime2-builder-v2-http

## v1 — Initial Security Audit (2026-03-23)

Full blue team + red team audit of HTTP module, provider registry, crypto/signing, and transport serialization. **FAIL** — 1 high (unbounded response body size), 3 medium (ToSigningBytes thread safety, SSE buffer, error body size), 4 low. Trust boundary (Ed25519 signatures) is solid. The systematic gap is zero size limits on untrusted external data. See [v1/summary.md](v1/summary.md) for details.
