# v1 Summary: Identity Module Test Stubs

## What this is

Test contract stubs for the PLang Runtime2 identity module — Ed25519 key pair management with CRUD operations (create, get, getAll, archive, setDefault, export), stored via System.DataSource. Also covers `[Sensitive]` attribute infrastructure and `%MyIdentity%` lazy resolver on MemoryStack.

## What was done

Created 40 test stubs across 12 files:

### C# Tests (32 tests)

- **`PLang.Tests/Runtime2/Modules/identity/IdentityHandlerTests.cs`** (18 tests) — Handler CRUD: create with key validation, default management, DataSource storage, get by name/null/auto-create, getAll filtering, archive with error paths, setDefault, export
- **`PLang.Tests/Runtime2/Modules/identity/IdentityVariableTests.cs`** (4 tests) — Type behavior: ToString returns public key, dot navigation for Name/PublicKey/Created
- **`PLang.Tests/Runtime2/Modules/identity/MyIdentityResolverTests.cs`** (5 tests) — Lazy resolver: auto-create on first access, dot notation, string context, update after setDefault
- **`PLang.Tests/Runtime2/Serializers/SensitivePropertyFilterTests.cs`** (5 tests) — [Sensitive] attribute: excluded from output serialization, included in raw/storage, no-op on types without it, coexists with other attributes, end-to-end with IdentityVariable

### PLang Tests (8 tests)

- `Tests/Runtime2/IdentityCreate/` — create + %MyIdentity% resolution
- `Tests/Runtime2/IdentityGetByName/` — named identity retrieval
- `Tests/Runtime2/IdentitySwitchDefault/` — switch default, verify %MyIdentity% changes
- `Tests/Runtime2/IdentityArchiveNonDefault/` — archive non-default succeeds
- `Tests/Runtime2/IdentityArchiveDefault/` — archive default returns error
- `Tests/Runtime2/IdentityAutoCreate/` — auto-create on first access
- `Tests/Runtime2/IdentityDotNavigation/` — %MyIdentity.Name%, %MyIdentity.PublicKey%
- `Tests/Runtime2/IdentityExport/` — export private key

## Code example

C# handler test pattern (same as SettingsDataTests):

```csharp
[Test]
public async Task Create_GeneratesValidEd25519KeyPair()
{
    // Keys are non-null, base64-decodable, correct lengths (32 bytes public, 32 bytes private)
    Assert.Fail("Not implemented");
}
```

PLang test pattern:

```plang
Start
/ Test: create an identity and verify %MyIdentity% resolves to its public key
- throw "not implemented"
```

## Status

All stubs written. Ready for coder to implement production code and fill in test bodies.
