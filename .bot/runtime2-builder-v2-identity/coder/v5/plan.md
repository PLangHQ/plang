# Coder v5 Plan — Fix Auto-Create Overwrite

## Fixes

1. **types.cs** — `GetOrCreateDefaultAsync`: before auto-creating, check for any existing non-archived identity and promote it to default instead. Only auto-create when no identities exist at all.
2. **IdentityHandlerTests.cs** — Update `Get_NullName_NoDefaultExists_AutoCreates` to expect promotion. Add `GetOrCreateDefault_ExistingNonDefault_PromotesInsteadOfOverwriting` test. Add `Export_NullName_NoDefault_ReturnsError` test.
