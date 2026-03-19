# Piece 1: Identity Module — Architecture Plan (v2)

## Changes from v1

1. **Duplicate name = always error** (even if archived). Names are identities, not labels.
2. **`[Sensitive]` is serialization only** — `%MyIdentity.PrivateKey%` works via dot navigation.
3. **Two new actions:** `unarchive` (restore archived identity) and `rename` (change name, keep keys).
4. **Test corrections:** `DotNavigation_PrivateKey_IsBlocked` → `DotNavigation_PrivateKey_ReturnsPrivateKey`. New tests for rename, unarchive, and duplicate-archived-name error.

Everything else from v1 is unchanged — storage, [Sensitive] infrastructure, lazy resolver, key generation.

---

## Overview

Key pair management for PLang apps. Every PLang app can have multiple identities (public/private key pairs). The System actor owns stored identities; User and Service actors receive identities from the outside (HTTP/signing layer sets them).

## Design Decisions

### Storage: System.DataSource, not raw SQLite

The identity module uses `System.DataSource` with table `"identity"`, keyed by name. This:
- Follows the settings module pattern exactly
- Respects testing/building context (in-memory SQLite when `Testing.IsEnabled`)
- Avoids a second database connection
- Gets table auto-creation for free

### One type: IdentityVariable

```csharp
public class IdentityVariable
{
    public string Name { get; set; }
    public string PublicKey { get; set; }

    [Sensitive]
    public string PrivateKey { get; set; }

    public bool IsDefault { get; set; }
    public bool IsArchived { get; set; }
    public DateTime Created { get; set; }

    public override string ToString() => PublicKey;
}
```

- `[Sensitive]` on PrivateKey — included in DataSource storage, excluded from output serialization
- `[Sensitive]` does NOT block dot navigation — `%MyIdentity.PrivateKey%` returns the private key
- `ToString()` returns PublicKey — so `%MyIdentity%` in string context gives the public key
- Dot navigation works for all properties: `%MyIdentity.Name%`, `%MyIdentity.PublicKey%`, `%MyIdentity.PrivateKey%`, `%MyIdentity.Created%`, `%MyIdentity.IsDefault%`, `%MyIdentity.IsArchived%`
- No separate internal/external types. One object, one truth.

### Name uniqueness — absolute, even across archived

A name is an identity. Creating "alice" when an archived "alice" exists is an error — the developer must unarchive it or pick a different name. This prevents key confusion where the same name maps to different key pairs at different times.

### Special variables — per-actor, not shared

| Variable | Actor | Source | Auto-create? |
|----------|-------|--------|-------------|
| `%MyIdentity%` | System | DataSource (stored) | Yes, on first access |
| `%Identity%` | User | Set by HTTP/signing layer | No |
| `%ServiceIdentity%` | Service | Set by HTTP/signing layer | No |

Only `%MyIdentity%` has a lazy resolver. `%Identity%` and `%ServiceIdentity%` are plain MemoryStack values set by the HTTP/signing modules (Pieces 2-3). The identity module does not manage them.

### Key generation: Ed25519, internal

Uses NSec.Cryptography (already referenced). Key generation is an identity concern — no dependency on the signing module. The signing module (Piece 2) depends on identity for private keys, not the other way.

## New infrastructure: `[Sensitive]` attribute

### Attribute

```csharp
// Engine/View.cs — alongside existing attributes
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveAttribute : Attribute { }
```

### Two serialization modes

**Storage mode** (DataSource): `SqliteDataSource.SerializeValue()` uses raw `JsonSerializer.Serialize()` — no view filtering. `[Sensitive]` properties are included. No changes needed here.

**Output mode** (everything else): The default `JsonStreamSerializer` options get a modifier that strips properties marked `[Sensitive]`. This applies to HTTP responses, output/write, logging, variable interpolation — anywhere data leaves the system.

Implementation: A `SensitivePropertyFilter` modifier (same pattern as `ViewPropertyFilter`) added to the default `JsonSerializerOptions` in `JsonStreamSerializer`. Unlike views which are opt-in per type, `[Sensitive]` is always active — any type, any property. The filter checks each property for `[Sensitive]` and removes it.

```csharp
// Engine/Channels/Serializers/SensitivePropertyFilter.cs
public static class SensitivePropertyFilter
{
    public static void Filter(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            var prop = typeInfo.Properties[i];
            if (prop.AttributeProvider?.IsDefined(typeof(SensitiveAttribute), false) == true)
                typeInfo.Properties.RemoveAt(i);
        }
    }
}
```

This is added to the default `JsonStreamSerializer` constructor's options as a TypeInfoResolver modifier. Storage (DataSource) doesn't use `JsonStreamSerializer` — it uses raw `JsonSerializer`, so `[Sensitive]` properties are preserved in storage.

## Module structure

```
PLang/Runtime2/modules/identity/
├── create.cs           — generate key pair, store in DataSource
├── get.cs              — get by name or default, auto-create if none exist
├── getAll.cs           — list all non-archived identities
├── archive.cs          — soft-delete (rejects default identity)
├── unarchive.cs        — restore archived identity
├── rename.cs           — change identity name, keep keys
├── setDefault.cs       — switch which identity is current default
├── export.cs           — return private key (LLM-gated)
├── types.cs            — IdentityVariable class
```

