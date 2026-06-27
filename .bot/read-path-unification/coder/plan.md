# Read-path unification — coder handoff

**Authoritative plan:** `../architect/v1/plan.md` (full design, leaf-trace, per-phase demolition, reader-coverage, OBP table). This file is the corrected short handoff — the original dual-path map carried pre-decision framing (`Of` as open, a `clr` reader floor) that the design review overturned.

## You own this

Every line reference and code shape here and in the architect plan is a **suggestion** grounded in the current source. You own the final shape. If a cleaner seam appears, take it — just keep the two invariants below and update both files to match what shipped.

## The two invariants

1. **No value parse at load.** Reading a `.pr` parses the envelope (`name`, `type`, `kind`) and captures the value **raw** — no `JsonDocument`, no DOM. The single parse happens in `source.Value()`.
2. **One door per type.** `type.Read(raw, ctx)` is the only creation path (uses an `ITypeReader` or constructs directly). No `Build`/`Judge` fork, no second `Create(parsed)` step, no `Of` delegate registry.

The center: **`source`** (the one lazy carrier) + **`type.Read`** (the one door — rename of the existing `Deserialize` at `app/type/this.cs:486`, one caller today; delegates to the registered `ITypeReader.Read`). The registry lookup is renamed `App.Type.Reader(name, kind)`.

## The one obstacle (read this before Phase 2)

A `JsonConverter<Data>.Read` has the `ref Utf8JsonReader` but **not the buffer**, so it can't slice a subtree — that's the only reason today's deferred path DOMs (`Wire.cs:397`). Capture rule: scalar token → `reader.GetString()` (no DOM); structured token → record `TokenStartIndex`, `Skip()`, slice `[start..BytesConsumed)` off the `.pr` buffer. The read must own the buffer. Mechanism is yours.

## Phase checklist (demolition detail in the architect plan)

1. **`ITypeReader` only, registry total** — wrap a stored raw as a one-shot `IReader`; delete `Readers.Of` + the delegate registry; make `App.Type.Reader` total (specific readers ‖ one generic default reader = the old `Convert` logic), so it's never null.
2. **`read(IReader)` + `@schema` dispatch + raw capture** — the read entry is `read(IReader)` (format-agnostic, mirror of `value.Write(IWriter)`; `json` is one `IReader`), not `JsonSerializer.Deserialize<Data>`. `@schema` dispatches via `App.Reader(schema)` (no `if signature`; `signature`/`data` are registered readers; `ReadSignatureLayer` → the `signature` reader). The `data` reader pulls `name`/`type`/`properties` via `IReader.Field` and `value` via `IReader.Raw` (raw slice, no DOM) → `source` → holder `Data`. `source` gains `Template`. Delete `IsDeferrableShape` + the eager value branches + the value DOM. Keep a thin `JsonConverter<Data>` STJ adapter.
3. **Thin `source` + the `!IsFinal` narrow** — `source.Value(data) => app.type.Create(this)` (whole carrier in; throws on bad parse, no try/catch); `app.type.Create(source) => App.Type.Reader(source).Read(source)` (total registry: specific reader ‖ one generic default reader = old `Convert` logic; never null, no fork, no `Convert` name); `Data.Value` = `result = await item.Value(this); if (!item.IsFinal) item = result; return result` (field `_type`→`item`); **delete `Cacheable`**, re-point `IsFinal` to "real value" (`false` only for `source`); move `%ref%`→variable into the `text`/`variable` reader (via `ReadContext.Template`).
4. **Delete the forks** — `type.Build`, `type.Judge`, the ctor fork, `Declare`'s fork, **and the value-ctor entirely** (settled: full retirement — every `new Data(name, value[, type])` site moves to the holder ctor or `Data.From`).
5. **Finish context-never-null** — delete `WireLocal` + the `_context==null` tripwire; `Wire._context` non-null.
6. **Fixtures + the 15** — pass because the read is correct, not silenced.
