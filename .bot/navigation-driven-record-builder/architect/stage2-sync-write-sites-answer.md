# Decision — no sync json-render exists, because nothing needs one (B, sharpened)

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/stage2-json-sweep-sync-write-sites.md`. Your lean (B) confirmed, with each site pushed one step further — the result: **zero sites need a sync json-render**, so (A)'s parallel sync container-emit path is never built and (C)'s partial strip is unnecessary.

## 1. `dict.Clr` — the json round-trip DIES; the replacement is a structural walk

What it does today: lower a dict to `Dictionary<string,T>` by **serializing to json and parsing back** — an in-memory operation using serialization. That's the internal-round-trip smell and a direct violation of the "json is I/O only" ruling; the sync/async collision you hit is the design telling you so.

```csharp
// dict.Clr, the map-lowering arm — AFTER: no serialization anywhere, sync by nature:
var map = (IDictionary)Activator.CreateInstance(target)!;
foreach (var entry in Items)
    map[entry.Name] = entry.Peek() is item.@this iv ? iv.Clr(elementType) : entry.Peek();
    // each value lowers ITSELF via the sync lowering door — recursive, structural, no json
```

- **This SUPERSEDES the map-lowering reroute in `stage2-json-channel-write-answer.md`** ("our writer emits → STJ parses the plain-CLR side") — that reroute quietly carried the same sync/async collision; the structural walk has no json at all.
- Record-use: already dead (Create). The untyped fallback: rides its existing SettingsStore/Identity todo.

## 2. `diff` (`SerializeForComparison`) — goes async

Trace the caller chain first (the watch/diff computation — likely already async territory); then it drives the async writer like everything else. If the chain surprises (a genuinely sync wall), stop and surface — don't invent a sync render for it.

## 3. `diagnostics` (`Format.Value`) — uses `ToString()`

A debug formatter needs a *readable* value, not canonical json — and every value owns a sync string face. Simpler than what's there (the `try { Serialize } catch { type.Name }` defensiveness collapses), no async ripple, no json.

## 4. `text.Convert` — nothing to do

Dies with the hub deletion, as you noted.

## The core question, answered

*"Is there a sanctioned sync json-render for a materialized value?"* — **No, and nothing needs one.** Every would-be consumer either had no business serializing (`dict.Clr`), can be async (`diff`), or needs a string rather than json (`diagnostics`). (A) — the parallel sync container-emit path — was the fork; it stays unbuilt.

## Sequencing

- The attribute strip's write-side gate clears with these three + the hub deletion.
- **Read side proceeds in parallel** (your ask — confirmed): `DeserializeAsync → IReader`, `ReadSlot` relocates to the json kind, `Parse` dies with callers rerouted, object-json absorbs into the kind's `Load`.

## Acceptance

- `dict → Dictionary<string,T>` lowering round-trips correctly with zero `JsonSerializer` calls (grep the method).
- diff results unchanged post-async (same structural diffs on the same inputs).
- Diagnostics output remains readable (spot-check the debug formats).
- After all gates: the 13 `[JsonConverter]` attribute lines strip; grep-zero under `type/` (the sweep's write-side close).
