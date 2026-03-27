# Provider Module

Manage pluggable providers at runtime. Providers are .NET assemblies that implement module interfaces (`ICryptoProvider`, `ISigningProvider`, `IIdentityProvider`, etc.), allowing you to swap implementations without changing your PLang code.

## Actions

### load

Load a provider from a .NET assembly.

```plang
- load provider 'plugins/my-crypto.dll'
- load provider 'plugins/my-crypto.dll' as 'custom-crypto'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Path | string | yes | Path to the .dll file |
| Name | string | no | Display name (provider supplies its own name by default) |

The loaded assembly is scanned for classes implementing `IProvider`. Each is registered for its specific interface (e.g., `ICryptoProvider`, `ISigningProvider`). The first provider registered for a type becomes the default.

### remove

Remove a registered provider by name.

```plang
- remove provider 'custom-crypto'
- remove provider 'custom-crypto' from signing
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | yes | Provider name to remove |
| Type | string | no | Provider type filter (e.g., "signing", "crypto", "identity", "key") |

Cannot remove the default provider — set a different default first.

### list

List registered providers.

```plang
- list providers, write to %providers%
- list signing providers, write to %signingProviders%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Type | string | no | Provider type to filter by. Omit to list all providers |

## Provider Types

| Type name | Interface | Used by |
|-----------|-----------|---------|
| `signing` | `ISigningProvider` | signing module |
| `crypto` | `ICryptoProvider` | crypto module |
| `identity` | `IIdentityProvider` | identity module |
| `key` | `IKeyProvider` | key generation |
| `http` | `IHttpProvider` | http module |

## Example

```plang
Start
- load provider 'plugins/RsaSigning.dll'
- list signing providers, write to %providers%
- write out 'Available: %providers%'
```
