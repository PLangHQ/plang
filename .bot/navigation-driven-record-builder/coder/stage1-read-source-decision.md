# Stage 1 decision request — the `Read` source abstraction (for architect)

**From:** coder. **Status:** Stage 0 landed (baseline 129/3816, pin test red, removal
list `[Obsolete]`, all pushed). Stage 1 tracing done; hit one fork that determines the
whole implementation. The plan pins the *mechanism* (reflection `Read`, `clr.Clr` →
`Kind.Clr`) but not the **source** `Read` pulls fields from. Want a call before writing
the deserializer (the plan's own "largest new surface").

---

## What I traced (grounding)

- The kind facade `app/type/kind/this.cs` delegates every verb to `behavior.@this`
  (`Kinds[this].<Verb>`). Verb surface on `behavior/this.cs`: `Navigate`, `Step`, `Data`,
  `Enumerate`, `Set`, `Load`, `Convert`, `Output`. **No `Clr`, no `Read` yet** — both are
  the additions Stage 1 needs.
- The json kind (`behavior/json.cs`) rides on **`JsonElement`** (`ClrForm =>
  typeof(JsonElement)`); all its verbs cast `(JsonElement)`. Its `Set` already
  materializes a json object into a mutable dict; its `Load` parses raw → clr(json).
- `clr.Clr(target)` (`clr/this.cs:126`) calls `ClrConvert` (`item/this.cs:345`) which
  **terminal-throws** ("the type must own this Clr projection") — the pin-test blocker.
- The `*`-kind `Output` (`behavior/reflection.cs:44`) walks
  `Tagged.PropertiesFor(type, mode)` and writes each prop. **`Read` is its inverse** — walk
  the same `[Store]` selector, READ each prop from a source, set it.
- `action.Parameters` is `List<app.data.@this>` (`action/this.cs:44`); the plan routes it
  through the `@schema:data` reader (`app/data/reader/this.cs`), which is a **ref-struct
  byte reader** (`Utf8JsonReader`).

## The fork

```
Read(Type target, SOURCE src, ctx):                         // mirror of *-kind Output
  host = new target()
  foreach prop in Tagged.PropertiesFor(target, Store):       // same selector as Output
      raw = src.field(prop.wireName)                         // ← SOURCE differs per option
      if prop is List<app.data.@this>  → @schema:data reader // params (action.Parameters)
      elif prop is host/collection     → recurse Read(prop.type, childSrc)
      else                             → scalar convert
      prop.SetValue(host, converted)
  return host
```

`Read` serves two callers: **(a)** the `.pr` load, **(b)** `json.Clr` (the write-path
Set conversion, e.g. clr(json) → `List<action>` — the pin test). What is `SOURCE`?

| | SOURCE | .pr load | json.Clr (Set) | cost |
|---|---|---|---|---|
| **1 (rec)** | a `JsonElement` | `.pr` is already json → parse once, reflect-populate by navigating it | already holds the `JsonElement` | one json parse; both callers identical |
| 2 | the byte `IReader` (ref struct) | natural (today's path) | must re-serialize its `JsonElement` back to bytes | awkward for Set |
| 3 | generic `IFieldSource` (json + byte impls) | own impl | own impl | true "one loop, two sources" — most code |

## Recommendation: Option 1 (`JsonElement` source)

1. **The `.pr` on disk IS json** — "parse to `JsonElement`, reflect-populate by navigating
   it" is one natural path; `json.Clr` already has the element. `Read(Type, JsonElement,
   ctx)` — one method, both callers supply it.
2. **It aligns with a piece the plan already committed to.** Stage 1's own worklist has
   *"Reader JsonElement-input door (review I3) reusing the `FromRaw` tail"* — i.e. the
   `@schema:data` reader is already getting a `JsonElement` entry point for params. Under
   Option 1 that door is exactly what `Read` feeds `List<data.@this>` props into; under
   Option 2/3 that planned door is half-orphaned (params come as bytes, host fields don't).
   **Option 1 makes the I3 door the whole story, not a special case.**
3. **The DoD falls out free.** "STJ `Deserialize<goal>` vs reflection `Read`, structural
   equality, while both exist" — both take the *same* json (bytes→element), so the
   round-trip test is a straight A/B on one input.

Option 2 loses on Set (re-serialize round-trip — the exact STJ-stepping-stone the plan
forbids). Option 3 buys format-agnosticism we have no second format for (the `.pr` and the
LLM result are both json); it's the most code for a generality nothing uses yet.

## Sub-questions for the architect (whichever source wins)

1. **Where does `Read` live?** My read: on the `*` (reflection) kind, mirroring its
   `Output` — with `json.Clr` delegating to it (json supplies the element, reflection
   supplies the [Store]-walk). Confirm, vs. putting `Read` on the behavior base.
2. **Does the `@schema:data` reader take a `JsonElement`?** Review I3 says a new
   JsonElement-input door reusing `FromRaw`'s tail. Confirm that's the seam `Read` uses for
   `List<data.@this>` props (so `%var%`-born / template / signing stay byte-identical — the
   sign-identical DoD).
3. **Nested-host recursion** (goal→steps→actions, `Modifiers`): same `Read` recursing on
   the child element + child target type — confirm no special-casing.
4. **`.pr` one-parse acceptable?** Option 1 routes `.pr` load as bytes → `JsonElement` →
   `Read` (replacing `Deserialize<goal>` + the goal `ITypeReader`). Confirm that's the
   intended shape (it's what "the goal reader hardcoded STJ — the cheat" was pointing at).

## Fold-in regardless of the decision

The pin test (`ClrJsonActionsWriteTests`) must birth a **`JsonElement`**-backed clr(json),
not `JsonNode` — the json kind rides on `JsonElement`. I'll switch it to match how the
reader actually produces clr(json). (Current `JsonNode` still reaches `clr.Clr` and throws,
so it proved the blocker, but `json.Clr` will cast `(JsonElement)`.)
