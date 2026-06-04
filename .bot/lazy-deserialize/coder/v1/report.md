# coder — lazy-deserialize — v1 report

## Status: Stages 1 & 2 COMPLETE (green, pushed). Both design calls resolved. Stages 3–5 remain.

Branch `lazy-deserialize`, commits pushed on top of test-designer v1.2:

- `2b8eb71b6` — reader registry floor (`app.type.reader.@this` + `ReadContext` + `path.Read`)
- `20840687c` — additive per-type Read entries (number, image, object/json)
- `efe92b37a` — distribute `OwnerOf` onto families (`OwnedClr` declarations)
- `7873f4f1f` — single json `Converter` for mid-graph path; delete `path.JsonConverter`; rewire 6 sites
- `04c13d6c3` — resolve converter-fold tail (architect call) + type-owned duration/hash reads
- `7ce095761` — **Stage 2 numbers Way 3** — exact-CLR storage, full tower, promote-then-narrow

Build clean. **Zero regressions** — Serialization 321, DataTests 303, Types 301, Core 251 all green. Remaining lazy stubs (Stages 3–5) fail by design.

## Stage 2 — DONE

NumberKind redefined to the full tower, derived from the boxed value's CLR type; `_i/_d/_f` union + float→double collapse gone. Promote-then-narrow with a signed-biased integer ladder (int+int→long on overflow, uint+uint→long). `NumberPolicy` repurposed per the architect (Q2): Overflow{Promote=narrow-default, Throw=strict-width}; Precision{Error=default on double⊕decimal, Double/Decimal=override}. 4 existing math tests updated to Way-3 expectations (documented in each). NumberTowerTests 39/39.

## Both design calls — RESOLVED

- **Q1 (fold):** architect chose (a) — TimeSpanIso8601/ErrorWire/HashDataConverter stay registered only where their semantics apply; type-owned `duration`/`hash` reads live in the registry; format-layer STJ converters stay. Deletion tests flipped. TimeSpan two-wire-form inconsistency captured as a todo (`Documentation/Runtime2/todos.md`).
- **Q2 (Way3 vs policy):** Way 3 wins on defaults; `NumberPolicy` repurposed (not inert) — see Stage 2 above.

## Stage 1 — what's green

- `ReaderRegistryShapeTests` (9/9), `DistributedOwnerOfTests` (6/6), `ResidualTryConvertTests` (3/3), `SnapshotCarveOutTests` (4/4).
- `PerTypeReadEntriesTests` 5/8 (path, number/int, number/biginteger via `*`, image/png, object/json).
- `TypeOwnedReadParityTests` 3/6 (number, object/json, path).
- `ConverterDeletionsTests`: PathJsonConverter gone, TypeJson stays, single `Converter` exists + routes mid-graph, **nested-path-three-levels-down** (the regression I caught), registration-sites-now-wire-Converter.
- `ReadFailureTests`: registry-returns-null.

## Stage 1 — remaining (the deferred fold) — OPEN DESIGN CALL #1

`TimeSpanIso8601` / `ErrorWire` / `HashDataConverter` deletion is **not** safely a "fold into one universally-registered factory." They have **site-specific** semantics:
- The plang wire writes `TimeSpan` as `"c"` (via `IWriter.TimeSpan`); `TimeSpanIso8601` writes ISO-8601. Different canonical forms in different bags.
- `ErrorWire` lives *only* in snapshot options; it's polymorphic over `IError` with a `$type` discriminator.
- `HashDataConverter` is object-shaped (`{type,value}`), in signing only.

