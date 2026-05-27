# Wire Shape Reference

Concrete JSON examples of what Data looks like serialized through `application/plang`. Each Data on the wire is flat: four reserved fields (`name`, `type`, `value`, `signature`) plus any `Properties` entries as top-level siblings. Nesting through byte-decoding layers, never JSON object nesting.

## Plain Data — untyped `%user%`

When the Data was constructed without a strict generic — `new data.@this("user", new { firstName="Ingi", ... })` — `type` carries the runtime's natural type-name for the value's CLR shape. For an anonymous object: `"object"`. For a `Dictionary<,>`: `"dict"`. The `name` field is the variable binding ("user"); the `type` field is the schema. Different things.

```json
{
  "name": "user",
  "type": "object",
  "value": {
    "firstName": "Ingi",
    "lastName": "Gauti"
  },
  "signature": {
    "type": "signature",
    "algorithm": "ed25519",
    "nonce": "qz3X...",
    "created": "2026-05-26T14:30:00Z",
    "expires": null,
    "identity": "did:plang:...",
    "contracts": null,
    "headers": null,
    "hash": {
      "type": "sha256",
      "value": "Lm9j..."
    }
  }
}
```

## Plain Data — strictly typed `Data<User>`

When the Data is `new data.@this<User>("user", instance)`, `type` carries the PLang schema name for `User`:

```json
{
  "name": "user",
  "type": "user",
  "value": {
    "firstName": "Ingi",
    "lastName": "Gauti"
  },
  "signature": { ... }
}
```

Wire shapes are identical for the receiver — the only difference is what `type` claims. Receiver: `channel.ReadAsync` → wire converter reconstructs the Data with all reserved fields populated. Caller binds it as `%user%` (preserving the name) or under any alias.

## Data with Properties — LLM response

When a Data carries Properties (metadata about the Data: cost, debug info, traces), each Property entry flattens to a top-level wire field next to the reserved ones. An LLM response with primary content as `Value` and cost as a Property:

```json
{
  "name": "response",
  "type": "string",
  "value": "Hello, how can I help you today?",
  "cost": 100,
  "model": "claude-opus-4-7",
  "signature": { ... }
}
```

Receiver: `%response%` resolves to the LLM text (Value renders as the primary content). `%response!cost%` reads `Properties["cost"]`. `%response!model%` reads `Properties["model"]`. The Property keys `name`, `type`, `value`, `signature` are reserved and cannot be used.

Forward-compatibility: receivers that don't know about a future top-level field (e.g., PLang later adds `traceId`) carry it as a Property automatically — no breaking change.

Signing covers Properties: tampering with `cost` in the JSON invalidates the outer `signature`. Properties are bound by the outer signature; they don't grow their own signatures (Properties are primitives, not Data).

## Compressed Data — `compress %user%`

`%user%` goes through `Compress()`, which produces an `archived` Data whose value IS the gzip-compressed bytes of the serialized inner Data. No nested Data in JSON.

```json
{
  "name": "",
  "type": "archived",
  "value": "H4sIAAAAAAAAA0WMQQ6CMBBF95yiV+gNcAEm6kJjItGFsZmU0jK0HUjbgN7eEhM2Pzm/5/9TZSnjJ9pNlSrjeMjqkk9JzMsK...",
  "signature": { ... }
}
```

The base64-decoded `value` bytes, after gzip-inflate, are themselves a fully serialized Data — including its own signature, populated automatically during Compress's serialize step (the wire converter calls `EnsureSigned` on every Data it walks):

```json
{
  "name": "user",
  "type": "object",
  "value": { "firstName": "Ingi", "lastName": "Gauti" },
  "signature": {
    "algorithm": "ed25519",
    "identity": "did:plang:alice",
    "hash": { "type": "sha256", "value": "..." }
  }
}
```

Two signatures, two attestations: the outer archived wrapper attests "this compressed package wasn't tampered with"; the inner signature attests "Alice authored this user data." Forwarding preserves the inner attestation — when Bob decompresses and forwards the unwrapped Data, Alice's signature rides along.

