# v1 Summary: Identity Module Test Stubs

## What this is

Test contract stubs for the PLang Runtime2 identity module — Ed25519 key pair management with CRUD operations (create, get, getAll, archive, unarchive, rename, setDefault, export), stored via System.DataSource. Also covers `[Sensitive]` attribute infrastructure and `%MyIdentity%` lazy resolver on MemoryStack.

## What was done

Created 59 test stubs across 14 files:

### C# Tests (49 tests)

- **`IdentityHandlerTests.cs`** (34 tests) — Handler CRUD: create (5 + duplicate name, duplicate archived name, empty name), get (4 + non-existent error, no-default-exists), getAll (2), archive (4), unarchive (3), rename (5), setDefault (3), export (2)
- **`IdentityVariableTests.cs`** (7 tests) — ToString, dot navigation for Name/PublicKey/Created/IsArchived/IsDefault/PrivateKey
- **`MyIdentityResolverTests.cs`** (5 tests) — Lazy resolver: auto-create, dot notation, string context, update after setDefault
- **`SensitivePropertyFilterTests.cs`** (5 tests) — [Sensitive]: excluded from output, included in storage, no-op without attribute, coexists with view attrs, E2E with IdentityVariable

### PLang Tests (10 tests)

- `IdentityCreate/` — create + %MyIdentity% resolution
- `IdentityGetByName/` — named identity retrieval
- `IdentitySwitchDefault/` — switch default, verify %MyIdentity% changes
- `IdentityArchiveNonDefault/` — archive non-default succeeds
- `IdentityArchiveDefault/` — archive default returns error
- `IdentityAutoCreate/` — auto-create on first access
- `IdentityDotNavigation/` — %MyIdentity.Name%, %MyIdentity.PublicKey%
- `IdentityExport/` — export private key
- `IdentityUnarchive/` — archive then unarchive, verify accessible
- `IdentityRename/` — rename identity, verify old name gone, new name works

## Evolution

1. **Initial (40 tests):** Core CRUD + [Sensitive] + resolver
2. **Gap review (+8 tests → 48):** Error paths, validation, missing property coverage, open questions flagged
3. **Architect v2 (+11 tests → 59):** Resolved questions, added unarchive/rename actions, renamed PrivateKey test

## Status

All 59 stubs written. Ready for coder to implement.
