# v1 Summary: Identity Module Test Stubs

## What this is

Test contract stubs for the PLang Runtime2 identity module — Ed25519 key pair management with CRUD operations (create, get, getAll, archive, setDefault, export), stored via System.DataSource. Also covers `[Sensitive]` attribute infrastructure and `%MyIdentity%` lazy resolver on MemoryStack.

## What was done

Created 48 test stubs across 12 files:

### C# Tests (40 tests)

- **`PLang.Tests/Runtime2/Modules/identity/IdentityHandlerTests.cs`** (24 tests) — Handler CRUD: create with key validation, default management, DataSource storage, duplicate name error, empty name error, get by name/null/auto-create/non-existent error/no-default-exists, getAll filtering, archive with error paths, setDefault + idempotent, export + non-existent error
- **`PLang.Tests/Runtime2/Modules/identity/IdentityVariableTests.cs`** (7 tests) — Type behavior: ToString returns public key, dot navigation for Name/PublicKey/Created/IsArchived/IsDefault, PrivateKey dot-nav blocked
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

### Tests added in gap review (v1.1)

8 new stubs added after gap analysis:

| # | Test | Category | Notes |
|---|---|---|---|
| 1 | `Create_DuplicateName_ReturnsError` | Critical | Architect Q: what about archived duplicates? |
| 2 | `Create_EmptyOrWhitespaceName_ReturnsError` | Medium | Name validation |
| 3 | `Get_NonExistentName_ReturnsError` | Critical | Missing error path |
| 4 | `Get_NullName_NoDefaultExists_AutoCreates` | Medium | No default but identities exist |
| 5 | `SetDefault_AlreadyDefault_IsIdempotent` | Critical | Edge case |
| 6 | `Export_NonExistentName_ReturnsError` | Critical | Missing error path |
| 7 | `DotNavigation_IsArchived_ReturnsIsArchived` | Medium | Missing property coverage |
| 8 | `DotNavigation_IsDefault_ReturnsIsDefault` | Medium | Missing property coverage |
| 9 | `DotNavigation_PrivateKey_IsBlocked` | Critical | Security — architect Q: blocked at var level or only serialization? |

## Open Questions for Architect

1. **Create duplicate name (archived)**: Should creating with an archived name re-create or error?
2. **PrivateKey dot navigation**: Should `%MyIdentity.PrivateKey%` be blocked at the variable level, or is `[Sensitive]` only a serialization concern?

## Status

All stubs written. Ready for architect to resolve open questions, then coder to implement.
