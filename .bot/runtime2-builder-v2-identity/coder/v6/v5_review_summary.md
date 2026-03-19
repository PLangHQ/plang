# v5 Review Summary (Auditor v1)

1. **Major #1** — `IdentityData.ResolveDefault()` has no try/catch around `GetOrCreateDefaultAsync` which throws on save failure. Lazy `%MyIdentity%` access crashes with unhandled exception.
2. **Minor #2** — `Export(null)` uses its own `LoadAll+Find` while `Get(null)` uses `GetOrCreateDefaultAsync` with promotion. Behaviors diverge on same concept.
3. **Minor #3** — `Data.Envelope._envelopeJsonOptions` missing `SensitivePropertyFilter`. Theoretical future leak if `Compress()` is used with identity data.
4. **Minor #4** — Rename: RemoveAsync failure after SaveAsync creates unrecoverable duplicate. Known limitation, needs transactional DataSource. **Skipped.**
5. **Nit #5** — Throw convention violation in `GetOrCreateDefaultAsync`. Related to #1. **Skipped** — fixing #1 makes this moot.
