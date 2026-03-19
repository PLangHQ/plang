# v4 Summary — Fix Flaky Test + Weak Assertions

## What this is

Fix 3 tester v2 findings: 1 flaky test (base64 JSON escaping) and 2 weak error key assertions.

## What was done

**SensitivePropertyFilterTests.cs** — `Sensitive_IdentityVariable_PrivateKeyExcluded`: replaced raw `json.Contains(identity.PublicKey)` with `JsonElement` deserialization + property comparison. This avoids the `+` → `\u002B` escaping issue with random Ed25519 keys.

```csharp
// Before (flaky):
await Assert.That(json).Contains(identity!.PublicKey);

// After (stable):
var deserialized = JsonSerializer.Deserialize<JsonElement>(json);
await Assert.That(deserialized.GetProperty("publicKey").GetString()).IsEqualTo(identity!.PublicKey);
```

**IdentityHandlerTests.cs** — Added `Error.Key` assertions:
- `Create_EmptyOrWhitespaceName_ReturnsError`: added `IsEqualTo("ValidationError")` for whitespace case
- `SetDefault_ArchivedOrMissing_ReturnsError`: added `IsEqualTo("NotFound")` for missing case

## Files modified

- `PLang.Tests/Runtime2/Serializers/SensitivePropertyFilterTests.cs`
- `PLang.Tests/Runtime2/Modules/identity/IdentityHandlerTests.cs`

## Verification

All 1647 tests pass (0 failures).
