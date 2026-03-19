# Coder v6 Plan — Address Auditor v1 Findings

## Fixes

1. **IdentityData.cs** — Wrap `GetOrCreateDefaultAsync` in try/catch(InvalidOperationException). Return null on failure — IdentityData already handles null Value.
2. **export.cs** — Use `GetOrCreateDefaultAsync` for null-name path (consistent with Get handler). Add try/catch for the throw.
3. **Data.Envelope.cs** — Add `SensitivePropertyFilter.Filter` to `_envelopeJsonOptions` TypeInfoResolver.
4. **IdentityHandlerTests.cs** — Update `Export_NullName_NoDefault_ReturnsError` to expect success (auto-create).

## Skipped

- #4 (rename duplicate on RemoveAsync failure) — needs transactional DataSource
- #5 (throw convention nit) — moot after #1 fix
