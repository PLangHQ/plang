# coder — lazy-deserialize — v1 report

## Status: Stage 1 substantially landed (green, pushed). Two design calls open before Stage 2/the fold.

Branch `lazy-deserialize`, commits pushed on top of test-designer v1.2:

- `2b8eb71b6` — reader registry floor (`app.type.reader.@this` + `ReadContext` + `path.Read`)
- `20840687c` — additive per-type Read entries (number, image, object/json)
- `efe92b37a` — distribute `OwnerOf` onto families (`OwnedClr` declarations)
- `7873f4f1f` — single json `Converter` for mid-graph path; delete `path.JsonConverter`; rewire 6 sites

Build clean. **Full C# suite: zero regressions** — every non-lazy test green (Serialization 321, DataTests 303, Types 301, Core 251, Goals 4, …). The 146 "failures" are all not-yet-built lazy-deserialize stubs (Stages 2–5 + the deferred fold); the only 2 non-stub failures were security path-traversal tests that *timed out under my concurrent-run contention* and pass in isolation.

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

## Next steps once the two calls land

1. Resolve fold (call #1) → green the 3 deletion + 3 parity rows, add duration/iso8601 + hash reader entries.
2. Stage 2 numbers (call #2) → full tower, exact-CLR storage, promote-then-narrow; green `NumberTowerTests`.
3. Stage 3 lazy `Data` → `_raw` + lazy `.Value` + `Wire.Read` defers + delete `LiftDataIfShaped`. (Also unblocks the 2 Stage-3-coupled `ReadFailureTests` error-path rows.)
4. Stage 4 one I/O boundary + `table` type.
5. Stage 5 access-driven resolution.