The `name` on the archived wrapper is empty because the wrapper isn't a variable, it's a transport layer. Empty names can be compacted out by STJ; conceptually the field stays.

## Encrypted Data — `encrypt %user%`

Same structural shape as compressed. Value is the ciphertext bytes; decrypt yields another serialized Data underneath.

```json
{
  "name": "",
  "type": "encryption",
  "value": "AQID...<ciphertext>...",
  "signature": { ... }
}
```

(Today's `Encrypt()` is a stub awaiting a crypto service. The shape lands when crypto lands.)

## Nested Data in `value`

When a Data's `value` is itself another Data (not bytes), STJ recurses through the wire converter. The converter visits each Data node it walks and applies sign-if-missing — so inner Datas each carry their own signature on the wire, attesting whoever first signed them.

Example: a response wrapper containing a named inner Data.

```json
{
  "name": "response",
  "type": "object",
  "value": {
    "name": "user",
    "type": "user",
    "value": { "firstName": "Ingi", "lastName": "Gauti" },
    "signature": {
      "algorithm": "ed25519",
      "identity": "did:plang:alice",
      "hash": { "type": "sha256", "value": "..." }
    }
  },
  "signature": {
    "algorithm": "ed25519",
    "identity": "did:plang:bob",
    "hash": { "type": "sha256", "value": "..." }
  }
}
```

Receiver writes the response to `%response%`; `%response.user%` navigates through the preserved internal name into the typed inner Data.

Pattern: every Data the converter visits emits `name`, `type`, `value`, and `signature` (plus any Properties as siblings). The outer signature canonicalizes over the full wire shape — *including* the inner signatures — so tampering with the inner attestation invalidates the outer. The Stage 2 canonicalization fix (hash through `Transport.ForOutbound` options) is what makes this binding cryptographically tight, instead of only structurally true.

List<Data> in `value` behaves the same: each list element is a Data, each gets sign-if-missing during the walk. Outer signature binds the full list-of-signed-Datas as encoded.

## What's NOT on the wire

- `Context` — `[JsonIgnore]`. Resolved at deserialize time from the receiving actor.
- `Parent`, `Path`, `IsInitialized`, `IsVariable`, `Created`, `Updated`, `OnChange`, `OnCreate`, `OnDelete` — all `[JsonIgnore]`. None of these are part of the wire contract.

The wire contract for `application/plang` is: four reserved top-level fields (`name`, `type`, `value`, `signature`) plus any number of Property entries as additional top-level siblings. Reserved-key check forbids Properties from using the four reserved names. Anything else on the Data class is a runtime concern.

(Pre-cleanup `Properties` was `[JsonIgnore]` and did not cross the wire. Stage 4 changes both the C# shape and the wire emission — see [main plan's Properties section](../plan.md).)

## Round-trip invariants

A Data sent through `application/plang` round-trips losslessly on:

- `name` preserved (binding coordinate)
- `type` preserved (PLang type name string)
- `value` preserved (primitive, nested Data, or byte[] depending on the type)
- `signature` populated on every Data the wire converter walked; verifiable iff the wire bytes are unchanged
- `Properties` preserved — every entry rides as a top-level wire field; receiver places unknown top-level fields into Properties verbatim

The other contracts:

- Every Data on the wire has a signature (sign-if-missing rule applied during the converter walk). Receivers verify explicitly; deserialize does not auto-invoke verification.
- The outer signature on a Data canonicalizes over the full wire shape (name, type, value, Properties, and inner signatures inside `value`). Tampering with anything visible on the wire invalidates the outer signature.
- Bytes-based layers (`archived`, `encryption`) hold their decoded payload as another serialized Data — discoverable by unwrapping, not by JSON traversal. Sign-then-compress chains preserve the inner attestation; outer attestation covers the compressed package.
- A non-Data input to `application/plang` is a compile error (Stage 1 tightens the ISerializer input contract).
