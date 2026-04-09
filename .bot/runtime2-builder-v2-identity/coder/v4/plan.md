# Coder v4 Plan — Address Tester v2 Findings

## Fixes

1. **SensitivePropertyFilterTests.cs** — Fix flaky `Sensitive_IdentityVariable_PrivateKeyExcluded`: deserialize JSON back to `JsonElement` and compare property values instead of raw string `Contains()`. Use camelCase property names (`publicKey`, `name`) since `JsonStreamSerializer` uses `CamelCase` naming policy. Still check `DoesNotContain(identity.PrivateKey)` and its escaped form.
2. **IdentityHandlerTests.cs** — Add `Error.Key` assertion to whitespace case in `Create_EmptyOrWhitespaceName_ReturnsError`.
3. **IdentityHandlerTests.cs** — Add `Error.Key` assertion to missing case in `SetDefault_ArchivedOrMissing_ReturnsError`.
