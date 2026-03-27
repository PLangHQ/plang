# Crypto Module

Cryptographic hashing and verification. Supports pluggable algorithm providers — default algorithms are Keccak256 and SHA256.

## Actions

### hash

Hash data using a cryptographic algorithm.

```plang
- hash %content%, write to %hash%
- hash %data% with sha256, write to %hash%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Data | object | yes | — | Data to hash. Byte arrays hash directly; everything else is JSON-serialized first |
| Algorithm | string | no | keccak256 | Hash algorithm to use |

**Returns:** A hashed data object with:

| Property | Description |
|----------|-------------|
| `algorithm` | Algorithm used (lowercase) |
| `format` | "raw" for byte arrays, "json" for serialized objects |
| `hash` | Hex-encoded hash string |

In string context (`%hash%`), returns the hex hash directly.

### verify

Verify data against a known hash.

```plang
- verify %content% against %expectedHash%, write to %isValid%
- verify %data% against %expectedHash% with sha256, write to %isValid%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Data | object | yes | — | Data to verify |
| Hash | string | yes | — | Expected hash (hex string) |
| Algorithm | string | no | keccak256 | Hash algorithm to use |

**Returns:** `true` if the data matches the hash, `false` otherwise.

## Supported Algorithms

| Algorithm | Implementation |
|-----------|---------------|
| `keccak256` | Nethereum Sha3Keccack (default) |
| `sha256` | System.Security.Cryptography |

Custom algorithms can be added by loading a provider DLL that implements `ICryptoProvider`.

## Examples

### Hash and Verify

```plang
Start
- set %secret% = 'my sensitive data'
- hash %secret%, write to %hash%
- write out 'Hash: %hash%'
- verify %secret% against %hash%, write to %isValid%
- write out 'Valid: %isValid%'
```

### Using SHA256

```plang
Start
- hash %content% with sha256, write to %hash%
- write out %hash%
```
