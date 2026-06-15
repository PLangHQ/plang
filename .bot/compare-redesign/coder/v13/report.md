# v13 — @schema layer foundation (clr Data-in-Data removal, signature redesign)

**Branch:** compare-redesign. **All committed + pushed, tree green throughout
(Wire 17 / Data 22 / Modules 48 flat — verified no regressions).**

## What this session did

Started the `@schema` layer epic (Ingi: "build @schema layer" → "pull signature
in now"). Removing the clr Data-in-Data courier; the real live case is the
signature. **Design fully locked with Ingi and recorded turnkey in
`.bot/compare-redesign/coder/schema-layer-design.md` — read that first.**

### Landed (committed, green)
1. **Deleted dead `Data.Wrap()`/`Unwrap()`** clr courier (zero prod callers).
2. **IWriter object surface** — `BeginObject`/`Name`/`EndObject` (the stated
   blocker: layers had no way to render `{@schema,…,value}`).
3. **`signature.@this` layer type** (`PLang/app/type/signature/this.cs`) —
   born-native (text/datetime/binary/list + typed `hash`), owns its `Write(IWriter)`.
4. **`SchemaLayerFormatTests`** pin the confirmed wire shape.

### The confirmed format + flow (all in the design doc)
- Layer = `@schema` (which layer) + `type` (which algorithm), uniform across
  signature/archive/encryption: `{@schema:"signature", type:"ed25519", nonce,
  created, expires, identity, hash:{type,value}, signature, value:<inner @schema:data>}`.
- No `headers`. Bytes property is `Signature`. Born-native fields.
- **Invoke via the module/action** (`sign`/`verify` actions), NOT `App.Code.Get<ISigning>`.
- **Write:** sign at the `application/plang` output boundary; writer **hoists** the
  layer to top level (one rule for every layer), recursing into `value`.
- **Read:** `@schema` dispatch → build layer → **auto-verify** (read fails on bad
  signature) → peel → repeat until `data`.
- Signature covers the inner `value` as a separate object → `MarkOuterForHash`
  carve-out + AsyncLocal machinery **dissolve**.

## NEXT — the disruptive unit (green-to-green, do together)
Can't reach green halfway (removing `Data.Signature` requires rewriting
sign/verify/Ed25519/Wire at once). Steps in `schema-layer-design.md`
"Decision taken" §:
1. `signature.@this.FromWire(...)` + `@schema` read-dispatch (`item.serializer.json.Parse`
   Object case / `Wire.ReadBody`).
2. `sign` → produces `signature.@this` wrapping the data.
3. Write hoist in `Wire.Write` (`data.Peek() is <layer>` → `layer.Write`).
4. Remove `Data.Signature` + `EnsureSigned`/`EnsureInnerSigned`/`MarkOuterForHash`.
5. `verify` → peel+verify a `signature.@this`; auto-verify-on-read calls it.
6. `Ed25519` rewritten to build/verify `signature.@this` (no POCO).
7. Delete `app.module.signing.Signature` POCO.
8. Migrate the `[Skip]`'d nested-signed-Data tests (`OuterSignature`, `StoreView`,
   `Cut2/Cut3`, `NestedSignedData*`) onto the layer. Then `archive`-as-layer rides
   the same rails (the archive↔signature coupling finding — see the doc).

`.Signature` refs to clear (~33 across 11 files): `Wire.cs`, `this.Normalize.cs`,
`this.Transport.cs`, `path/http/this.cs`, `http/code/Default.cs`,
`signing/code/Ed25519.cs`, `variable/set.cs`, `actor/permission/this.cs`,
`this.cs`, `json/writer.cs`, `variable/this.cs`.

## Gotchas (carry these)
- Data/Wire suites segfault at teardown AFTER printing — read counts from the log.
- csharp-ls flags TUnit/`Assert`/generated symbols as errors — NOISE; trust `./dev.sh build`.
- `signature.@this.Value` hides the base `Value(asking)` method (CS0108) — a tolerated
  warning, same as `archive.Value`; Ingi wants `Value` for consistency, leave it.
- Production C# edits via Edit/Write only; test edits may be shell-batched.
