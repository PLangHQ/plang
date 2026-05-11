# Code Module

Manage pluggable code implementations at runtime. Code implementations are .NET assemblies that implement module interfaces (`ICrypto`, `ISigning`, `IIdentity`, etc.) and let you swap behaviour without changing your PLang code.

## Actions

### load

Load code from a .NET assembly.

```plang
- load code 'plugins/my-crypto.dll'
- load code 'plugins/my-crypto.dll' as 'custom-crypto'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Path | string | yes | Path to the .dll file |
| Name | string | no | Display name (the implementation supplies its own name by default) |

The loaded assembly is scanned for classes implementing `ICode`. Each is registered for its specific interface (e.g., `ICrypto`, `ISigning`). The first registration for a type becomes the default.

### remove

Remove a registered implementation by name.

```plang
- remove code 'custom-crypto'
- remove code 'custom-crypto' from signing
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | yes | Implementation name to remove |
| Type | string | no | Type filter (e.g., "signing", "crypto", "identity", "key") |

Cannot remove the default — set a different default first via `setDefault`.

### list

List registered implementations.

```plang
- list code, write to %impls%
- list signing code, write to %signingImpls%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Type | string | no | Type to filter by. Omit to list everything |

### setDefault

Switch which registered implementation is the default for a type.

```plang
- set default signing code to 'custom-signing'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | yes | Implementation name to promote to default |
| Type | string | yes | Type the default applies to (e.g., "signing", "crypto") |

## Code Types

| Type name | Interface | Used by |
|-----------|-----------|---------|
| `signing` | `ISigning` | signing module |
| `crypto` | `ICrypto` | crypto module |
| `identity` | `IIdentity` | identity module |
| `key` | `IKey` | key generation |
| `http` | `IHttp` | http module |
| `template` | `ITemplate` | ui module |
| `llm` | `ILlm` | llm module |
| `builder` | `IBuilder` | builder module |

## Example

```plang
Start
- load code 'plugins/RsaSigning.dll'
- list signing code, write to %impls%
- write out 'Available: %impls%'
```
