# Stage 6: Primitive cleanups — datetime / date / time / duration

**Goal:** Retire the half-states in the temporal vocabulary: rebind `datetime` off `DateTime`, give `date`/`time` their own CLR types, and name `duration` for `TimeSpan`.
**Scope.** *Included:* the four rebinds and their folder/table placement, plus the conversion/serializer touch-ups they require. *Excluded:* any new behavior beyond the CLR types' own; culture-aware formatting (deferred to the formatting pass).
**Deliverables (per [plan/types.md](plan/types.md) "mechanical cleanups"):**
- `datetime` → `System.DateTimeOffset` (DateTime banished from production type bindings). Gets a folder: `app/types/datetime/this.cs` (wraps DateTimeOffset) + `this.Parse.cs` (ISO-8601, tz-aware).
- `duration` → `System.TimeSpan`. Gets a folder: `app/types/duration/this.cs` + `this.Parse.cs` (`1.02:03:04` and ISO-8601 duration). The LLM-facing name is `duration`; `timespan` stays a **deprecated alias**.
- `date` → `System.DateOnly`, `time` → `System.TimeOnly`. Table-only entries (trivial CLR wrappers, no folder, no `Build` — no kind).
- Touch-ups: `app/types/Conversion.cs` paths that hardcode the old CLR type; `Wire.Read`/`Wire.Write` DateTime cases → DateTimeOffset (the writer already has `DateTimeOffset`); the `TimeSpanIso8601` serializer stays (type-keyed). A test/builder-example sweep for anything that picked `datetime` against `DateTime`.
**Dependencies:** Stage 1 (registry/folder shape). Independent of the `number`/`image`/`code` stages — can land in parallel with 3–5 if convenient.

## Design

> **You own the code.** Intent, not dictation.

These are rebinds, not new machinery — they need no per-format serializer asymmetry, which is why they're a stage of their own rather than folded into the type stages. The value of doing them on this branch: they close the exact half-state that motivated the whole effort (`IsPrimitive` accepts `DateTimeOffset` but the name table didn't list it), and they let the cleaned-up catalog read coherently next to the new types.

`datetime` and `duration` get folders because they carry real parse/format complexity worth owning (DateTimeOffset's tz-aware ISO-8601 round-trip; TimeSpan's two text forms). `date`/`time` stay table-only — they're trivial `DateOnly`/`TimeOnly` wrappers with no kind and no parse subtlety. None of these four have a `kind`, so none get a `this.Build.cs`.

`duration` over `timespan` as the surface name: PLang devs write prose ("a duration of 5 minutes") and pick types that read like prose. `timespan` survives as a deprecated alias so existing `.goal` files don't break; the catalog leads with `duration` and the docs mention only it.

Watch the DateTime sweep — anywhere a fixture or builder example stamped `datetime` expecting `System.DateTime`, the rebind to `DateTimeOffset` changes the CLR type a downstream conversion targets. Grep for `datetime`/`DateTime` usages and confirm each still round-trips.
