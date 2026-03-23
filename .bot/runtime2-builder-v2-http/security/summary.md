# Security Bot — runtime2-builder-v2-http

## v1 — Initial Security Audit (2026-03-23)

Full blue team + red team audit of HTTP module, provider registry, crypto/signing, and transport serialization. **PASS** — 0 critical, 0 high, 2 medium (response size limit, ToSigningBytes thread safety), 5 low. Trust boundary (Ed25519 signatures) holds correctly. See [v1/summary.md](v1/summary.md) for details.
