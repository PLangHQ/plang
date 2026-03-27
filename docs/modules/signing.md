# Signing Module

Create and verify signed data envelopes using Ed25519 (or any pluggable signing provider). Signing attaches a cryptographic signature to data; verification confirms the data hasn't been tampered with and was signed by the claimed identity.

## Actions

### sign

Sign data and attach a signature envelope.

```plang
- sign %data%, write to %signedData%
- sign %payload% with contracts ['Transfer', 'v2'], write to %signed%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Data | object | yes | — | Data to sign |
| Contracts | list | no | ["C0"] | Contract identifiers attached to the signature |
| Headers | dictionary | no | — | Optional headers included in the envelope |
| ExpiresInMs | int | no | — | Signature TTL in milliseconds |

**Returns:** The data with a `.Signature` property containing the signed envelope (nonce, timestamp, identity, hash, and cryptographic signature).

### verify

Verify a signed data envelope.

```plang
- verify %signedData%, write to %isValid%
- verify %signedData% with contracts ['Transfer', 'v2'], write to %isValid%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Data | object | yes | — | Signed data to verify (must have `.Signature`) |
| Contracts | list | no | — | Expected contracts to match |
| Headers | dictionary | no | — | Expected headers to match |
| TimeoutMs | long | no | — | Override the default timeout (5 minutes) |

**Returns:** `true` on success. On failure, returns an error with a specific key:

| Error Key | Cause |
|-----------|-------|
| `NoSignature` | Data has no signature attached |
| `InvalidType` | Signature type field is wrong |
| `TimedOut` | Signature is older than timeout |
| `Expired` | Signature's explicit expiry has passed |
| `NonceReplay` | This nonce was already seen (replay attack) |
| `ContractMismatch` | Signer's contracts don't match expected |
| `HeaderMismatch` | Signer's headers don't match expected |
| `DataHashMismatch` | Data has been tampered with |
| `SignatureInvalid` | Cryptographic verification failed |

## How It Works

1. **Sign**: Hashes the data (Keccak256), builds an envelope with nonce, timestamp, identity, and contracts, then signs the envelope bytes with the signer's Ed25519 private key.
2. **Verify**: Runs a 9-step check — type, provider, timeout, expiry, nonce replay, contracts, headers, data hash, cryptographic signature. Each step returns a specific error key on failure.

## Examples

### Sign and Verify

```plang
Start
- set %message% = 'Hello, signed world'
- sign %message%, write to %signed%
- verify %signed%, write to %isValid%
- write out 'Valid: %isValid%'
```

### Sign with Contracts and Expiry

```plang
Start
- sign %payload% with contracts ['Payment', 'v1'], expires in 60000ms, write to %signed%
- verify %signed% with contracts ['Payment', 'v1'], write to %isValid%
```
