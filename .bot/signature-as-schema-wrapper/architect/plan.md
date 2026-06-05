# Plan — the wire is a registry of schema objects; signatures are one entry

## Why

Today a signature is a field on `Data` (`Data.Signature`), `[Out, Store]`, hand-emitted by the wire converter and validated late. Three problems: the untrusted value is fully deserialized **before** anything checks the signature; one type carries two unrelated concerns (a value and an attestation about it); and there's no clean way to express more than one signer. We also recognize a Data on the wire by **sniffing** its shape (`IsWireShape` looks for value+type keys), which is fragile and can't tell a signature apart from data.

The fix is one idea applied twice. Put an explicit `@schema` name on every wire object and look it up in a registry that says how to deserialize it. `data` and `signature` become two entries. A signature is then a **wrapper**: `{@schema:"signature", …, value:<inner>}` whose `value` is the next schema object down — the data core, or another signature. A reader dispatches on `@schema` and validates a signature **before** touching its payload. `Data` goes back to four clean fields. Multiple signers are nested layers, not a second field. And the same `@schema` registry later absorbs `archived` and `encrypted` (the existing transport wrappers) with no rework — out of scope here, but the design leaves the seam open.

## You own this

The code shapes, file names (`wire/signed.cs`, the `set signing` step's action name, the `is signed by` verb surface), the dispatch converter's internal structure, and where the signing posture is stored are **design intent, not final code**. You own the final shapes. If any seam fights the existing code, flag it back rather than forcing it.

## The settled model

**Wire = schema objects in a registry.** Every wire object is `{@schema:"<name>", …fields…, value:<inner>}`. `@schema` (the name) indexes a registry → which type deserializes it. One dispatch converter, used at the top level **and** on every `value` slot: read `@schema` → registry → `Deserialize<T>` → recurse. There is no shape sniff.

- `data` — the base entry and the default. Missing `@schema` ⇒ `data`. Shape: `{@schema:"data", name, type, value, properties}`; `value` is the leaf payload.
- `signature` — the wrapper entry. Shape: `{@schema:"signature", identity, algorithm, nonce, created, expires, contracts, headers, hash, signature, value}`; `value` is the inner schema object (a `data`, or a nested `signature`). Nesting = a `signature` whose `value` is a `signature`.

**Egress signing = `serializer.Sign`.** The `application/plang` serializer instance carries `Sign`, set when it's built — content-type negotiation for a response, fixed policy for persistence. `Sign==true` wraps the Data in a signature and emits `@schema:"signature"`; `Sign==false` emits `@schema:"data"`. This replaces sign-if-missing. Read never looks at `Sign` — it always dispatches on `@schema`. So signed/unsigned is a **write** choice only.

**Ingress posture = strict/relaxed, set by a step.** The callstack context carries a `Signing` posture, default **strict**, flipped by a plang step (`- set signing to relaxed`). Scope: the goal that sets it **and the goals it calls**, reverting when that goal returns. It applies **only at the I/O input boundary** — data received over a channel (a request, an external read), never internal values like `set %y% = 5`. Strict: received data must carry a valid signature or the read fails. Relaxed: received unsigned data is accepted as plain data. Plang clients sign, so plang→plang never needs the step; only foreign/old callers do. Most public web endpoints will run relaxed — that's expected and explicit.

**Trust = an explicit plang verb.** The runtime only proves a signature is **internally valid** — it matches `hash(inner)` under the public key it claims (`Identity`), so it's tamper-evident. On success `%x%` is just the data; the signature is invisible. **Who** you trust is plang policy: `verify %x% is signed by %publicKey%`, written where the program cares. No required-identity parameter in the runtime.

**In-memory provenance.** After a validated read peels the wrapper(s), the validated layers are kept on the resulting Data as `Data.Signature` — now `List<Signature>` in signing order (innermost signer first), `[JsonIgnore]`, never serialized. It exists as the substrate the `is signed by` verb reads (and `%x!Signature[0]%` if we keep that navigation surface). On egress a peeled data emits `@schema:"data"`; re-signing is `serializer.Sign` / explicit `sign`.

## Leaf-trace — incumbents and their disposition

**Construction seam (who builds a signed thing) vs validation seam (who checks one) are split throughout: today both live inside `Signer`; the rewrite keeps validation in `verify`, moves construction to the wrap, and moves the "should this be signed" decision out to `serializer.Sign`.**

1. **`Data.Signature`** — `this.Transport.cs:36`, `app.module.signing.Signature?`, `[JsonIgnore][In][Out, Store]`, single value, hand-emitted by Wire.
   → Change type to `List<Signature>`. Drop `[In][Out, Store]`, keep `[JsonIgnore]` only. Populated by the read peel, not by deserialization. No longer hand-emitted.

2. **`EnsureSigned()`** — `this.Transport.cs:49`, lazily signs via `RunAction(new sign{Data=this})`, sync-over-async.
   → **Delete.** Signing stops being lazy/implicit. Call sites:
   - `Wire.cs:184`, `Wire.cs:495` (sign-if-missing walk) → delete with the walk.
   - `path/file/this.Operations.cs:436`, `path/this.Authorize.cs:91` (`if (persist) d.EnsureSigned()`) → **persist through a `Sign==true` serializer**. This is the persist/permission-grant seal; grants are always signed, so the persist path builds a signing serializer instead of mutating the Data. **Risk point — needs its own test (persist a grant → read it back → it validates).**

3. **`Wire.cs`** — `Sign` flag (`:493`), manual signature emit (`:569`), signature deser (`:322`), `Signature` copies (`this.cs:1234/1266/1477`, `this.Normalize.cs:95`).
   → Becomes the `wire/` folder. `wire/this.cs` = base: serialize/deserialize a Data as `@schema:"data"`, no signing. `wire/signed.cs` = variant: wrap via `sign`, emit `@schema:"signature"`. The `Sign` flag moves onto the serializer instance and selects the variant. Manual signature-field emit/deser is gone — a signature is now a top-level schema object handled by the dispatch converter, not a sub-field. (No separate `unsigned.cs` — unsigned is just the base.)

4. **`IsWireShape` sniff** — `this.cs:737`, used at `:695/:746`, with `TypeFromWire`.
   → **Delete the sniff.** Recognition is reading `@schema`. The bind path becomes peek `@schema` → registry → deserialize. `UnwrapJsonElement` (`this.cs:1348`) lifts a marked object the same way.

5. **`sign` action** — `sign.cs` → `Ed25519.SignAsync`. Today: get identity, hash `Data.Value`, build `Signature`, sign `ToSigningBytes()`, set `action.Data.Signature = signedData`, return the same Data.
   → Rewrite to **validate-inner-then-wrap**: if `action.Data` is already a signature, run verify on the existing layer(s) first and refuse to wrap an invalid stack (never attest something unverified). Build a new `Signature` whose `value` is the inner schema object; `hash` covers the **canonical bytes of that immediate inner schema object** (not just `.Value` as today); return the new wrapper. Do not mutate a `.Signature` field.

6. **`verify` action** — `verify.cs` → `Ed25519.VerifyAsync`. Today: read `Data.Signature`, then 8 checks (type, freshness, expiry, nonce, contracts, headers, hash-rematch, ed25519 verify) over `Data.Value`, with `SkipFreshnessCheck` for stored artifacts.
   → Rewrite to **peel outermost-in**: for each layer run the same checks, but hash over the layer's immediate inner schema object, down to the `data` core. Any layer invalid → stop. Keep `SkipFreshnessCheck`, freshness, nonce, expiry, contracts, headers as-is per layer. The `is signed by %publicKey%` check is a **thin policy over the validated layer set** (does a validated layer's `Identity` equal `%publicKey%`), not baked into VerifyAsync's pass/fail.