A single factory registered everywhere `path` is would start emitting ISO-8601 TimeSpans / ErrorWire-shaped IErrors on wires that never used them → behavior change, breaking the "no behavior change" bar. Also: path/TimeSpan are *string-shaped* (route cleanly through `type.Read`), but Error/hash are *object-shaped* (don't fit a string `Read`).

**Options for the architect:**
- (a) Accept these three stay as specialized converters (the format-coupled *names* remain) — flip the 3 `*_TypeIsGone_OrFolded` tests to assert "registered only where its semantics apply."
- (b) One factory, but **per-site configured** about which types it handles (defeats "one converter, types never enumerated").
- (c) Fold only the string-shaped ones (TimeSpan) carefully per-bag; leave Error/hash as-is.

Until resolved, the 3 deletion tests + 3 parity rows (Error/Hash/TimeSpan) stay red. Reader entries `duration/iso8601` and `hash/default` depend on this too. `table/csv` is genuinely Stage 4.

## Stage 2 (numbers) — scoped, NOT started — OPEN DESIGN CALL #2

Blast radius is **fully contained**: `NumberKind` and the slot accessors have zero consumers outside `app/type/number/`. The rewrite touches only the `number/` partials + the `data` stamp (`data/this.cs:242`). The atomicity is fine — I'd rewrite them together and only commit when green.

The open call: **Way 3's promote-then-narrow conflicts with the existing `NumberPolicy`** that `math.*` depends on.
- Existing model: `OverflowMode` (Int overflow→Long→Decimal under Promote, or Throw) + `PrecisionMode` (Decimal×Double → Double or Decimal).
- Way 3: integers promote to `BigInteger`, compute, **narrow to the smallest kind that fits** — so they *never* wrap and `OverflowMode` becomes moot for integers; `double⊕decimal` is a **hard error**, superseding the `PrecisionMode` swing.

So Way 3 **supersedes** `NumberPolicy`'s two axes and **changes `math.*` observable behavior** (and likely its existing C# arithmetic tests, which pin the policy-based promotion). Need a call: does Way 3 replace `NumberPolicy` (keep the struct for signature-compat but make its axes no-ops / remove), and are the existing math arithmetic tests expected to change to Way-3 semantics?

My default if you want me to just proceed: Way 3 wins, `NumberPolicy` axes become no-ops (struct kept for `math.*` signatures), update the affected math C# tests to Way-3 expectations, document each changed expectation.

## Stage 3 — CORE DONE (commit `0f19d1430`), Wire-lazy tail OPEN (design call #3)

The lazy `Data` mechanism is landed and green, **inert for existing flows** (nothing sets `_raw` until Stage 4): `_raw` slot, lazy `.Value` materialize (only when `_value` null & `_raw` set), `_raw` survives materialization, mutation invalidates it, `ConvertValue` folded into the `Materialise()` seam, touch-time errors cached as `Data.Error` naming the source. `FromRaw(raw, type, context)` factory. Zero regression (DataTests 303, Serialization 321, Core 251, Types 301, Goals 4). LazyDataTests 19/26.

The deferred 7 rows (`WireReadLazy` ×6 + `AfterMutation`/`RawBackedSerialize`) need the **lazy `Wire.Read`/`Wire.Write`** rewrite, which has an unresolved wrinkle worth an architect call:

**Design call #3 — lazy `Wire.Read` for untyped value slots + nested Data.** The plan says `Wire.Read` captures the value slot's raw json into `_raw`, stamps type/kind from the type slot, and defers; `LiftDataIfShaped` deletes. But:
- **Untyped value slots** (the common case — `{name:"x", value:5}` with no `type`) have no kind to materialize toward. Deferring them means `.Value` can't reconstruct the primitive/list/dict. My proposed resolution: defer **only when the type slot is present**; keep eager `Deserialize<object?>` for untyped slots (cheap primitives/lists/dicts that need no type-driven read). Verbatim passthrough then applies to typed values (config.json → `{object,json}` → raw deferred), which is the payoff anyway.
- **Nested Data in a bare value slot** (Data-wrapping-Data) is what `LiftDataIfShaped` rehydrated. The architect says the containing *type's* reader rebuilds it (e.g. `Signature` rebuilds its Data field) — true when the nested Data is reached through a typed domain field, but a bare untyped value slot holding a Data has no type to drive reconstruction. Deleting `LiftDataIfShaped` outright would turn such an inner Data into a dict (its Signature would stop reaching `signing.verify`). Needs either: nested Data always rides a typed slot, or a retained (non-shape-sniff) reconstruction path.

This touches the serialization core (snapshot/signing/all wire) — I'm not gambling on it without the call. Recommended: my "defer-only-when-typed" resolution + confirm the nested-Data path.

**RESOLVED (architect, 2026-06-03):** both wrinkles confirmed. Defer only when the type slot is present; untyped stays eager. `LiftDataIfShaped` **kept lean** (not deleted) — envelope recognition stays (a leaf's job), only the `GetRawText` double-parse drops. And **signing recanonicalizes** (`ToSigningBytes`) — "verify on raw" was wrong and is removed; a signed Data materializes on verify, signing is left alone. **Landed** (`c94b6dc95`): lean `LiftDataIfShaped` (zero regression), `WireReadLazyTests` aligned (23/26). The remaining 3 rows are the **typed-slot deferral** — folded into Stage 4, because `channel.read` is the natural origin of raw-backed wire Data and carries the context the registry needs to materialize (Wire.Read itself has none). Fully type-driven (a `data` type) is a follow-up todo the architect filed.

## Remaining

3. **Stage 3 lazy `Data`** — `_raw` + lazy `.Value` (materialize via reader when `_value` null & `_raw` set) + mutation-invalidates-`_raw` + `Wire.Read` defers the value slot + delete `LiftDataIfShaped`. Touches the engine's hottest type (`Data.Value`); needs full-suite verification. Also unblocks the 2 Stage-3-coupled `ReadFailureTests` rows + the `MaterialiseErrorPath` rows.
4. **Stage 4 one I/O boundary** — `channel.read` stamps from Mime → lazy Data; file/http become channel kinds; `http.response` dissolves; new `table` type + `(table,csv)` reader; shape-based Mime mapping. (Greens the `table`/`Reader_Of_TableCsv` rows held from Stage 1.)
5. **Stage 5 access-driven resolution** — scalar utf-8 decode, navigate-materializes, `as <type>`, property-never-materializes, type-unknown error, no sniffing.
