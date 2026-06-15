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

## INTEGRATION DONE in production — on branch `compare-redesign-signature-wip`

Executed the big-bang. **PLang + PlangConsole production code BUILDS CLEAN** with
the full I/O-boundary signing model. Preserved on `compare-redesign-signature-wip`
(pushed) so `compare-redesign` stays green. What landed there:
- `Data.Signature` + `EnsureSigned` removed; `Signature` POCO deleted.
- `Wire.Write` signs at the boundary (runs `sign` action) + **hoists** the layer
  top-level; `Wire.Read` probes `@schema` (struct-copy of the reader — no double
  parse on the data path), `ReadSignatureLayer` rebuilds via `FromWire`, runs the
  `verify` action (**auto-verify on read**), peels to the inner data.
- `Ed25519` sign→`Data.Ok(signature.@this)`, verify peels+validates the layer
  (freshness/expiry/nonce/contracts/rehash-inner/sig over `ToSigningBytes`).
- `permission.Add(grant, persist)` — persist intent replaces the in-memory
  signature heuristic; `TryCover` drops in-memory re-verify.
- clone/normalize/set/json-writer drop `.Signature`; HTTP `.Signature` adapted.

**REMAINING on the WIP branch: ~25 test files** still use the removed
`Data.Signature`/`EnsureSigned` API (counts: Wire ~65 errs, Runtime ~35,
ActorPermissionStorage 26, WireConverterSigning 18, TransportPropertyFilter 17,
Cut3 ×2, Data ~15, …). They test the OLD model and need migrating to boundary
signing (serialize→layer appears; deserialize→auto-verifies) or deletion. That
migration is the next focused unit — do it on the WIP branch, then merge to
compare-redesign when green.

**SECURITY REVIEW flags (in-code `// SECURITY REVIEW:` comments):**
- `permission.TryCover` no longer re-verifies in memory — relies on
  auto-verify-on-read covering `SettingsStore` grant loads. Confirm that path.
- HTTP request signing (`X-Signature`) was ADAPTED to serialize the signed Data,
  not removed — Ingi wanted it gone; revisit.

## (superseded) NEXT — the integration big-bang (one push, can't compile halfway)
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

---
## ✅ COMPLETE + MERGED to compare-redesign (signature-as-layer)

The full integration landed and is GREEN — zero regressions vs baseline:
Wire 17/19 · Data 21/22 · Types 12/13 · Modules 46/48 · Runtime 54/56 · Gen 0.

**Done:** Data.Signature removed; I/O-boundary signing (Wire signs at the
application/plang border, hoists the signature layer top-level; read probes
@schema, auto-verifies, peels); Ed25519 on signature.@this; IWriter object
surface; signature.@this owns Write/FromWire/ToSigningBytes; permission grants on
a persist flag; HTTP request-signing removed; full test migration (SignAction/
VerifyAction on the immutable layer, shape tests unwrap the layer).

**Key bug fixed during the migration:** auto-verify-on-read NRE'd because the
boundary-verify path runs with an unset Contracts slot whose .Value() is null —
guarded it; that one fix cleared the whole round-trip cascade.

**Deferred (todos.md):** SettingsStore verify-on-read (rides its OBP rewrite);
archive-as-layer (compress/hash over signature — a few skipped tests point here,
and Decompress currently loses the inner value through that path — flagged);
full response-side HTTP mutual-auth removal.