## Actions detail

### create

**Parameters:**
- `Name : string` — defaults to `"default"`
- `SetAsDefault : bool` — defaults to `false`

**Flow:**
1. Validate name: non-empty, non-whitespace
2. Check name uniqueness across ALL identities (including archived) — error if taken
3. Generate Ed25519 key pair via NSec
4. Build `IdentityVariable` (Name, PublicKey, PrivateKey, IsDefault=false, IsArchived=false, Created=now)
5. If SetAsDefault: clear IsDefault on all others in DataSource, set this one as default
6. Store in `System.DataSource.Set("identity", name, identityVariable)`
7. If it became default: register/update `%MyIdentity%` on System MemoryStack
8. Return `Data.Ok(identityVariable)`

### get

**Parameters:**
- `Name : string?` — null means get default

**Flow:**
1. If name provided: `System.DataSource.Get("identity", name)` → return (error if not found)
2. If no name: scan all identities, find `IsDefault == true`
3. If no default exists: call create internally (name="default", setAsDefault=true)
4. Register/update `%MyIdentity%` on System MemoryStack
5. Return `Data.Ok(identityVariable)`

### getAll

**Parameters:** none

**Flow:**
1. `System.DataSource.GetAll("identity")`
2. Filter out `IsArchived == true`
3. Return `Data.Ok(list)`

### archive

**Parameters:**
- `Name : string` — which identity to archive

**Flow:**
1. Load identity from DataSource (error if not found)
2. If it's the default: return `Data.Fail("Cannot archive the default identity. Set a different default first.")`
3. If already archived: return `Data.Ok()` (idempotent)
4. Set `IsArchived = true`
5. Save back to DataSource
6. Return `Data.Ok()`

### unarchive

**Parameters:**
- `Name : string` — which identity to unarchive

**Flow:**
1. Load identity from DataSource (error if not found)
2. If not archived: return `Data.Ok()` (idempotent)
3. Set `IsArchived = false`
4. Save back to DataSource
5. Return `Data.Ok(identityVariable)`

### rename

**Parameters:**
- `Name : string` — current name
- `NewName : string` — desired new name

**Flow:**
1. Validate NewName: non-empty, non-whitespace
2. Load identity from DataSource by Name (error if not found)
3. Check NewName uniqueness across ALL identities including archived (error if taken)
4. Remove old entry from DataSource: `System.DataSource.Remove("identity", name)`
5. Update identity.Name = NewName
6. Store under new key: `System.DataSource.Set("identity", newName, identity)`
7. If this was the default identity: update `%MyIdentity%` on System MemoryStack (same object, new name)
8. Return `Data.Ok(identityVariable)`

### setDefault

**Parameters:**
- `Name : string` — which identity becomes default

**Flow:**
1. Load all non-archived identities
2. Find target (error if not found or archived)
3. Clear IsDefault on all, set IsDefault on target
4. Save all changed identities back to DataSource
5. Update `%MyIdentity%` on System MemoryStack with the new default
6. Return `Data.Ok(identityVariable)`

### export

**Parameters:**
- `Name : string?` — null means current default

**Flow:**
1. LLM-gated: return `AskError` ("Exporting private key. Are you sure?")
2. If confirmed: load identity, return `Data.Ok(identity.PrivateKey)` as string
3. The `[Sensitive]` attribute doesn't matter here — we're returning the raw string, not serializing the object

**LLM-gating**: The `export` action's description in the module registry tells the LLM this is sensitive. The builder generates an `AskError` in the .pr file's onError section. This is a builder concern, not a runtime enforcement — the runtime just returns the value.

## Lazy resolver for %MyIdentity%

Registered on System actor's MemoryStack during engine initialization. Pattern similar to `%Now%` — a func/lazy that triggers on first access.

**Flow:**
1. Engine starts → registers `%MyIdentity%` as lazy resolver on System MemoryStack
2. PLang step accesses `%MyIdentity%` → MemoryStack invokes resolver
3. Resolver: query DataSource for default identity → auto-create if none → cache IdentityVariable → return
4. Subsequent accesses return cached value (updated by create/setDefault/archive/rename actions)

**Implementation note:** The resolver needs access to System.DataSource and the Ed25519 key generation. This could be a delegate registered during module initialization, or a static method on the get action class. The exact wiring is a coder concern — the architecture just requires that `%MyIdentity%` resolves lazily through the same logic as the `get` action.

## Key generation internals

```csharp
// Inside the identity module — not exposed as an action
using NSec.Cryptography;

internal static class KeyGenerator
{
    public static (string publicKey, string privateKey) GenerateEd25519()
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        using var key = Key.Create(algorithm, new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        });

        var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);
        var privateKeyBytes = key.Export(KeyBlobFormat.RawPrivateKey);

        return (
            Convert.ToBase64String(publicKeyBytes),
            Convert.ToBase64String(privateKeyBytes)
        );
    }
}
```

## Test changes from v1

