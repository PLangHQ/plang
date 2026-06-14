# v9 — Stage 11: lazy read + lazy containers (store raw, type on read)

Implements `architect/stage-11-lazy-read-and-containers.md` and v9/plan.md.
Part A (wire value-slot read) + Part B (`list`/`dict` raw backings) landed as
one change, per the finding that they interlock.

## What changed (7 files, +243/-148)

**Part B — raw container backings (the heart):**
- `list/this.cs` — `_items: List<Data>` → `List<object?>` (raw-or-Data). A slot
  holds a raw CLR scalar, a native sub-container, or a Data (dropped in by
  `add`/`set`). `Row(i)` materializes-and-caches a raw slot to a Data on first
  touch; `Inner(slot)` is the structural face for the dissolve/locate flatten
  machinery; `AddRaw` is the store-raw seam. Public Data surface (Items/At/
  Count/First/Last/…) preserved.
- `dict/this.cs` — `_entries: List<Data>` + `_index` → `_keys: List<string>` +
  `_map: Dictionary<string,object?>` (case-insensitive). The KEY is the identity
  (off `Data.Name`). `Slot(key)` materializes-and-caches. Public surface
  (Entries/Get/Has/Keys/Set/…) preserved. The `@schema` write-guard moved into
  the single `Put` seam so BOTH `Set` overloads enforce it.
- `item/serializer/json.cs` — `ObjectLeaf`/`ArrayLeaf` now store RAW leaves via
  `RawSlot` (scalar / native sub-container / reconstructed Data for a
  `@schema:data` element) — never a Data per element. New `BornFromRaw` is the
  raw→born-native narrow used by `Row`/`Slot`; it keeps a number's EXACT kind
  (`number.FromObject`, so a long stays a long — the per-element NumberLeaf path
  it replaces did too).
- `list/Json.cs`, `dict/Json.cs` — the raw-STJ Read converters defer to the
  raw-storing `json.Parse`, dropping their per-element-Data loops.

**Part A — single-pass wire value read (`Wire.cs`):**
- The value-slot decode is one `json.Parse(element)` call (scalar wrapper /
  native container with raw slots / reconstructed Data for a marked slot). The
  triple-parse is gone.
- Demolished: `LiftDataIfShaped`, `LiftArrayElements`, `HasDataMarker`.

## Verification — zero regressions

Name-level diff of every C# suite, mine vs `git stash` baseline (this branch
carries many pre-existing failures; the bar is *no new* ones):

| suite | baseline | mine | new |
|-------|---------:|-----:|----:|
| Data | 104 | 104 | 0 |
| Wire | 34 | 34 | 0 |
| Types | 27 | 27 | 0 |
| Generator | 7 | 7 | 0 |
| Modules | 137 | 137 | 0 |
| Runtime | 70 | 70 | 0 |

Five tests asserting the *old internals* I restructured were updated/explained:
- `LiftDataIfShaped_KeptLean_StillExists` → `LiftDataIfShaped_IsDemolished`
  (reflection now asserts the method is gone; the two behavior tests below it
  cover the real contract — a marked nested Data still round-trips as a Data).
- 3 numeric/array tests (`UnwrapJsonElement_IntegerNumber_ReturnsLong`,
  `UnwrapJsonArray_…`, `Materialize_JsonObjectRoot_NarrowsToDict`) were failing
  on a `long`→`double` ternary-widening bug in `RawSlot` (fixed: cast to
  `object`) + the `BornFromRaw` exact-kind narrow.
- `AtSchemaBlocked_AsDictKey_WireMarkerOnly` (the one genuine regression caught)
  — fixed by moving the `@schema` guard into `Put`.

`Upload_ResponseParsed_AsJson` shows as a Modules suite-run swap but **fails
identically on baseline in isolation** — pre-existing flaky, not mine.

Analyzer-on PlangConsole build (PLNG001/PLNG002 gates) is clean.

## Deviation from the architect's demolition list (deliberate, conservative)

The architect's demolition worklist also called for removing `IsDeferrableShape`
+ the `deferredRaw` capture (the source-backed verbatim path) and the
`_readDepth`/`MaxReadDepth` guard. **I kept both**, on purpose:

- **Source-backed deferral is the verbatim mechanism.** For `object/item/table`
  payloads with a real encoding kind (json/csv/xlsx), the value rides as its raw
  bytes and parses lazily — so a *signed structured payload relayed by a courier*
  re-emits byte-identical (no parse-then-reserialize that could reorder keys /
  reformat floats and break the signature). Removing it risks signature breakage
  on relay; it directly serves the "untouched verbatim" non-negotiable. The
  common container literals (`set %x% = 1,2,3,4`, `{a:1}`) are NOT deferrable and
  DO get the Part B raw-slot laziness — both paths are lazy, different mechanisms.
- **`_readDepth` still bounds real recursion.** A `@schema:data` slot re-enters
  `Wire.Read` via `Deserialize<Data>` (STJ resets its own depth), so the
  cumulative guard still earns its keep (security v1 F1).

The architect's doc says the worklist is "design intent, not implementation" and
grants the coder these calls; the non-negotiables (store raw / type on read; one
Data per container; verbatim passthrough; keep `Read(object raw)`, no `IReader`)
all hold. Flagging for the architect to confirm or push the deferral demolition
to a follow-up.

## Not done (deferred, noted)

Write-side verbatim (writer/Normalize iterating raw slots so an untouched native
container re-emits without minting Data) — the architect's worked-trace
optimization. I route writer/Normalize through the materialized public surface
instead, which is byte-identical to today's output (zero write-side risk). The
load-side laziness — no eager Data/wrapper per element — is the realized win.
