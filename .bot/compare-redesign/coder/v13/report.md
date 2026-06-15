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

## UPDATE — layer machinery COMPLETE + green (committed)

`signature.@this` now owns its full wire machinery, all tested green (Wire 17):
- `Write(IWriter)` — renders `{@schema, type, nonce, created, expires, identity,
  contracts(bare strings), hash:{type,value}, signature, value:<inner>}`.
- `FromWire(JsonElement, options)` (`signature/this.Wire.cs`) — rebuilds it,
  inner `value` through the wire converter, every field born-native.
- `ToSigningBytes()` — deterministic signed-metadata bytes (fixed order; binds the
  inner value via `hash`, not inline). The signing module signs/verifies over this.
- `Signed(binary)` — NOT yet added; add a copy-with-signature helper for SignAsync.
- Tests: `SchemaLayerFormatTests` (shape + round-trip + ToSigningBytes determinism).

## Ingi's signing model (drives the integration)
Signing is **I/O-boundary only**; `Data.Signature` is removed and NOT replaced.
Clone/HTTP/set just DROP signature handling. A Data crossing the `application/plang`
boundary is auto-signed (one layer over the whole payload); read auto-verifies+peels.
Permissions: a persisted grant was verified on load, so the in-memory `.Signature`
checks drop. See `schema-layer-design.md` "Signing is I/O-boundary ONLY".

## NEXT — the integration big-bang (one push, can't compile halfway)
Deleting `Data.Signature` breaks ~12 files at once. Do all together, build at the end:

1. **`Ed25519.SignAsync`** → build a `signature.@this` wrapping `action.Data`
   (hash the inner via the `Hash` action → `layer.Hash`; sign `layer.ToSigningBytes()`
   with the identity priv key → `layer.Signed(binary)`), return `Data.Ok(layer)`.
   Add `signature.@this.Signed(binary)`. duration→TimeSpan for `expires`;
   `Contracts = await action.Contracts.Value()` (native list, no Lower). Drop headers.
2. **`Ed25519.VerifyAsync`** → `action.Data.Peek() is signature.@this layer` else
   NoSignature; freshness/expiry/nonce/contracts; rehash `layer.Value` via `Hash`,
   compare `layer.Hash`; `Verify(layer.ToSigningBytes(), layer.Signature.Value, layer.Identity)`.
3. **Delete `Data.Signature`** property + `EnsureSigned` (`this.Transport.cs:25-65`).
4. **Drop `.Signature` at every consumer** (deletions, not rewrites): `this.cs:1168`
   (clone), `this.Normalize.cs:102`, `variable/set.cs:340`, `permission/this.cs:99,144`,
   `json/writer.cs:106-113` (the signature-field write), `path/http/this.cs:448-450`
   + `http/code/Default.cs` (HTTP request signing + `!ServiceIdentity` — feature goes).
5. **`Wire.cs`**: remove `EnsureSigned`-walk / `EnsureInnerSigned` / `MarkOuterForHash`
   / the `signature` read+write field. Add: **write** — if `Sign && Actor` and
   `data.Peek()` is not a layer, run the `sign` action, then `data.Peek() is <layer>`
   → `jsonWriter.Value(layer)` (hoist, top-level) and return. **read** — in `ReadBody`
   / `item.serializer.json.Parse` Object case, `@schema=="signature"` →
   `signature.FromWire` → run `verify` action (auto-verify; fail → error Data) → peel
   to `layer.Value`. Top-level read has `_context` for verify; nested needs context threaded.
6. **Delete `Signature.cs`** POCO.
7. **Migrate tests**: un-`[Skip]` + rewrite `OuterSignature`, `StoreView`, `Cut2`,
   `Cut3`, `NestedSignedData*`, `SignedDataInListLiteral`, `Canonicalization*`,
   and the existing signing tests that set/read `Data.Signature`.

## NEXT-NEXT — archive-as-layer rides the same hoist/dispatch rails.

## OLD plan (superseded by the UPDATE above)
### the disruptive unit (green-to-green, do together)
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

---
## Ingi clarifications (continuation on `compare-redesign-signature-wip`)

**Permission model (confirmed + a real gap found).** Flow: user answers → `sign
%answer% → %signedAnswer%` → stored in DB → on **read from DB it must validate;
invalid → nothing returned (absent)**. So `TryCover` correctly drops the in-memory
re-verify. BUT the gap: `SettingsStore`/`Sqlite.cs` uses a **context-less**
`new plang.@this()`, and `IStore.Load(...)` takes no actor context. Write-signing
works (uses the grant Data's own context), but **read does NOT auto-verify**
(`ReadSignatureLayer` skips verify when the Wire `_context` is null). FIX: thread an
actor read-context into `SettingsStore.Get/Load` (or make the store per-actor) so
`verify` runs on grant load and an invalid signature yields absent. SettingsStore is
the app-level singleton `app.this.cs:178`.

**HTTP — FULLY REMOVE (not adapt).** Ingi: signing is the channel I/O border's job,
keyed to **`application/plang`** (NOT application/json — json is plain/unsigned). An
http request/response whose content-type is `application/plang` is signed/verified by
the channel's plang serializer (the rewired `Wire`) automatically. So delete ALL
signing code from the http module: `SignRequestAsync`, `ApplySignature`, the
`!ServiceIdentity` extraction, `TryExtractSignedErrorIdentity`, and their call sites
in `http/code/Default.cs` + `path/http/this.cs`. The body already serializes through
the content-type's channel serializer.

**Remaining continuation (this branch):**
1. Thread actor read-context into SettingsStore so grant loads verify (permission gap above).
2. Fully remove http-module signing (above).
3. Migrate ~25 test files off `Data.Signature`/`EnsureSigned` to boundary signing.
4. Build green → merge to compare-redesign.
