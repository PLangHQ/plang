# Identity Module — CHANGELOG

## New: Identity Module (`identity`)

PLang now has built-in Ed25519 identity management. Each identity is a named key pair stored locally in the system database.

### Actions

| Action | Description |
|--------|-------------|
| `create` | Create a new identity with Ed25519 key pair |
| `get` | Get an identity by name, or the default (auto-creates if needed) |
| `getAll` | List all active (non-archived) identities |
| `archive` | Soft-delete an identity (must not be default) |
| `unarchive` | Restore an archived identity |
| `rename` | Rename an identity (keys preserved) |
| `setDefault` | Switch which identity is the default |
| `export` | Export the private key of an identity |

### Variables

- `%MyIdentity%` — resolves to the default identity. In string context, gives the public key. Use `%MyIdentity.PrivateKey%` for the private key, `%MyIdentity.Name%` for the name.

### Security

- Private keys are marked `[Sensitive]` — automatically stripped from all output serialization (API responses, console, compressed payloads).
- Private keys persist in storage and are accessible in PLang code via `%MyIdentity.PrivateKey%` or the `export` action.
- Ed25519 keys generated via NSec (OS CSPRNG).

## New: [Sensitive] Attribute

Properties marked `[Sensitive]` are excluded from output serialization but included in storage. Applied automatically — no opt-in needed.

## Changed: Data.Envelope

`SensitivePropertyFilter` added to `Data.Envelope._envelopeJsonOptions`. Defense-in-depth: compressed payloads won't leak sensitive properties.

## Changed: Actor

Each actor now has:
- `Identity` property — lazy `IdentityData` that auto-creates a default identity on first access
- `DataSource` property — lazy per-actor SQLite storage (`.db/{actorname}.sqlite`)
