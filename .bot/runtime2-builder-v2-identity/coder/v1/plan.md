# Coder v1 Plan — Identity Module Implementation

## Overview
Implement the identity module per architect v2 plan: Ed25519 key pair management with 8 CRUD actions, `[Sensitive]` attribute infrastructure, `%MyIdentity%` lazy resolver, and 59 tests (49 C# + 10 PLang).

## Phases

### Phase 1: [Sensitive] Infrastructure
1. Add `SensitiveAttribute` to `Engine/View.cs`
2. Create `SensitivePropertyFilter.cs` — always-on filter stripping `[Sensitive]` properties
3. Wire into `JsonStreamSerializer.cs` default options

### Phase 2: Identity Types
4. `types.cs` — `IdentityVariable` with OBP persistence (LoadAsync, LoadAllAsync, SaveAsync, RemoveAsync)
5. `KeyGenerator.cs` — Ed25519 via NSec

### Phase 3: IdentityData + Actor Integration
6. `IdentityData.cs` — Data subclass, lazy resolution of default identity
7. `Actor.cs` — add lazy `Identity` property + register `%MyIdentity%` DynamicData

### Phase 4: 8 Handlers
8. create, get, getAll, archive, unarchive, rename, setDefault, export

### Phase 5: C# Tests (49 bodies)
### Phase 6: PLang Tests (pending builder prompt)

## Key Design
- Storage: `System.DataSource` table `"identity"`
- IdentityData on Actor: lazy-loaded, handlers call `.Update()` after changing default
- `%MyIdentity%` = `DynamicData("MyIdentity", () => engine.System.Identity.Value)`
- Error keys: NotFound, DuplicateName, ValidationError, CannotArchiveDefault
