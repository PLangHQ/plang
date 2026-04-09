# Identity Module

Manage Ed25519 cryptographic identities. Each identity has a name, public/private key pair, and can be set as the default. The default identity is available as `%MyIdentity%` in all PLang code.

## Actions

### create

Create a new identity with an Ed25519 key pair.

```plang
- create identity 'alice'
- create identity 'alice', set as default
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Name | string | no | "default" | Identity name |
| SetAsDefault | bool | no | false | Make this the default identity |

Names are case-insensitive and must be unique (including archived identities).

### get

Get an identity by name, or the default identity.

```plang
- get identity 'alice', write to %identity%
- get identity, write to %identity%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | no | Identity name. Omit to get the default (auto-creates if none exist) |

**Returns:** An identity object with:

| Property | Description |
|----------|-------------|
| `name` | Identity name |
| `publicKey` | Ed25519 public key (base64) |
| `isDefault` | Whether this is the default identity |
| `isArchived` | Whether this identity is archived |
| `created` | Creation timestamp |

In string context (`%identity%`), returns the public key.

### list

List all non-archived identities.

```plang
- get identities, write to %identities%
```

No parameters.

### archive

Soft-delete an identity. Cannot archive the default — set a different default first.

```plang
- archive identity 'alice'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | yes | Identity name to archive |

Idempotent — archiving an already-archived identity is a no-op.

### unarchive

Restore an archived identity.

```plang
- unarchive identity 'alice'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | yes | Identity name to restore |

### rename

Rename an identity. Keys are preserved.

```plang
- rename identity 'alice' to 'alice-prod'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | yes | Current identity name |
| NewName | string | yes | New identity name |

If the renamed identity is the default, `%MyIdentity%` updates automatically.

### setDefault

Set an identity as the default. Only one identity can be the default at a time.

```plang
- set default identity to 'alice'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | yes | Identity name to make default |

Cannot set an archived identity as default.

### export

Export an identity's full data including the private key.

```plang
- export identity 'alice', write to %exported%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | no | Identity name. Omit to export the default |

Returns the full identity data including the private key.

## %MyIdentity%

The `%MyIdentity%` variable is always available and points to the default identity. It re-evaluates on every access, so changes via `setDefault` or `rename` are reflected immediately.

```plang
Start
- write out 'My public key: %MyIdentity%'
- write out 'My name: %MyIdentity.Name%'
```

If no identities exist, accessing `%MyIdentity%` auto-creates a "default" identity.

## Examples

### Create and Use Identity

```plang
Start
- create identity 'main', set as default
- write out 'Public key: %MyIdentity%'
```

### Multiple Identities

```plang
Start
- create identity 'personal'
- create identity 'work', set as default
- get identities, write to %all%
- write out 'All identities: %all%'
- set default identity to 'personal'
- write out 'Now using: %MyIdentity.Name%'
```
