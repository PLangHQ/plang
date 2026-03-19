# Test Plan: Identity Module (v1)

## Context

Implementing test stubs for the identity module — Ed25519 key pair management with CRUD operations, stored via System.DataSource. Includes `[Sensitive]` attribute infrastructure and `%MyIdentity%` lazy resolver.

## Files to Create

### C# Test Files (49 tests)

| File | Tests | Purpose |
|------|-------|---------|
| `PLang.Tests/Runtime2/Modules/identity/IdentityHandlerTests.cs` | 34 | Handler CRUD: create, get, getAll, archive, unarchive, rename, setDefault, export + error paths |
| `PLang.Tests/Runtime2/Modules/identity/IdentityVariableTests.cs` | 7 | Type behavior: ToString, dot navigation (all props including PrivateKey) |
| `PLang.Tests/Runtime2/Modules/identity/MyIdentityResolverTests.cs` | 5 | Lazy resolver: auto-create, dot notation, update after setDefault |
| `PLang.Tests/Runtime2/Serializers/SensitivePropertyFilterTests.cs` | 5 | [Sensitive] attribute: excluded from output, included in storage |

### PLang Test Files (10 tests)

| Directory | Test | Purpose |
|-----------|------|---------|
| `Tests/Runtime2/IdentityCreate/` | Create + resolve | create identity, verify %MyIdentity% |
| `Tests/Runtime2/IdentityGetByName/` | Get by name | create named, get by name, verify props |
| `Tests/Runtime2/IdentitySwitchDefault/` | Switch default | two identities, switch, verify %MyIdentity% changes |
| `Tests/Runtime2/IdentityArchiveNonDefault/` | Archive non-default | archive succeeds, default unchanged |
| `Tests/Runtime2/IdentityArchiveDefault/` | Archive default = error | try archive default, verify error |
| `Tests/Runtime2/IdentityAutoCreate/` | Auto-create | access %MyIdentity% when none exist |
| `Tests/Runtime2/IdentityDotNavigation/` | Dot navigation | %MyIdentity.Name%, %MyIdentity.PublicKey% |
| `Tests/Runtime2/IdentityExport/` | Export key | export private key, verify non-empty |
| `Tests/Runtime2/IdentityUnarchive/` | Unarchive | archive then unarchive, verify accessible |
| `Tests/Runtime2/IdentityRename/` | Rename | rename identity, verify old name gone, new name works |

## Total: 59 tests

## Architect decisions (v2)

1. **Duplicate name = always error** (even archived). Names are identities, not labels. Developer must unarchive or pick a new name.
2. **[Sensitive] is serialization only** — `%MyIdentity.PrivateKey%` works via dot navigation. No access control at variable level.
3. **Two new actions:** `unarchive` (restore archived identity) and `rename` (change name, keep keys).

## Pattern

- C# tests follow SettingsDataTests.cs pattern (engine setup/teardown, direct handler invocation)
- PLang tests follow existing Runtime2 test conventions (Start goal, assert statements)
- All test bodies are stubs: `Assert.Fail("Not implemented")` (C#) or `- throw "not implemented"` (PLang)