7. **`Signature` class** — `Signature.cs`, flat metadata + `ToSigningBytes()` (deterministic order), `Type="signature"`.
   → Add a `value` slot (the inner schema object). `@schema` becomes the discriminator; fold the redundant `Type` field into it (pre-1.0, no compat to keep — `ToSigningBytes` signs over `@schema`). Keep deterministic field order. `Hash` now covers the inner schema object's canonical bytes.

8. **`Signing` posture** — NEW. A posture on the callstack frame, default strict, inherited by called goals, reset when the setting frame returns. A step action (`signing.set` or similar) flips it. The I/O input path consults it when materializing received data: strict + unsigned-or-invalid → fail the read; relaxed → accept as data. Gated on **provenance** (the Data arrived over a channel), so internal values are never affected.

9. **Schema registry** — NEW. `@schema` name → type + deserializer. Entries this branch: `data` (default), `signature`. Designed so `archived`/`encrypted` register later with no rework.

## Steps

**Step 1 — schema marker + wire registry (the enabler).**
- `@schema` on the Data wire shape; `WireSchema = "@schema"`, `WireSchemaData = "data"` constants.
- Registry: name → type; `data` (default, missing⇒data) + `signature`.
- One dispatch converter on `Data`/`Data<T>` and every `value` slot; delete `IsWireShape`.
- `Wire.cs` → `wire/this.cs` (base, `@schema:"data"`, no signing).
- **Risk point 1 — marker consistency.** Round-trip test through every serialize path (plain STJ, json channel, `application/plang`) proving every Data comes back marked. The marker must be written everywhere and recognized everywhere, or a path silently drops it.

**Step 2 — signatures as schema wrappers + posture + persist reroute.**
- `Signature` gains `value`; registered under `signature`.
- `wire/signed.cs` variant; `serializer.Sign` selects it.
- Rewrite `SignAsync` (validate-then-wrap) and `VerifyAsync` (peel outermost-in).
- `Data.Signature` → `List<Signature>`, JsonIgnore, populated by the read peel.
- Strict/relaxed `Signing` posture; the `set signing` step; the I/O input check.
- `verify %x% is signed by %publicKey%` verb over the validated layer set.
- Delete `EnsureSigned`; reroute the two persist sites to a signing serializer. **Risk point 2 — the persist/permission-grant seal** (test: persist a grant → read → validates).

## Scope

This branch builds the registry + `data` + `signature` only. Do **not** migrate the transport pipeline (`Wrap`/`Compress`/`Encrypt`, the `archived`/`encrypted` outers) — leave the registry open so they can register later. Pre-1.0: no backward-compat shims for the old `Data.Signature` wire field.

## Open for the coder to settle

- The `set signing` step's action surface (a `signing` module action vs a reserved-variable set).
- Exactly where the `Signing` posture lives on the callstack frame and how the I/O input path reads it.
- The `value`-slot converter's structure (the recursive `@schema` dispatch) and how `Signature` declares its `value` so STJ routes it through that converter.
