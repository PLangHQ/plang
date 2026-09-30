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

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| Path | the path, inline (`load code 'plugins/my-crypto.dll'`) | path | yes | Path to the .dll file |
| Name | `as '<name>'` | string | no | Display name (the implementation supplies its own name by default) |

The loaded assembly is scanned for three kinds of contribution:

- **`ICode` implementations** — registered for their specific interface (`ICrypto`, `ISigning`, …). The first registration for a type becomes the default.
- **`[PlangType]` classes** — added to the type registry. Runtime-loaded names win over built-ins at name → CLR-type resolution, so a DLL can introduce a new type (`quantity`, `tensor`) or refine how an existing one resolves at runtime.
- **`ITypeRenderer` implementations** — added to the per-(type, format) renderer table. Runtime-registered renderers win over generator-emitted ones, so a DLL can change how an existing type lands on the wire for a given format.

A DLL only needs to contribute one of the three to load successfully.

**Sealed names — refused at load.** The following type names are reserved and a runtime-loaded DLL cannot shadow them: `identity`, `signature`, `signedoperation`, `callback`, `channel`. Their bodies are signing- or transport-load-bearing — replacing them would let an attacker-composed body ride out under a valid signature. Attempting to load a DLL that registers one of these names fails with `TypeLoadCollision`. Primitives (`int`, `string`, `path`, …) stay overridable because their body is constrained by the type itself.

**Honest limit.** Runtime registration changes *resolution* (name → CLR type) and *rendering* (the wire form) going forward. It does NOT rewrite what the source generator already baked at PLang build: PLNG-validated parameter slots, the `Data<int>` slots on already-compiled action handlers, or type stamps in shipped `.pr` files. Adding new types is unconstrained; overwriting built-ins is "new resolution + new rendering, same compiled slots."

### remove

Remove a registered implementation by name.

```plang
- remove code 'custom-crypto'
- remove code 'custom-crypto' from signing
```

**Parameters:**

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| Name | the name, inline (`remove code 'custom-crypto'`) | string | yes | Implementation name to remove |
| Type | `from <type>` (`remove code 'custom-crypto' from signing`) | string | no | Type filter (e.g., "signing", "crypto", "identity", "key") |

Cannot remove the default — set a different default first via `setDefault`.

### list

List registered implementations.

```plang
- list code, write to %impls%
- list signing code, write to %signingImpls%
```

**Parameters:**

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| Type | the type before `code` (`list signing code`) | string | no | Type to filter by. Omit to list everything |

### setDefault

Switch which registered implementation is the default for a type.

```plang
- set default signing code to 'custom-signing'
```

**Parameters:**

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| Name | `to '<name>'` (`set default signing code to 'custom-signing'`) | string | yes | Implementation name to promote to default |
| Type | the type before `code` (`set default signing code`) | string | yes | Type the default applies to (e.g., "signing", "crypto") |

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
