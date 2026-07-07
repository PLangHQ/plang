# clr + a kind/mime registry — host data becomes navigable/loadable/convertible without materializing

**Branch:** `clr-navigators` (off `variable-as-value`).
**Status:** design, for architect review. No feature code written yet (a double-wrap throw guard added, see below).
**Author:** coder (with Ingi, session 2026-07-07).

---

## Confirmed root cause (architect: this is the thing you were tracking down)

You suspected the `llm.query` wrapping and asked whether the `JsonElement→dict` narrowing is
bypassed or must be removed. **Confirmed, with diagnostics — here's exactly what happens:**

1. **The narrowing is NOT bypassed at OpenAi.** A diagnostic at `OpenAi`'s result construction
   showed `result.item = dict` — the JSON *does* narrow to a `dict` there. So OpenAi hands back a
   proper `dict`.
2. **The opacity is a downstream WIRE ROUND-TRIP.** A diagnostic on `Variable.Set` for `plan`
   showed `clrItem=source mint=object` — by the time `write to %plan%` runs, the value is a
   **`source` typed `object`**, not the dict. And a stack diagnostic at the source birth showed
   it's born in **`data/reader/this.cs` (the wire deferred-value read, via `Wire.Read` inside an
   STJ collection)** from a **JSON *string***.
3. **The mis-pick line is `data/reader/this.cs:79-80`** — the SAME line as the variable-as-value
   fix. It chooses the read format **by token shape**: a `String` token → `text/plain`,
   *regardless of declared type*. So an `object`-typed value whose wire form is a JSON string is
   born `source(object, text/plain)`.
4. **`text/plain` never re-parses the JSON.** `source.Value()` → `Text.Read` → the `object`
   reader over a scalar `value.Reader` → it reads the JSON as one opaque scalar, not a parsed
   `dict`. Result: the `{Value:…}` blob, non-navigable → `foreach %plan.steps%` iterates garbage
   → `%planStep.index%` null → `IndexNotSet` at `BuildStep/Start.goal:6`.

Fresh vs cached both reproduce it (cache cleared → still opaque), so it's current, not stale.
The **cached path** (`OpenAi.ParseResultValue`) re-borns the raw response string as
`source(object, text/plain)` directly; the **fresh path** narrows to a dict but the round-trip
(the value crossing the wire) re-stringifies it. Either way the fix is the same layer: **the
producer must stamp the right kind/format**, not have `data/reader` guess by token shape.

---

## The decision (Ingi): don't convert on load — keep it `clr` with `kind`

We are NOT going to eagerly narrow external structured data into `dict`/`list` on load. Instead
the value stays **`clr` carrying the raw host object, stamped with a `kind`** (`json`, `yaml`,
`xml`, …), and it becomes navigable / loadable / convertible **lazily** through a **kind-keyed
registry**. Rationale:

- **Lazy.** Reading `plan.steps[0].index` shouldn't build the whole tree into a `dict`; the
  navigator descends just that path. Materializing a whole object to read one field is wasteful.
- **Uniform across formats.** json/yaml/xml are all "structured host data": `clr` + `kind=<fmt>`
  + one navigator each. No special json path, no per-format eager conversion. Ask the LLM for
  yaml → `clr(kind=yaml)` → the yaml navigator. One mechanism, extends by registering a navigator.
- **The producer owns the kind (the honest source of truth).** `OpenAi` knows what it asked for,
  so it stamps the result: json/yaml/xml → `clr(kind=<fmt>)`; **md/prose → `text` with
  `kind=md`** (scalar text, not a navigable container). It does not parse or convert — the
  registry does, on demand.
- **`application/json`, not `application/plang`.** `application/plang` is the *internal* Data wire
  (`{name,type,value,…}`). The LLM response is plain external JSON — parsing it (when a consumer
  asks) is the `application/json` reader / the json navigator, never the internal wire.

---

## The design — a kind/mime registry (name TBD)

`clr` carries `(hostObject, kind)`. A registry **keyed by kind** provides, per format, the
operations a value needs — and `clr` delegates to it. Three operations (the third is Ingi's
addition, to be worked out with the architect):

```
clr(hostObject, kind)                       // JsonElement/"json", XElement/"xml", YamlNode/"yaml", POCO/"*"
  Navigate(key)  → clr(sub-node)            // NAVIGATE: descend one level, lazy/partial
  Value()        → self OR plang scalar     // (container stays clr; scalar leaf → its plang scalar)
  Clr<T>()       → raw host object          // developer: clr.Clr<JsonElement>()

registry:  kind → { Navigator, Loader, Converter }     // like the (type,kind) reader registry
  "json"  → json  {navigate, load, convert}
  "yaml"  → yaml  {navigate, load, convert}   (later)
  "xml"   → xml   {navigate, load, convert}   (later)
  "*"     → reflection {navigate over public properties}   ← default
```

