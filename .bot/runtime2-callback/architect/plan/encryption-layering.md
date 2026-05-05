# Encryption — owned by the Callback serializer

Encryption ships in this branch. v1 is a **structural pass-through**: the actions exist (`crypto.encrypt`, `crypto.decrypt`), the Callback serializer wires through them, but the v1 implementations return their input unchanged. Real symmetric crypto lands later (tracked in `Documentation/Runtime2/todos.md`) once the missing PLang runtime features are in place. The shape of the layering is settled now so the real implementation slots in without rework.

## The layering

Three classes, three concerns:

- **Callback (`AskCallback` / `ErrorCallback`)** — owns its on-disk shape end-to-end via `Serialize` / `Deserialize`. Encryption happens *inside* this layer, not above or below it.
- **Channels Data serializer** — wraps the Callback's bytes in `Data<ICallback>`, attaches `Data.Signature` over those (already-encrypted) bytes. Knows nothing about plaintext.
- **`crypto` module** — exposes `crypto.encrypt` and `crypto.decrypt` actions. v1 is identity (input → input); future v2 implements AES-GCM via `IKeyProvider`.

The Data layer never sees plaintext; the crypto module never knows it's serving Callback. Each class minds its own business.

## What gets encrypted

The whole Callback payload. Each `Serialize` builds the byte representation it needs (`AskCallback`'s explicit fields; `ErrorCallback`'s `Snapshot.@this` tree) and pipes the entire thing through `crypto.encrypt` before returning. No partial encryption, no per-field decisions — the layer is owned by one class so defense-in-depth is free.

There's no reason to leak path, position, frame chain, or provider names to anyone holding a stored callback blob.

What's *not* encrypted: `Data.Signature` itself (the signing identity, the signature bytes, expiry timestamp). That's the envelope that needs to be verifiable without knowing the encryption key.

## Issuance flow

Inside `callback.Serialize(ctx)`:

1. Build a flat byte representation of the record's fields.
2. Pipe through `ctx.App.Modules.Get("crypto").EncryptAsync(bytes)`.
3. Return the encrypted bytes.

The Channels Data serializer takes those bytes, wraps them in `Data<ICallback>`, populates `Data.Signature` (signing the encrypted bytes), and writes to whatever channel the developer chose. See [transparent-signing.md](transparent-signing.md) for the channel-aware emission rules.

## Resume flow

Inside `callback.Run(ctx)`:

1. Verify `Data.Signature` (existing `signing.verify`). Hard error on mismatch.
2. Unwrap `Data<ICallback>` to get the encrypted payload bytes.
3. Pipe through `ctx.App.Modules.Get("crypto").DecryptAsync(bytes)`.
4. `XxxCallback.Deserialize(plaintext, ctx)` reconstructs the record.
5. Resolve `Goal` stub against the live App, jump and run (see [resume-mechanics.md](resume-mechanics.md#callback-run)).

## v1 implementation: pass-through actions

`crypto.encrypt` and `crypto.decrypt` exist as real PLang actions in v1. Their handler bodies return the input unchanged. The full pipeline — Callback's serializer calling through them, signature wrapping the (currently identical) bytes — is exercised end-to-end. When real crypto lands, only the action handlers change; nothing in Callback or Data needs to move.

Tracked in `Documentation/Runtime2/todos.md`:

> **`crypto.encrypt` / `crypto.decrypt` real implementation.** v1 is identity. Real impl is symmetric AES-GCM via `IKeyProvider`, gated on `<missing PLang runtime features — name them when known>`.

## OBP rationale

Earlier sketches had `Callback.EncryptInPlace(ctx)` / `Callback.DecryptInPlace(ctx)` as separate methods invoked by an outside coordinator. That's the wrong shape — it splits Callback's serialization across two callers. With encryption inside `Serialize` / `Deserialize`, the call site (Channels) doesn't know encryption exists, the developer doesn't see encryption verbs in PLang, and there's no orchestration question about "did someone forget to encrypt before signing." The class owns its discipline.

## Forward compatibility

When real crypto lands, the action signatures stay the same; the wire bytes change content but not envelope shape; the signature still wraps the encrypted payload. Existing pass-through callbacks become unreadable (their plaintext bytes won't decrypt under real keys), which is correct — they were never secure. No migration story is needed because nothing has shipped to users yet.
