# Security — runtime2-generator-obp

- **v1** ([details](v1/summary.md)) — First security pass on the v4
  generator-OBP refactor. Reviewed `Data.As<T>(context)` resolution
  (cycle + depth protection solid), source-generator emission (trust
  boundary OK at build time), `App.Run` catch filter (preserves NRE/OOM/
  SOE correctly), JSON ingestion depth guards. **PASS**: 1 medium + 3 low,
  no critical/high. Dominant finding: `__SnapshotParams` ignores
  `[Sensitive]` on action parameter properties — recurring info-disclosure
  pattern, currently dormant. Recommended **auditor** next.
- **v2** ([details](v2/summary.md)) — Re-audit on the Variable +
  IRawNameResolvable migration (architect/v5 → coder/v7) and confirm v1
  closure. **v1 #1 + #3 fixed** (`__SnapshotParams` masks `[Sensitive]`;
  cycle/depth → `FromError`). New surface: `Data.AsT_Impl` reflection
  bypass for marker types — narrow, signature-exact, no exposure
  widening. Dominant new finding: 19/22 migrated handlers carry no
  `[IsNotNull]` guard, so null/missing Name slots NRE-crash where v6
  returned graceful `ServiceError` — robustness regression from
  architect-vs-implementation drift. **PASS**: 4 low (1 new + 1 contract
  trap + 1 informational + 1 standing v1 #2), no critical/high.
  Recommended **auditor** next; or **coder** if finding 1 should be
  closed first via generator-side not-null check.