### Modified tests
| Test | Change |
|------|--------|
| `DotNavigation_PrivateKey_IsBlocked` | → `DotNavigation_PrivateKey_ReturnsPrivateKey` — assert it returns the private key |
| `Create_DuplicateName_ReturnsError` | Remove architect question comment — decision is: always error, even archived |

### New C# tests needed
| Test | File | Purpose |
|------|------|---------|
| `Create_DuplicateArchivedName_ReturnsError` | IdentityHandlerTests.cs | Creating with a name that exists but is archived still errors |
| `Unarchive_RestoresArchivedIdentity` | IdentityHandlerTests.cs | IsArchived goes from true to false |
| `Unarchive_NonExistentName_ReturnsError` | IdentityHandlerTests.cs | Error path |
| `Unarchive_NotArchived_IsIdempotent` | IdentityHandlerTests.cs | Already active identity — no-op success |
| `Rename_ChangesName_KeepsKeys` | IdentityHandlerTests.cs | Same key pair, different name, old name gone |
| `Rename_DuplicateNewName_ReturnsError` | IdentityHandlerTests.cs | NewName already taken (including archived) |
| `Rename_NonExistentName_ReturnsError` | IdentityHandlerTests.cs | Source name doesn't exist |
| `Rename_DefaultIdentity_UpdatesMyIdentity` | IdentityHandlerTests.cs | %MyIdentity% reflects new name |
| `Rename_EmptyNewName_ReturnsError` | IdentityHandlerTests.cs | Validation |

### New PLang tests needed
| Directory | Test | Purpose |
|-----------|------|---------|
| `Tests/Runtime2/IdentityUnarchive/` | Unarchive | archive then unarchive, verify accessible |
| `Tests/Runtime2/IdentityRename/` | Rename | rename identity, verify old name gone, new name works |

### Test count
- **C# tests:** 40 (existing, with 1 renamed) + 9 new = **49**
- **PLang tests:** 8 (existing) + 2 new = **10**
- **Total: 59**

## Files to create/modify

### New files
| File | Purpose |
|------|---------|
| `PLang/Runtime2/modules/identity/create.cs` | Create action handler |
| `PLang/Runtime2/modules/identity/get.cs` | Get action handler |
| `PLang/Runtime2/modules/identity/getAll.cs` | GetAll action handler |
| `PLang/Runtime2/modules/identity/archive.cs` | Archive action handler |
| `PLang/Runtime2/modules/identity/unarchive.cs` | Unarchive action handler |
| `PLang/Runtime2/modules/identity/rename.cs` | Rename action handler |
| `PLang/Runtime2/modules/identity/setDefault.cs` | SetDefault action handler |
| `PLang/Runtime2/modules/identity/export.cs` | Export action handler |
| `PLang/Runtime2/modules/identity/types.cs` | IdentityVariable class |
| `PLang/Runtime2/modules/identity/KeyGenerator.cs` | Ed25519 key generation (internal) |
| `PLang/Runtime2/Engine/Channels/Serializers/SensitivePropertyFilter.cs` | [Sensitive] filter |

### Modified files
| File | Change |
|------|--------|
| `PLang/Runtime2/Engine/View.cs` | Add `[Sensitive]` attribute |
| `PLang/Runtime2/Engine/Channels/Serializers/Serializer/JsonStreamSerializer.cs` | Add SensitivePropertyFilter to default options |
| `PLang/Runtime2/Engine/this.cs` | Register `%MyIdentity%` lazy resolver on System MemoryStack |

### Test files to modify
| File | Change |
|------|--------|
| `PLang.Tests/Runtime2/Modules/identity/IdentityHandlerTests.cs` | Add 9 new test stubs (rename, unarchive, duplicate-archived), remove architect question comment |
| `PLang.Tests/Runtime2/Modules/identity/IdentityVariableTests.cs` | Rename PrivateKey test, remove architect question comment |

### Test files to create
| File | Purpose |
|------|---------|
| `Tests/Runtime2/IdentityUnarchive/IdentityUnarchive.test.goal` | PLang unarchive test |
| `Tests/Runtime2/IdentityRename/IdentityRename.test.goal` | PLang rename test |

## Definition of done

- Identity CRUD works (create, get, getAll, archive, **unarchive**, **rename**, setDefault, export)
- Auto-creates "default" identity on first `get` when none exist
- `%MyIdentity%` resolves lazily to current default IdentityVariable (System actor)
- Dot navigation works for all properties including `%MyIdentity.PrivateKey%`
- `%Identity%` and `%ServiceIdentity%` are reserved for User/Service actors (not managed here)
- Private key excluded from output serialization via `[Sensitive]`
- Private key included in DataSource storage
- Private key accessible via dot navigation (serialization ≠ access control)
- Name uniqueness enforced across all identities including archived
- Archive rejects default identity — developer must `set default` first
- Unarchive restores an archived identity
- Rename changes name, preserves key pair, updates `%MyIdentity%` if default
- Stored in System actor's DataSource, table `"identity"`
- Ed25519 key generation via NSec (internal, no signing module dependency)
- `[Sensitive]` attribute and `SensitivePropertyFilter` available for other modules
- All 59 tests pass (49 C# + 10 PLang)
