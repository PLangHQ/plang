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

## Order

1. `Template` on item + text door + Cacheable gate (no callers yet) — green.
2. Stamp seams (.pr walk + FromWire) — green (sniff still ungated: stamps and
   sniff coexist, behavior unchanged).
3. Gate the sniffs on the stamp — the behavior flip; full gates here.
4. Async Write conversion.
5. Boundary gates + commit/push.
