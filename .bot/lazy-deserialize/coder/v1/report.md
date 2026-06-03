# coder — lazy-deserialize — v1 report

## Status: Stage 1 floor landed (green, pushed). Consolidation + Stages 2–5 remain.

Branch `lazy-deserialize`, two commits pushed on top of test-designer v1.2:

- `2b8eb71b6` — reader registry floor (`app.type.reader.@this` + `ReadContext` + `path.Read`)
- `20840687c` — additive per-type Read entries (number, image, object/json)

Build clean (0 errors, pre-existing warnings only). No existing behavior changed — both increments are purely additive.

## The mid-graph knot (resolved before coding)

Traced the read-side incumbents and surfaced one real design conflict: the plan said delete `path.JsonConverter` outright, but a payload-level reader registry can't serve a `path` field STJ hits **mid-graph** (nested three levels down in a CLR object). Raised it; architect resolved with **one json `Converter`** (`channel/serializer/json/converter.cs`) that talks STJ and routes any plang-typed field to its `Read`/`Write` via the registry — `ErrorWire`/`HashDataConverter`/`TimeSpanIso8601` collapse into it, `type.json` stays (it reads the type *descriptor*, not a value). The nested-path round-trip test was added (credited). Design now in `plan.md` "Mid-graph fields — one json `Converter`", `stage-1` §5.

## What's green

- `ReaderRegistryShapeTests` — all 9 (type exists, `Of`/`Register` signatures, `AnyKind == renderer.AnyFormat`, delegate sig, precedence runtime>generated and exact>wildcard, null on miss, discovery of static `Read`).
- `PerTypeReadEntriesTests` — 5/8: path, number/int, number/biginteger (via Default `*` fallback), image/png, object/json. (Failing: table/csv → Stage 4; duration/iso8601 + hash/default → deletion increment below.)
- `TypeOwnedReadParityTests` — 2/6: NumberRead, ObjectJsonRead. (Failing: PathRead, HashRead, ErrorRead, TimeSpanRead → deletion increment.)

## Design decisions made (coder owns these per verdict)

- **Reader keys on `(type, kind)`**, mirror of renderer's `(type, format)`; `AnyKind = "*"`; precedence identical to renderer. Discovery is the renderer's namespace-scan with `Write`→`Read` and a 3-arg signature `Read(object raw, string? kind, ReadContext ctx)`.
- **`ReadContext`** is a record wrapping the actor context — room to grow (target CLR hint, source channel) without re-threading every `Read`.
- **Per-type `Read` = thin adapter over each family's existing `Convert` hook** (number, image) — literal "re-house, don't reimplement." `path.Read` lifts `path.JsonConverter.Read`'s body. `object/json` re-houses the inline `type.Convert("json")` json→dict read into `app/type/object/serializer/json.cs`.

## Remaining Stage 1 (consolidation) — in the order I'd take it

1. **Distributed `OwnerOf`** (`app/type/convert/this.cs:58`). Each family declares the CLR types it owns (`number` → numeric tower, `text` → string, `path` → subclasses, `image` → byte[]); the central `if u==typeof(int)…` ladder dies; routing composes from declarations. Well-scoped, unblocks Stage 2. Greens `DistributedOwnerOfTests`. Needs a discovery mechanism for family declarations (suggest a static `Owns` surface per family + a composer in `convert`).
2. **duration/iso8601 + hash + error Read entries** — additive like the others, but they couple to the deletions below (the converters being deleted are the parity reference). Add `duration/serializer/Default.cs` (or `iso8601.cs`) lifting `TimeSpanIso8601.Read`; hash Read lifts `crypto.hash.FromWire`; error Read lifts `ErrorWire.Read`. Then fill PathRead/HashRead/ErrorRead/TimeSpanRead parity.
3. **The single json `Converter` + deletions + 6-site rewiring** — the risky core. Build `channel/serializer/json/converter.cs` (STJ `JsonConverterFactory` routing any plang-typed value to its `Read`/`Write` via registry/`OwnerOf`, built per-actor with context). Delete `path.JsonConverter`, `ErrorWire`, `HashDataConverter`, `TimeSpanIso8601`. Rewire the 6 registration sites (`Diagnostics/Format.cs:31`, `channel/serializer/Json.cs:47`, `channel/serializer/plang/this.cs:51`, `module/builder/this.cs:50`, `app/this.cs:420`, `type/list/Conversion.cs:42,64`) to wire the `Converter`. **This is where the "no behavior change" bar bites** — must run the full existing suite green before/after with no expected-output edits. `IReader` + `json/reader.cs` (layer-1 decode surface) land here too, used by the `Converter`.

Then Stages 2 (numbers), 3 (lazy Data), 4 (one I/O boundary + table type), 5 (access resolution). Stage 2 can land right after the distributed `OwnerOf`.

## Notes / flags for next session

- `Reader_Of_TableCsv` and `ReadCsv_LandsAsTable.test.goal` are in Stage-1 test files but `table` is genuinely Stage 4 — those rows green when the table type lands.
- Cross-stage coupling: several Stage-1 PerType/parity rows reference converters that only fully resolve in the deletion increment; that's expected, not a regression.
