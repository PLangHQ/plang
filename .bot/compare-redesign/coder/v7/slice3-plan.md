# Slice 3 plan — live templates + async Write

Contract: model doc §"Templates and refs — live, resolved at use" +
§"Serialization — Write is async, one walk"; stage doc slice 3.

## The shape

1. **`Template` is an ordinary init-only property on `item.@this`** (string?,
   value `"plang"` — the language tag; the template TEXT is the value itself).
   Set at creation, never after (instance immutability holds — restamping an
   already-created instance means creating a stamped copy and rebinding the
   Data, same rule as `set`).

2. **Stamp at the authored seams** — detection is deterministic code (contains
   `%ref%`), run only where values are builder-authored:
   - goal `.pr` load (post-deserialize walk: steps → actions → parameters /
     defaults / modifiers, recursive),
   - `Action.FromWire` (on-error recovery chains, compile-response rebuild).
   Channel input is NEVER stamped — a user string `"%secret%"` prints
   literally. Containers with nested refs get the stamp too (the container
   knows something inside needs rendering without being walked).

3. **Doors**:
   - `text.Ready()` override: `Template != null` → full-match `%x%` resolves
     the variable and answers through the VAR VALUE's own door (door recursion
     fine); partial interpolates via `Variable.Resolve` (single-pass over the
     INPUT; output never re-scanned). `text.Cacheable => Template == null` —
     the rebind in `Data.Value()` is already gated on `Cacheable`, so
     "cache iff template == null" falls out with no new mechanism.
   - `AsCanonical` / `AsT_Impl`: the `strVal.Contains('%')` SNIFF branches
     become STAMP-GATED (`instance.Template != null`). `TryFullVarMatch`
     stays as a mechanism (full-match vs partial classification) but no
     longer fires on unstamped strings — that is the retirement the stage
     doc names.
   - Container walk (`WalkContainerVars`) gates on the container stamp.

4. **Async Write** — `item.Write(IWriter)` → `async ValueTask`; dict/list
   await entries; a stamped text renders inside its own Write. The STJ
   `[JsonConverter]` path (today's application/plang wire) is the documented
   sync exception and pre-resolves. NOTE: today the dominant wire path IS the
   STJ converter, so the async conversion's reach is the IWriter pipeline
   (json.Writer, per-type renderers). Done after the stamps land; if the
   ripple proves disproportionate mid-slice, it is split out and flagged in
   the summary rather than half-landed.

## Expected fallout

- C# tests that hand-construct `new Data("X", "%var%")` and expect resolution
  are exercising the pre-model sniff; they re-pin by constructing stamped
  (test-side helper), or move to the real path. Each gets judged, not
  blanket-fixed.
- plang suite is the real gate: if a stamping seam is missed, variables stop
  resolving loudly.

## Outcome (2026-06-11)

Steps 1–3 LANDED and gated green (C# all six slices, plang 330/0 real fails):

- `Template` stamp on `item` (init-only, [model: ordinary typed property]),
  `text.Render` door + `text.Cacheable => Template == null`, `Data.Value()`
  renders a stamped answer fresh at every use.
- Authored seams: `goal.list.Add` (every registered goal — .pr load, builder
  output, programmatic composition), `GoalCall.LoadFromFile` (the second .pr
  load path — child-app/test-runner loads bypass Add), `Action.FromWire`
  (recovery chains), `Data.Authored()` (the slot-level seam; test fixtures
  use it as the builder surrogate).
- `StampedForm` covers: text, native list/dict (rebuilt stamped, entries
  restamp in place), wire-read `source` (text-declared raw collapses to
  stamped text), labeled `clr` carrying a text-declared string, `clr`
  carrying a raw CLR container (graph-scanned once at the seam).
- Sniff branches in `AsCanonical`/`AsT_Impl` (string + container walk) are
  STAMP-GATED — unstamped input containing "%secret%" stays literal. That is
  the TryFullVarMatch retirement: the regex survives as the full-vs-partial
  classifier, but nothing sniffs unstamped strings anymore.
- Test re-pins to the model: no-mutation asserts moved from `Value()` (which
  now renders) to `Peek()` (source form intact); two variable.set tests
  re-pinned to reference semantics (ref-free containers stored uncopied —
  slice-4 position, surfaced early by the gating).

Step 4 (async `Write(IWriter)`) is SPLIT OUT, not half-landed: today the
dominant wire path is the STJ converter — the model doc's documented sync
exception, which pre-resolves — so the conversion is wide mechanical churn
(item.Write + ~15 overrides + writer pipeline) with no behavioral win until
the channel path moves off STJ. It rides with slice 5 / its own pass, where
`text.Value` privatization forces the Write surface open anyway.

## Order

1. `Template` on item + text door + Cacheable gate (no callers yet) — green.
2. Stamp seams (.pr walk + FromWire) — green (sniff still ungated: stamps and
   sniff coexist, behavior unchanged).
3. Gate the sniffs on the stamp — the behavior flip; full gates here.
4. Async Write conversion.
5. Boundary gates + commit/push.
