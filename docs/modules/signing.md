# Signing Module

Create and verify signed data envelopes using Ed25519 (or any pluggable `ISigning` implementation registered on `app.Code`). Signing attaches a cryptographic signature to data; verification confirms the data hasn't been tampered with and was signed by the claimed identity.

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
| Expires | TimeSpan | no | — | Signature TTL (e.g., `PT5M` ISO 8601 duration). When set, `signature.Expires = Created + this`. |

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

**Returns:** `true` on success. On failure, returns an error with a specific key:

| Error Key | Cause |
|-----------|-------|
| `NoSignature` | Data has no signature attached |
| `Expired` | The signature is past its expiry (`%x!signature.expired%`) |
| `NonceReplay` | A signature read live presents a nonce already seen (replay attack) |
| `ContractMismatch` | Signer's contracts don't match expected |
| `HeaderMismatch` | Signer's headers don't match expected |
| `DataHashMismatch` | Data has been tampered with |
| `SignatureInvalid` | Cryptographic verification failed |

## How It Works

1. **Sign**: Hashes the data (Keccak256), builds an envelope with nonce, timestamp, identity, and contracts, then signs the envelope bytes with the signer's Ed25519 private key.
2. **Verify**: asks the signature whether it has expired, then checks the nonce (a live read only), contracts, data hash and the cryptographic signature. Each step returns a specific error key on failure.

## Expiry

A signature's expiry is `%x!signature.expires%` (a datetime) and `%x!signature.expired%` (a bool). It is what the signer
signed (`sign … Expires=5m`), or, for a signature read live off the wire, `%!signing.setting.expiry%` (default `5m`) after
it was created, whichever comes first. A signature read from plang's own store (a saved grant) gets no such window: only
what its signer signed bounds it, and its nonce is not checked for replay (the same nonce re-presents on every read).

```plang
- set %!signing.setting.expiry% = 1m
```

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