- **NAVIGATE** — descend a host object one level (`JsonElement.GetProperty`), enumerate a
  container's children (json array → elements), each still a `clr`. Nothing materialized whole.
- **LOAD (read)** — this is the existing reader concern (`application/json` reads bytes/tokens →
  a value). It belongs in the same registry keyed by kind/mime: "how do I read a `json` payload,"
  "how do I read `yaml`."
- **CONVERT** — **Ingi is taking this with the architect.** The idea: the same kind/mime entry
  also knows how to convert the host data into a target plang type on explicit demand
  (`as dict`, `As<T>`), distinct from the lazy navigate. (Relates to the existing per-type
  `Convert` hooks — the value-to-type conversion door — and to `catalog/Conversion.cs`, which
  already converts `JsonElement`→dict/list on `As<T>`.)

**Naming:** should this be a "mime-type registry"? It's keyed by kind (`json`, `xml`) which maps
to a mime (`application/json`, `application/xml`). One registry, three operations, one lookup per
kind. Open for the architect.

### Container-vs-scalar rule (unchanged)

- `clr` wrapping a **container** (json object/array) → stays `clr` (navigable/enumerable);
  `.Value()` = self; C# unwraps with `Clr<JsonElement>()`.
- `clr` wrapping a **scalar** (json number/string/bool/null) → `.Value()` becomes the plang
  scalar. So `planStep.index` navigates to `clr(1)`, and using it as an index resolves to `1`.

---

## OpenAi's change (the concrete fix)

In `OpenAi`'s result construction — **both** the fresh (`context.Ok(TryParseJson…)`) and cached
(`ParseResultValue`) paths — stamp the value from `effectiveFormat`:

- `json` → `clr(kind=json)` holding the raw response (read via `application/json` on demand).
- `md` → `text` with `kind=md`.
- `xml` → `clr(kind=xml)` (or `text/kind=xml`, TBD).
- prose / no format → `text`.

That makes fresh == cached (both stamp the same kind), and `%plan%` becomes a navigable
`clr(kind=json)` → `plan.steps` works → `IndexNotSet` falls.

---

## The throw guards

1. **Double-wrap guard — ADDED (keeper).** `data/this.cs` ctor now throws if a bare `Data` is
   assigned as a value (mirrors the existing `SetValueDirect` guard) — catches the `Data<object>`
   double-wrap footgun. (It did NOT fire for this bug — proving the `{Value:…}` here is the
   `source(object,text/plain)` opacity above, not a Data-in-Data wrap.)
2. **Container-materializes-to-scalar guard — PROPOSED.** In `source.Value`: if the declared type
   is a container (`object`/`dict`/`list`) but it materialized to a non-container leaf, throw. A
   container must never come back a scalar — this class of round-trip loss then fires loudly and
   immediately, right at the point.

---

## Open questions for the architect

1. **The big decision — does the eager `JsonElement→dict` narrowing (`Data` ctor `json.Parse`) go
   away for external structured data?** Under "don't convert on load," llm results / `file.read`
   of `.json` should stay `clr(kind=json)` and NOT hit the eager narrowing (else we're back to
   eager dict). Coder's lean: go all-in on `clr(kind=…)` + navigators for external structured
   data. This is the single biggest call in the design.
2. **CONVERT (Ingi + architect).** Shape of the convert operation in the kind/mime registry, and
   how it reconciles with the existing per-type `Convert` hooks and `catalog/Conversion.cs`.
3. **Registry name + shape.** "mime-type registry"? Keyed by kind → `{navigate, load, convert}`?
   Auto-discovered by namespace like the readers (`app.type.clr.<kind>` or a dedicated registry)?
4. **Navigator interface.** `Navigate(host, key)` + `Enumerate(host)` (yield `(key, item)` for
   `foreach … item= key=`?) + scalar handling. Integer index (`steps[0]`) unified with string key?
5. **Scalar `.Value()` typing.** json scalar → plang type purely by `ValueKind`
   (Number→number, String→text, True/False→bool, Null→null), number's precision from the token?
6. **Write side.** JsonElement is immutable — is `set %json.x% = 5` (mutate a host object) in scope
   for v1, or read/navigate only?

---

## First concrete deliverable

`OpenAi` stamps `kind=json` on json results + the json **navigator** (object→navigable,
array→enumerable, scalar→plang scalar) → `plang build` clears the `IndexNotSet` blocker. A clean,
testable first cut before yaml/xml and before CONVERT.
