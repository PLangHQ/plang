# Design: the `@schema` layer model (removing the Data-in-Data clr courier)

**Status:** drafted 2026-06-15, Ingi chose "build @schema layer" as the next work.
Supersedes the interim `archive : item` and the clr Data-in-Data courier. Sibling
to `clr-dissolution-design.md` (role 3 + role 4) and architect `stage-11`.

## The reframe (what I found tracing it)

"Remove the Data-in-Data courier" is NOT "store the inner Data directly somewhere."
The end-state **eliminates plain Data-in-Data**. Per stage-11: *"There is no
'nested Data inside a value slot' concept. One layer."* A Data's value slot
(`_type : item.@this`) never holds another `Data`.

The ONLY legitimate way a Data wraps a Data is through an explicit **layer** — a
self-describing wire object tagged by `@schema`:

```
{ @schema: "signature",  ...sig fields...,  value: <inner schema> }
{ @schema: "encryption", type: <algo>, ...,  value: <bytes of inner schema> }
{ @schema: "archive",    type: <algo>,        value: <base64 bytes of inner schema> }
{ @schema: "data",       name?, type?, value, properties?, signature? }   ← the base, lowest
```

Layers nest; `value` of a layer is recursively another schema; `data` is lowest.
The read side **dispatches on `@schema`** to pick the layer reader; an absent
`@schema` defaults to `data` (already true — `WireSchemaData` is written today).

## What's already there (don't rebuild)

- The wire **writer already emits `@schema:"data"`** for every record
  (`json/writer.cs:63`, `Data.WireSchema`/`WireSchemaData`).
- The writer **already nests a Data cleanly** via `case app.data.@this nested`
  (BeginRecord → Value → EndRecord). A Data inside a list/dict/Properties slot
  already round-trips as a nested record with its own signature — that path is
  fine and stays.
- `archive : item` exists but is an **interim bytes leaf** (`Write → w.Bytes`),
  NOT yet the `{@schema:"archive", type, value}` layer.

## Why clr leaks (the bug being removed)

The courier is `SetValueDirect(clr(innerData, label))`. `clr` is **transparent**
(no `[Out]` tags → NormalizeObject reflects every public prop), so its `Context`
back-reference ships → `clr.Context → App → CultureInfo.Parent` cycle. A real
layer item renders itself via `Write(IWriter)` and is never reflected — no leak.

## The live Data-in-Data clr sites (the whole inventory)

| Site | What | Disposition |
|---|---|---|
| `this.Transport.cs:92` Wrap() | category outer wrapping a Data | **DEAD in prod** (zero non-test callers) → delete Wrap/Unwrap, or fold into archive |
| `Wire.cs:269` | read: reconstruct a nested-Data value as clr-labeled | dies — single-layer read has no nested-Data value slot; a layer reads via @schema dispatch |
| `Wire.cs:289` | read: domain property-bag dict labeled with declared name | role-2 declared-label (separate from this epic; rides the read-honors-kind work) |
| `this.cs:548` SetValueDirect(Data) | the courier itself | **delete** (Ingi blessed); only the skipped tests call it directly |
| `Data.Signature` property (11 files, ~33 refs) | the ONE real live Data-in-Data: a signature over a Data | → `@schema:"signature"` layer wrapping the data; **the heart of the epic** |

So the only *live production* Data-wraps-Data is **the signature**. Everything
else is dead (Wrap) or a read-reconstruction of a shape we stop writing.

## The infrastructure both need

1. **IWriter layer surface.** Today IWriter has `BeginRecord(Data)` (the data
   envelope) + scalars/array. A layer needs to emit `{@schema:<name>, <fields>,
   value:<inner>}`. Add a minimal object surface — e.g. `BeginLayer(string
   schema)` / `Field(string name, ...)` / a `value:` slot that recurses / `EndLayer()`
   — OR model layers as a generalization of BeginRecord. (Decision below.)
2. **Read @schema dispatch.** `Wire.ReadBody` peeks the `@schema` field; on a
   layer name it routes to that layer's reader, which reconstructs the layer item
   wrapping its inner schema (read recursively). `data`/absent → today's path.
