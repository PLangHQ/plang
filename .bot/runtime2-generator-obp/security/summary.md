# Security — runtime2-generator-obp

- **v1** ([details](v1/summary.md)) — First security pass on the v4
  generator-OBP refactor. Reviewed `Data.As<T>(context)` resolution
  (cycle + depth protection solid), source-generator emission (trust
  boundary OK at build time), `App.Run` catch filter (preserves NRE/OOM/
  SOE correctly), JSON ingestion depth guards. **PASS**: 1 medium + 3 low,
  no critical/high. Dominant finding: `__SnapshotParams` ignores
  `[Sensitive]` on action parameter properties — recurring info-disclosure
  pattern, currently dormant. Recommended **auditor** next.
