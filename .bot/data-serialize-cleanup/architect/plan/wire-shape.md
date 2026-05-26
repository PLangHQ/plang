# Wire Shape Reference

Concrete JSON examples of what Data looks like serialized through `application/plang`. Every Data on the wire is flat — four fields max: `name`, `type`, `value`, `signature`. Nesting through byte-decoding layers, never JSON object nesting.

## Plain Data — `%user%`

The simplest case: a Data named "user" with an object value, signed by the channel on its way out.

```json
{
  "name": "user",
  "type": "user",
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

Receiver: `channel.ReadAsync` → serializer reconstructs the Data with all four fields populated. Caller binds it as `%user%` (preserving the name) or under any alias.

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

The base64-decoded `value` bytes, after gzip-inflate, are themselves a serialized Data:

```json
{
  "name": "user",
  "type": "user",
  "value": { "firstName": "Ingi", "lastName": "Gauti" }
}
```

Note: the inner Data (when decoded out of the bytes) has **no signature field**. The outer archived wrapper's signature attested to the entire compressed payload — that's the integrity guarantee for the bytes inside.

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

When a Data's `value` is itself another Data (not bytes), STJ recurses through the `DataConverter`. The inner Data is also flat, but its `signature` is absent because only the outermost was signed by the channel.

Example: a response wrapper containing a named inner Data.

```json
{
  "name": "response",
  "type": "object",
  "value": {
    "name": "user",
    "type": "user",
    "value": { "firstName": "Ingi", "lastName": "Gauti" }
  },
  "signature": { ... }
}
```

Receiver writes the response to `%response%`; `%response.user%` navigates through the preserved internal name.

Pattern: outermost has a signature. Inner Datas (in the JSON `value` slot) emit `name`, `type`, `value` only. The `[Out]` filter omits null/unset signature fields on inner levels naturally — because nothing in the recursion path called `EnsureSigned` on them.

## What's NOT on the wire

- `Context` — `[JsonIgnore]`. Resolved at deserialize time from the receiving actor.
- `Properties` — `[JsonIgnore]` without `[Out]`. In-process transport view only.
- `Parent`, `Path`, `IsInitialized`, `IsVariable`, `Created`, `Updated`, `OnChange`, `OnCreate`, `OnDelete` — all `[JsonIgnore]`. None of these are part of the wire contract.

The four-field shape (`name`, `type`, `value`, `signature`) is the entire wire contract for `application/plang`. Anything else is a runtime concern.

## Round-trip invariants

A Data sent through `application/plang` round-trips losslessly on these four fields:

- `name` preserved (binding coordinate)
- `type` preserved (PLang type name string)
- `value` preserved (primitive, nested Data, or byte[] depending on the type)
- `signature` populated on the outermost; verifiable against the byte stream

The other contracts:

- Inner Datas (recursed into via `value`) never carry a signature on the wire.
- Bytes-based layers (`archived`, `encryption`) hold their decoded payload as another serialized Data — discoverable by unwrapping, not by JSON traversal.
- A non-Data input to `application/plang` is a category error (boundary throws).