3. **The layer item types** (`item.@this` subclasses): `archive` (exists, upgrade
   to the layer wire shape), `encryption` (stub today), `signature` (new — the
   redesign).

## Sequencing — three landable increments

**Increment 1 — foundation + archive as the proof (NO signature change).**
- Add the IWriter layer surface + the read @schema dispatch.
- Upgrade `archive` from bytes-leaf to the real `{@schema:"archive", type, value}`
  layer (value = the inner serialized schema bytes, base64). Compress/Decompress
  already produce/consume archive; only the wire shape changes.
- Delete the dead `Wrap()`/`Unwrap()` clr courier (no prod callers).
- Greens: the archive/compress wire-shape tests. Leaves signature untouched.

**Increment 2 — signature-as-wrapper (the big semantic change).**
- `sign` produces a `signature` layer wrapping the data; `verify` peels-and-
  validates; **`Data.Signature` the property is removed** (11 files, ~33 refs:
  Wire, Normalize, Transport, http, Ed25519, variable/set, permission, writer, …).
- Rewrite the skipped Data-in-Data tests (`OuterSignature`, `StoreView`, `Cut3`,
  the `NestedSignedData` family) onto the layer (sign → signature layer), or
  delete the ones that only exercised the obsolete courier.
- This is the `signature-as-schema-wrapper` branch the todo already scoped.
  Changes signing/verify semantics everywhere — own focused run, big diff.

**Increment 3 — delete the courier + clr (Data-in-Data arm).**
- `SetValueDirect(Data)` and the `Wire.cs:269` nested-Data clr reconstruction go.
- The remaining clr roles (declared-label role 2, POCO role 1, parse-fail role 5)
  are the *other* clr arms — separate from the Data-in-Data epic.

## FINDING (2026-06-15) — archive-layer is COUPLED to signature-layer

"Foundation first" assumed archive-as-real-layer is separable from the signature
redesign. Tracing the wire contract, **it is not.**

Today a compressed Data serializes as the **data envelope**:
`{@schema:"data", type:archive, value:"<base64>", signature:…}` (`Cut2_OuterWireJson`
pins this, passing). The **outer Data signature signs over the archive bytes** —
that is the sign-then-compress integrity property: `Cut2_TamperingValueByte` flips
a base64 byte and asserts outer verify FAILS. The integrity lives on the data
envelope's `signature`, not on the archive.

Flipping archive to a top-level `{@schema:"archive", type, value}` layer **drops
the data envelope, hence the outer signature** — the tamper-fails-verify contract
breaks. The only clean way to keep integrity on a top-level archive layer is to
wrap it in a **signature layer**: `{@schema:"signature", …, value:{@schema:"archive",…}}`.
That is the signature redesign. So:

> **Archive-as-real-layer cannot preserve the tested integrity contract without
> the signature layer. The two land together, or archive stays inside today's
> signed data envelope.**

This vindicates the architect's "build the abstraction with its real consumer"
rule: the real consumer of the layer machinery is signature.

### Revised increments
- **Increment 1 (LANDED):** delete the dead `Wrap()`/`Unwrap()` clr courier.
  (`e86e1e948`.) Real clr-site removal, zero behavior change.
- **Increment 1.5 (optional, safe):** build the IWriter object surface
  (`BeginObject`/`PropertyName`/`EndObject`) + the read `@schema` dispatch hook,
  WITHOUT changing archive's wire shape. Pure machinery, greens nothing, prep for
  signature. Only worth doing immediately before increment 2.
- **Increment 2 (the real work):** signature-as-layer + archive-as-top-level-layer
  TOGETHER. `sign`→signature layer, `verify`→peel, `Data.Signature` removed,
  archive flips to `{@schema:archive,…}` wrapped by a signature layer when signed.
  Rewrites Cut2 / OuterSignature / StoreView / the nested-signed-Data family onto
  the layer shape.

## Open design decision (for Ingi)

Given the coupling: keep archive inside today's signed data envelope and defer ALL
layer work until the signature run, or pull signature into scope now. See the question.
