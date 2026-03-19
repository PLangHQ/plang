# Builder V2 — Architect Summary

**v1**: Initial master plan. 8 pieces: identity → signing → http → template → llm → error extensions → build module → integration. See [v1/plan.md](v1/plan.md).

**v2**: Crypto module added as piece 2 (signing needs hashing). 9 pieces total. Identity revised to become opaque key vault — key generation moves to signing provider. Signing provider is swappable per-call → actor → engine default → Ed25519. Verification dispatches by message type, not settings. Contracts are pass-through consent strings. See [v2/plan.md](v2/plan.md).
