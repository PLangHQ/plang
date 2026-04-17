# Security v2 Summary

## What this is
Re-audit of the runtime2-builder-plan branch after the coder fixed all 11 open security findings from v1. Includes both fix verification and a fresh-eyes scan for anything v1 missed.

## What was done

### Fix verification (all 11 PASS)
Every v1 fix was verified by reading the actual implementation (not just the diff):
- **F1** (download size limit): `maxBytes` param on `StreamWithProgressAsync`, configurable via `MaxDownloadSize`
- **F2** (SSE overflow): `consecutiveOverflows` counter, disconnects after 3, resets on success
- **F3** (slow-loris): Throughput check (1KB/sec over 30s) in all 3 stream readers
- **F5/F6** (JSON guards): `MaxElementCount=100K` + `MaxDepth=64` with ref counter threading
- **F7** (timing): `CryptographicOperations.FixedTimeEquals` on UTF-8 encoded header bytes
- **F8** (nonce replay): Cache TTL = timeout, timestamp check bounds replay window
- **F9/F12** (info disclosure): `path.Raw` replaces `path.Absolute`, type names removed
- **F10** (breadth): `MaxResolveItems=100K` with reset at top-level call
- **F11** (URL scheme): `Uri.TryCreate` + scheme whitelist

### Fresh-eyes scan — 3 new findings
1. **F13 [MEDIUM]** — `ResolveDeep()` lacks `skipInfrastructure` parameter. External data containing `%!app.System.SettingsStore%` would be resolved, leaking infrastructure state. `Resolve()` has this guard but `ResolveDeep()` does not.
2. **F14 [LOW]** — Throughput threshold (1KB/sec) is generous. At threshold, 100MB download takes 28 hours. Suggest making configurable.
3. **F15 [LOW]** — No SSRF IP filtering. `http://169.254.169.254` passes scheme check. Suggest optional config flag for server-mode apps.

### Also confirmed secure (fresh scan)
- Action dispatch: string-based lookup against module registry, no arbitrary type instantiation
- Parameter conversion: whitelist of ~50 safe types
- JSON deserialization: strongly typed, no `$type` polymorphism
- Event system: goal-name-only bindings
- Variable injection: flat dictionary, no path traversal possible
- Header injection: CRLF stripping correct
- Regex patterns: no ReDoS risk

## Verdict
**PASS** — No critical or high findings open. 1 medium + 2 low remain as hardening opportunities.

## Recommendation
Run the **auditor** next for final code quality review before merge.
