# Piece 1: Identity Module — Architecture Plan

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
- `ToString()` returns PublicKey — so `%MyIdentity%` in string context gives the public key
- Dot navigation works: `%MyIdentity.Name%`, `%MyIdentity.PublicKey%`, `%MyIdentity.Created%`
- No separate internal/external types. One object, one truth.

### Special variables — per-actor, not shared

| Variable | Actor | Source | Auto-create? |
|----------|-------|--------|-------------|
| `%MyIdentity%` | System | DataSource (stored) | Yes, on first access |
| `%Identity%` | User | Set by HTTP/signing layer | No |
| `%ServiceIdentity%` | Service | Set by HTTP/signing layer | No |

Only `%MyIdentity%` has a lazy resolver. `%Identity%` and `%ServiceIdentity%` are plain Variables values set by the HTTP/signing modules (Pieces 2-3). The identity module does not manage them.

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
PLang/App/modules/identity/
├── create.cs           — generate key pair, store in DataSource
├── get.cs              — get by name or default, auto-create if none exist
├── getAll.cs           — list all non-archived identities
├── archive.cs          — soft-delete, auto-promote next default
├── setDefault.cs       — switch which identity is current default
├── export.cs           — return private key (LLM-gated)
├── types.cs            — IdentityVariable class
```

## Actions detail

### create

**Parameters:**
- `Name : string` — defaults to `"default"`
- `SetAsDefault : bool` — defaults to `true` if it's the first identity, `false` otherwise

**Flow:**
1. Generate Ed25519 key pair via NSec
2. Build `IdentityVariable` (Name, PublicKey, PrivateKey, IsDefault, Created=now)
3. If SetAsDefault or first identity: clear IsDefault on all others in DataSource, set this one as default
4. Store in `System.DataSource.Set("identity", name, identityVariable)`
5. If it became default: register/update `%MyIdentity%` on System Variables
6. Return `Data.Ok(identityVariable)`

### get

**Parameters:**
- `Name : string?` — null means get default

**Flow:**
1. If name provided: `System.DataSource.Get("identity", name)` → return
2. If no name: scan all identities, find `IsDefault == true`
3. If no default exists: call create internally (name="default", setAsDefault=true)
4. Register/update `%MyIdentity%` on System Variables
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
1. Load identity from DataSource
2. Set `IsArchived = true`, `IsDefault = false`
3. Save back to DataSource
4. If it was the default: find next non-archived (ordered by Created), make it default
5. If no non-archived remain: `%MyIdentity%` becomes null (next access auto-creates via lazy resolver)
6. Update `%MyIdentity%` on Variables
7. Return `Data.Ok()`

### setDefault

**Parameters:**
- `Name : string` — which identity becomes default

**Flow:**
1. Load all non-archived identities
2. Clear IsDefault on all, set IsDefault on target
3. Save all changed identities back to DataSource
4. Update `%MyIdentity%` on System Variables with the new default
5. Return `Data.Ok(identityVariable)`

### export

**Parameters:**
- `Name : string?` — null means current default

**Flow:**
1. LLM-gated: return `AskError` ("Exporting private key. Are you sure?")
2. If confirmed: load identity, return `Data.Ok(identity.PrivateKey)` as string
3. The `[Sensitive]` attribute doesn't matter here — we're returning the raw string, not serializing the object

**LLM-gating**: The `export` action's description in the module registry tells the LLM this is sensitive. The builder generates an `AskError` in the .pr file's onError section. This is a builder concern, not a runtime enforcement — the runtime just returns the value.

## Lazy resolver for %MyIdentity%

Registered on System actor's Variables during engine initialization. Pattern similar to `%Now%` — a func/lazy that triggers on first access.

**Flow:**
1. Engine starts → registers `%MyIdentity%` as lazy resolver on System Variables
2. PLang step accesses `%MyIdentity%` → Variables invokes resolver
3. Resolver: query DataSource for default identity → auto-create if none → cache IdentityVariable → return
4. Subsequent accesses return cached value (updated by create/setDefault/archive actions)

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

## Test expectations

### C# unit tests (~15)
- create: generates valid Ed25519 key pair, stores in DataSource, first identity becomes default
- create: second identity with SetAsDefault=false doesn't change default
- create: second identity with SetAsDefault=true changes default
- get: returns identity by name
- get: returns default when no name given
- get: auto-creates "default" when none exist
- getAll: returns only non-archived
- getAll: empty when all archived
- archive: sets IsArchived, clears IsDefault
- archive: auto-promotes next identity as default
- archive: last identity archived, %MyIdentity% becomes null
- setDefault: switches default, clears old default
- setDefault: error when name doesn't exist or is archived
- export: returns private key string
- IdentityVariable.ToString(): returns PublicKey

### PLang tests (~8)
- Create identity, verify %MyIdentity% resolves to public key
- Create named identity, get by name
- Create two identities, switch default, verify %MyIdentity% changes
- Archive default, verify auto-promote
- Get identity when none exist (auto-create)
- %MyIdentity.Name% dot navigation
- Export private key
- Create, archive all, verify next access auto-creates

### Infrastructure tests (~5)
- [Sensitive] attribute: property excluded from JsonStreamSerializer output
- [Sensitive] attribute: property included in raw JsonSerializer (DataSource path)
- SensitivePropertyFilter: no-op on types without [Sensitive]
- SensitivePropertyFilter: works alongside view attributes
- Round-trip: store IdentityVariable with DataSource, retrieve, PrivateKey present

## Files to create/modify

### New files
| File | Purpose |
|------|---------|
| `PLang/App/modules/identity/create.cs` | Create action handler |
| `PLang/App/modules/identity/get.cs` | Get action handler |
| `PLang/App/modules/identity/getAll.cs` | GetAll action handler |
| `PLang/App/modules/identity/archive.cs` | Archive action handler |
| `PLang/App/modules/identity/setDefault.cs` | SetDefault action handler |
| `PLang/App/modules/identity/export.cs` | Export action handler |
| `PLang/App/modules/identity/types.cs` | IdentityVariable class |
| `PLang/App/modules/identity/KeyGenerator.cs` | Ed25519 key generation (internal) |
| `PLang/App/Engine/Channels/Serializers/SensitivePropertyFilter.cs` | [Sensitive] filter |

### Modified files
| File | Change |
|------|--------|
| `PLang/App/Engine/View.cs` | Add `[Sensitive]` attribute |
| `PLang/App/Engine/Channels/Serializers/Serializer/JsonStreamSerializer.cs` | Add SensitivePropertyFilter to default options |
| `PLang/App/Engine/this.cs` | Register `%MyIdentity%` lazy resolver on System Variables |

## Definition of done

- Identity CRUD works (create, get, getAll, archive, setDefault, export)
- Auto-creates "default" identity on first `get` when none exist
- `%MyIdentity%` resolves lazily to current default's public key (System actor)
- `%MyIdentity.Name%`, `%MyIdentity.PublicKey%` dot navigation works
- `%Identity%` and `%ServiceIdentity%` are reserved for User/Service actors (set by HTTP/signing layer, not managed here)
- Private key excluded from output serialization via `[Sensitive]`
- Private key included in DataSource storage
- Archive auto-promotes next identity as default
- Stored in System actor's DataSource, table `"identity"`
- Ed25519 key generation via NSec (internal, no signing module dependency)
- `[Sensitive]` attribute and `SensitivePropertyFilter` available for other modules
- C# tests for all actions + infrastructure
- PLang tests for end-to-end identity management
