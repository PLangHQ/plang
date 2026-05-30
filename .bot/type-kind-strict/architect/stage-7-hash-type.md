# Stage 7: The `hash` type — algorithm-as-kind

> **Coder/test-designer: you own the final shape.** Settled: `hash` is a first-class type, the algorithm is its `kind`, and verification reads the algorithm off the value instead of taking it as a loose parameter. Whether the value is a `hash.@this` entity or bare bytes + a kinded type, and whether verify lives on the type or stays a crypto action, is yours — the constraint is that the kind carries the algorithm and verify uses it.

**Goal:** Introduce `hash` as a type whose `kind` is the hashing algorithm, so a digest value knows how to be verified. `crypto.hash` produces it; `crypto.verify` reads the algorithm off the value instead of requiring it as a separate parameter.
**Scope:** Included — a `hash` type (`PLang/app/type/hash/`), **registration in the supported type list** (so the builder/LLM and the registry both know `hash` exists — see §"Register in the supported type list"), its `Kinds` vocabulary (the supported algorithms), `crypto.hash` (`code/Default.cs`) returning the structured type, `crypto.verify` reading the kind. Excluded — `encrypt`/`decrypt` (still opaque `byte[]` pass-throughs, no kind); the extension-derived producers (stage 6 — `hash`'s kind is *not* extension-derived).
**Deliverables:** the `hash` type; `hash` listed in the supported types the builder/LLM sees and resolvable through the type registry; `crypto.hash` stamps `{name: hash, kind: <algorithm>}`; `crypto.verify` defaults the algorithm from the hash value's kind.

**Name is settled:** `hash`, not `checksum` (Ingi, 2026-05-30) — these are cryptographic digests in the signing path; a non-crypto CRC would just be a `crc32` *kind* of `hash`.
**Dependencies:** Stages 1–2 (`type.@this` carries `Name`/`Kind`; `Kinds` vocabulary fold). Independent of stage 6.

## Why

`crypto.hash` (`code/Default.cs:65`) returns `Data<byte[]>` stamped `FromName(algorithm)` — the algorithm lands in the *type-name* slot on a bytes value. Two things wrong: the name should describe what the value *is* (a hash), and the algorithm is a *kind*, not a name.

It matters because of verification. `crypto.verify` today (`verify.cs`) makes the developer pass the algorithm *separately* from the hash string:

```
verify %data% against %hash%, algorithm "sha256"   ← algorithm decoupled from the hash; easy to mismatch
```

A digest is meaningless for verification without knowing which algorithm produced it. So the algorithm belongs *on the value* — as its `kind`. Then verify reads it:

```
verify %data% against %hash%      ← %hash% is {name:hash, kind:sha256}; verify recomputes with sha256
```

This is the `image`/`number` pattern (kind that the value owns), not the extension-derived `text`/`file` pattern — which is why it's a separate stage from 6.

## Design

### The `hash` type

A scalar type, folder `PLang/app/type/hash/`, primary class `app.type.hash.@this`. Mirrors the `path`/`image` precedent (a typed value with its own behavior and rendering):

- **Wraps the digest** — the raw `byte[]`. Renders to a canonical string (base64 matches the existing `verify` which does `Convert.FromBase64String`; hex is the other common choice — coder picks, but it must round-trip with verify). `Shape = "string"` (string-shaped scalar, like `path`).
- **`kind` = the algorithm** (`sha256`, `keccak256`). The single owner of the algorithm is the value; the `type.@this.Kind` is stamped from it at construction (one mirrored field — the accepted `image.mime`-style cost, not the #6 flat-copy smell).
- **`Kinds` vocabulary** — the advertised list of supported algorithms (`keccak256`, `sha256` today), via the static `Kinds` property the entity folds. This is *advertised*, not extension/literal-derived, so **no `Build` hook** (unlike `text`/`image`). The LLM emits the algorithm as the kind when the step names it.
- **Behavior on the type** — verification is hash behavior; per OBP it belongs on the value. A `Verify(input)` / equality member on `hash.@this` recomputes from the algorithm and compares. `crypto.verify` can then be a thin caller, or the type owns it outright. (Recompute logic stays shared with `crypto.hash` — don't fork the algorithm switch.)

### Register in the supported type list

`hash` must show up wherever the existing first-class types (`image`, `path`, `number`) do, so the builder/LLM can emit it and the registry can resolve it:

- **Type registry** — `app.type.list.@this` must resolve `app.Type["hash"]` to the entity. Folder-based `@this` types are normally discovered by the catalog walk (`BuildTypeEntries` / the `@this`-convention name); confirm `hash` lands there and isn't dropped (the collision/seed logic in `type/list/this.cs`).
- **The LLM-facing supported-types list** — the vocabulary the builder teaches (stage 5's `app.builder.type.@this` / `TypeSchemas`, and `primitive.@this.BuilderNames` if that's the seam the catalog draws from). Whatever surface currently lists `image`/`number`/`text` as available type names, `hash` joins it, with its `Kinds` (the algorithms) advertised the same way `number` advertises `int|long|…`.
- Don't register `hash` as a *primitive alias* (it's not a string/bytes alias the way the retired `csv` hack was) — it's a real type with behavior, registered like `image`.

### `crypto.hash`

Replace the return stamp:

```csharp
// today
return data.@this<byte[]>.Ok(hashBytes, type.@this.FromName(algorithm));
// stage 7 — value is a hash; algorithm is its kind
return data.@this<hash>.Ok(hashValue, type.Create("hash", kind: algorithm));
```

The unsupported-algorithm error path stays (it already rejects anything outside the switch); the supported set *is* the `Kinds` vocabulary — keep them in one place so the switch and the advertised list can't drift.

### `crypto.verify`

- The `Algorithm` parameter becomes **optional** — when the `Hash`/digest value carries `{name: hash, kind: …}`, default the algorithm from that kind. An explicit `Algorithm` still overrides (and a bare base64 string with no kind still needs it, as today).
- Recompute with the resolved algorithm, compare. Same `SequenceEqual` as today.

## The Data shape this produces

```
hash %data%, sha256  →  data.@this { Name="…", Value=<digest>,
                                     Type = { Name="hash", Kind="sha256", Strict=false } }
wire:  { "name":"…", "type":{"name":"hash","kind":"sha256"}, "value":"<base64 digest>" }
```
