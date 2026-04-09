# Security Audit Plan — runtime2-builder-v2-cleanup v1

## Scope

Full blue+red team security audit of the cleanup branch. 642 files changed, ~14.7k insertions. Major areas:

1. **Signing module** — SignedData redesign, Ed25519Provider, thread-safe ToSigningBytes
2. **HTTP module** — Size-limited reads, SSE buffer protection, signing integration
3. **Identity module** — Key persistence, export, [Sensitive] filtering
4. **Module/Provider loaders** — Assembly.LoadFrom without signature verification
5. **Engine core** — Data deserialization, depth guards, __condition__ removal, Variables
6. **Event module** — Consolidation (6→3 handlers), skipAction mechanism
7. **Condition module** — DefaultEvaluator exception handling
8. **File module** — Provider pattern, path validation
9. **Variable/List modules** — Type handling, bounds checking

## Approach

- Phase 1 (Blue): Map attack surface across all changed modules
- Phase 2 (Red): Exploit sketches for each finding
- Phase 3: Write security-report.json, verdict.json, summary

## Threat Model

PLang is user-sovereign. Trust boundary = cryptographic signatures on Data. Defend against untrusted external data, not the user. .pr files are trusted.
