# Note for test-designer — sync the contract to the Converter resolution

Your v1.1 contract rebased onto the shape-based typing commit but not onto the **mid-graph Converter resolution** that landed after it (the coder's knot — `plan.md` "Mid-graph fields — one json `Converter`", `stage-1` §5, leaf-trace). Three deltas in `ReaderRegistryTests/ConverterDeletionsTests.cs`:

1. **`TypeJson_TypeIsGone` → invert or drop.** `type.json` now **stays** — it reads the type descriptor `{name,kind,strict}` (the wire `type` slot), not a value, so it's not a value-converter to fold. Flip the test to assert `app.type.json` is *still present*, or remove it.

2. **Add: the single json `Converter` routes mid-graph.** One converter — `app/channel/serializer/json/converter.cs`, class `Converter` — replaces the per-type converters. Pin that it exists and that a mid-graph typed field (path/error/duration) routes to that type's `Read` via the registry / `OwnerOf`.

3. **Add: nested-path-field round-trip.** A `path` three levels down in a CLR object (`As<T>`) deserializes through the `Converter`. This is the regression the coder caught — the load-bearing test for the whole resolution. (Credited to coder.)

Rows 2–3 are already in `plan/test-coverage.md` Stage 1.

**What's already correct, leave as-is:** `PathJsonConverter_TypeIsGone` (path's converter type genuinely deletes), `ErrorWire`/`HashDataConverter`/`TimeSpanIso8601` "gone or folded" (they collapse into the `Converter` + each type's `Read`), and `PathConverterRegistrationSites_NoLongerAddPathJsonConverter` — accurate, with the note that those 6 sites now wire the single `Converter` instead.

You own the contract — these are pointers to where the design moved under you, not edits.
