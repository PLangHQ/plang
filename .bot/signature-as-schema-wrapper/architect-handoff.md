# Design spec — signatures as `@schema:"signature"` wrappers (next branch)

**To:** architect · **From:** coder (relaying Ingi's design) · **Status:** proposed, for a new
branch · **Depends on:** the `@schema` Data marker (this branch — it's the enabler).

## The decision

A signature is **not a property on Data**. Signing **wraps** the data in a first-class
`@schema:"signature"` object. The current `Data.Signature` field is the wrong model and gets
removed.

```
%doc% = { amount: 100 }                          // @schema:"data"

sign %doc%  (Alice)
  → { @schema:"signature", identity:Alice, algorithm:ed25519, hash:H(doc),
      nonce, expires, signature:…, value: %doc% }

sign that  (Bob)
  → { @schema:"signature", identity:Bob, hash:H(Alice-layer), signature:…,
      value: { @schema:"signature", identity:Alice, …, value:%doc% } }
```

A signature is a layer around the value. Multiple signatures = **nested layers**, always adding
one more on top.

## Why the current model is wrong

Today `Data` carries a `Signature` property. Problems:
- The untrusted **value is already deserialized/handled before** anything checks the signature.
- It conflates two concerns — a value and an attestation about it — on one type.
- It has no clean way to express more than one signer.

## Why the wrapper model is right

1. **Verify-before-touch.** The outer object announces itself (`@schema:"signature"`). A reader
   validates the signature over `hash(inner)` **first**; only on success does it look at the
   payload. Invalid → reject without ever parsing the value. Real security gain.
2. **Data stays clean** — back to `{@schema:"data", name, type, value}`, no transport/security
   cruft. Signing is a layer, not a field.
3. **Plang-only, naturally.** Signing is a plang concern; a plain `application/json` response is
   just the peeled value. `@schema` is what dispatches signature vs data, so the wrapper never
   leaks to json consumers.

## The model — one mechanism, nested layers

There is **one** signing mechanism: nested `@schema:"signature"` layers. "Co-signing" is not a
separate concept — it's this structure. "N parties must sign" / "these identities must sign" is a
**policy check on the validated layer set**, on top of the one mechanism.

**Settled rules:**
- **Always add a layer.** Signing the same object twice yields two signature layers over the data.
  No overwrite, no in-place replace (a layer's hash covers a fixed inner, so it can't be edited).
- **Verify-then-wrap.** Signing over an already-signed object first **validates the existing
  layer(s)**; refuse to wrap an invalid stack. You never attest something you haven't verified.
- **Verify peels outermost-in.** Validate each layer's signature over `hash(its inner)`, down to
  the `@schema:"data"` core. Any layer invalid → stop.
- **Hash scope.** Each layer's `hash` covers the canonical bytes of its immediate inner object
  (the `@schema:"data"` core, or the next `@schema:"signature"` layer).

**Known property (a feature, not a limit):** nesting is **serial**. Each signer signs over the
layers below, so they must already have them — signers go in sequence (Alice → Bob → Carol), which
is a real audit order. The only thing nesting can't express is "two parties sign the exact same
bytes without either seeing the other's signature" — which essentially never matters for plang.

## What it touches

- **Remove `Data.Signature`** (and its `[In]/[Out]/[Store]` plumbing, `EnsureSigned`).
- **New `signature` type** (`@schema:"signature"`) — the wrapper: identity, algorithm, hash, nonce,
  expires, contracts, headers, signature-bytes, and `value` (the inner). Wrap/peel surface.
- **Rewrite `sign`** → validate-inner-then-wrap (returns the new outer signature layer).
- **Rewrite `verify`** → peel outermost-in, validate each layer; expose the policy hook
  ("these identities present and valid").
- **Retire sign-if-missing** — the wire stops auto-signing on egress (`Wire.Write`'s
  `EnsureSigned` walk goes away). Signing becomes the explicit `sign` action; the wire just
  serializes whatever it's handed (a `data` or a `signature`). This is its own nice cleanup.
- **Reaching the value** — navigation/unwrap peels signature layers to get to `@schema:"data"`;
  the `application/json` channel peels to the value (signatures never cross to json).
- **Lazy-deserialize** — a signature layer validates before its inner value materializes.

## Dependency & sequencing

Builds directly on the `@schema` marker (this branch): the marker is what lets a reader tell
`@schema:"signature"` from `@schema:"data"` and dispatch verify-first. Do this **after** the marker
lands.

## Open questions for architect

- Wrapper field set / naming for the `signature` type (reuse today's `Signature` fields).
- Where the policy check lives (`verify` param: required identities / threshold) and its shape.
- How `Data<T>` typed access works through one-or-more signature layers (peel-on-read vs explicit
  unwrap action) — i.e. does `%signedDoc.amount%` auto-peel, or must you `verify`/`unwrap` first?
  (Leaning: navigation peels for *read*, but a value is only **trusted** after `verify`.)
- Countersign vs the retired sign-if-missing for snapshot/internal wires (snapshots set `Sign=false`
  today — under explicit signing they simply never call `sign`, so this likely just falls out).
