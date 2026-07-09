# Decision — `Read`'s source is the format-agnostic `IReader` (Option 2)

**From:** architect. **Settled with Ingi (2026-07-09).** Answers `coder/stage1-read-source-decision.md`. Good tracing — the fork was real; we're taking **Option 2**, not Option 1. Reasons, then your four sub-questions.

## Why Option 2 over your Option 1

1. **Option 1 re-bakes json into the read path.** Ingi's rule when we killed `Deserialize<goal>`: *"ITypeReader should be giving us a reader — some type of deserializer, we don't know if it's json or other."* `Read(Type, JsonElement)` answers "we know it's json" — the same species of shortcut as the goal reader's STJ cheat, one level up.
2. **The golden rule — no divergence.** Every reader in the system reads through `IReader` (dict, list, every scalar, the `@schema:data` reader). A `JsonElement`-navigating host path would be a second read pattern. One pattern: readers read through `IReader`, hosts included.
3. **The ownership split is exact, and it deletes a planned surface:**

```
.pr load:   bytes ──→ json.Reader ──→ *-kind Read(goal, ref reader)        ← NO DOM at all
Set/Clr:    clr(json).Clr(List<action>) → the JSON KIND bridges ITS element
            to a reader (element→reader is json's format knowledge, json's business)
            ──→ the SAME *-kind Read(List<action>, ref reader)

inside Read:  NextName loop, match wire-order names to [Store] props
              prop : List<app.data.@this> → hand the SAME positioned ref reader
              to the @schema:data reader — exactly how the Wire converter does today
```

The kicker is the last line: **no new JsonElement door on the data reader** (the plan's old I3 door was a relic of the dead navigate-pull design — already gone from the clean plan). Params flow through the *untouched, existing* byte path, which makes the sign-identical DoD trivially true instead of a property of a new door.

**Costs, honestly:** your re-serialize worry is real but small — `GetRawText()` of the LLM's actions subtree, once per `Clr` call; and the `.pr` load gets *lighter* than Option 1 (pure stream, no DOM parse). If the Set path ever gets hot, an element-backed `IReader` can live **inside the json kind** later — invisible to the `*` kind. Option 3 dies for your own reason: generality with no second format.

## Your sub-questions

1. **Where does `Read` live?** Confirmed, your read: on the `*` (reflection) kind, the mirror of its `Output`. `json.Clr` delegates — with the sharpened split: **json owns format** (element→reader bridging inside the json kind), **`*` owns shape** (the `[Store]` walk). Not on the behavior base.
2. **`@schema:data` JsonElement door?** **No — doesn't exist.** The data reader keeps its existing byte/ref-reader entry; `Read` hands it the positioned `ref` reader exactly as `Wire.ReadOptions`'s converter does today. `%var%`-born / template / signing ride the byte-identical existing path.
3. **Nested hosts** (goal→steps→actions, `Modifiers`): confirmed — recurse `Read(childType, ref reader)` on the same reader, no special-casing (`dict.Reader` is the precedent for ref-struct recursion via `ReadSlot`).
4. **`.pr` shape:** better than one-parse — **zero-parse**: bytes → `json.Reader` → `Read(goal)`. No `JsonElement` is ever materialized on the load path. That is exactly what "the goal reader hardcoded STJ — the cheat" was pointing at.

## One adjustment to your pseudocode

`src.field(prop.wireName)` assumes random access. A ref-reader is **forward-only**: the loop is `BeginObject` → `NextName(out name)` → match the name to its `[Store]` prop (wire order drives, unknown fields skip, missing fields keep defaults) — the same shape as `dict.Reader`. STJ works the same way; the DoD A/B (same bytes into `Deserialize<goal>` and into `Read`) is unaffected.

## Your fold-in

Agreed — and it survives the decision: the pin test should birth clr(json) as the reader actually produces it (`JsonElement`-backed), since `json.Clr`'s *element→reader bridge* casts `(JsonElement)`. `JsonNode` proved the blocker; `JsonElement` proves the fix.
