# Encryption layering — setup for the future ask-user branch

**This branch ships without encryption.** The file settles the layering decision now so the signing path doesn't need to be rethought when the ask-user branch lands.

## The decision

For ask-user, the variable values transported over the wire need encryption (the user's browser shouldn't be able to read `%orderId%`). The encryption sits **inside Callback's own serialization**, *not* at the Data layer.

## Layering for issuance (future)

1. Callback class encrypts its variable values internally — `Callback.EncryptInPlace(Context ctx)` walks `VariablesByActor`, calls `ctx.App.Modules.Get('crypto').EncryptAsync(value)` per entry.
2. The resulting `Data<Callback>` is serialized to bytes — Data layer signs the encrypted-variables-containing bytes (same transparent path as in [transparent-signing.md](transparent-signing.md)).
3. Wire transport ships the signed envelope (base64 in an HTTP form field).

## Layering for resume (future)

1. Verify Data signature (existing `signing.verify`).
2. Callback class decrypts internally — `Callback.DecryptInPlace(Context ctx)`.
3. Hand off to `App.Run(callback)` as in this branch.

## Why this layering

- **Most Data writes don't need encryption** — keeping it out of the Data layer means logs, debug output, file writes, channel output stay unaffected.
- **Encryption is Callback's discipline** — OBP says the class owns its own behavior. Other Data subtypes don't pay any cost.
- **Signing wraps encryption naturally** — the signature certifies the encrypted content as authentically issued.
- **Forward-compatible** — Callback ships now *without* encryption methods; the ask-user branch adds them. Nothing has shipped yet, so no breakage.

## What the ask-user branch will need (not now)

`ICryptoProvider` extension: `EncryptAsync(value)` / `DecryptAsync(value)`. Symmetric AES-256-GCM via the existing `IKeyProvider`. Encrypt/decrypt as Callback-class methods, not module actions.
