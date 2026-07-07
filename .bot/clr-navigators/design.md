# clr navigators — a CLR object becomes navigable without materializing

**Branch:** `clr-navigators` (off `variable-as-value`).
**Status:** design, for architect review. No code written yet.
**Author:** coder (with Ingi, session 2026-07-07).

---

## The problem (a real, reproduced blocker)

`plang build` now runs the whole builder pipeline (planner → validate → BuildStep) and dies
at the *original* handoff blocker: `BuildStep/Start.goal:6`

```
- set %step% = %goal.Steps[planStep.index]%   →  IndexNotSet: %planStep.index% is null
```

Traced to the source: `%plan%` (the LLM planner result, `llm.query … write to %plan%`) is
**not navigable**. Debug:

```
%plan%       = {Value: {description: …, steps: [{index:1, actions:[…], …}]}}
%plan.Value% = {Value: {description: …, steps: […]}}      ← navigating .Value returns the WHOLE thing
%plan.steps% = {Value: {description: …, steps: […]}}      ← navigating .steps returns the WHOLE thing
```

`llm.query` → `OpenAi` does `context.Ok(TryParseJson(extracted))`, and `TryParseJson` returns
a **`System.Text.Json.JsonElement`**. So the plan is a `JsonElement` sitting inside a `clr`
carrier, and **navigation no-ops on it** — `.steps`/`.Value` return the whole blob. So the
`foreach %plan.steps%` iterates garbage, `%item%`/`%planStep%` are bogus, and
`%planStep.index%` is null.

The narrow bug is "a `JsonElement` result isn't navigable." The design below is the general
answer.

---

## The insight

`clr` **is** a normal plang value — the closed host-object carrier we already decided to keep
(navigate / write-if-setter / serialize-`[Out]`; `Peek → self`). The fix is NOT to convert a
`JsonElement` into a `dict`/`list` at birth (materializing a whole tree just to read one field
is wasteful, and loses laziness). The fix is to make `clr`'s **navigate / value pluggable by
the CLR object's shape** — a `JsonElement` navigates one way, an `XElement` another, a POCO by
reflection.

So `clr` stays `clr` and just becomes **navigable in place**.

---

## The design

A per-CLR-type **navigator** registry on `clr` (mirrors the reader registry), default =
reflection. The navigator gives a `clr` three capabilities, none of which materialize the
whole object:

```
clr(hostObject)                      // a JsonElement, XElement, YamlNode, POCO, …
  Navigate(key)  → clr(sub-node)     // descend ONE level, lazy/partial
  Enumerate()    → clr(element)…     // walk a container's children, each a clr
  Value()        → self OR plang scalar  (see below)
  Clr<T>()       → the raw host object   (developer: clr.Clr<JsonElement>())

registry:  CLR type → IClrNavigator
  JsonElement        → json navigator
  XElement/XDocument → xml navigator      (later)
  YamlNode           → yaml navigator     (later)
  (default)          → reflection navigator (walk properties)
```

### The container-vs-scalar rule (the crux)

A navigator decides per node:

- **A `clr` wrapping a container (json object/array) → stays `clr`.** `.Value()` returns self;
  it *is* a valid plang value. Navigation (`Navigate`) and iteration (`Enumerate`) work on it
  directly. In a C# action the developer unwraps with `clr.Clr<JsonElement>()`.
- **A `clr` wrapping a scalar (json number/string/bool/null) → `.Value()` becomes the plang
  scalar** (`number`/`text`/`bool`/`null`). A leaf has a natural plang type and nothing left to
  navigate, so it resolves to its scalar there.

That single rule keeps everything lazy and `clr`-native, yet makes leaves usable as plang
values exactly where they're needed.

### The builder walkthrough (why it unblocks)

```
%plan%             = clr(JsonElement object)                 // from llm.query
%plan.steps%       = Navigate("steps") → clr(JsonElement array)     // partial, no dict build
foreach %plan.steps%  → Enumerate() → clr(step-object)…      // each item a clr
%item% / %planStep% = clr(JsonElement step object)
%planStep.index%   = Navigate("index") → clr(JsonElement number 1)
%goal.Steps[planStep.index]%  → the index needs a number → clr(1).Value() → number 1   ✓
```

Nothing materialized a whole `dict`; only the one scalar leaf that got *used* became a plang
value. `IndexNotSet` falls out.

### Extends for free

`read x.xml` → the value is an `XElement` in a `clr` → the xml navigator handles it.
`read x.yaml` → a `YamlNode` → yaml navigator. Anything with no registered navigator → the
reflection navigator (walk public properties, index enumerables). Adding a format = adding one
navigator, no changes to `clr` or the rest of the runtime.

---

## OBP notes

- **Registry = selection; behavior on the navigator.** `clr` doesn't `switch` on the host
  type — it looks up the navigator and delegates. Each navigator owns its shape's navigate /
  enumerate / scalar rules.
- **Default reflection is not the "generic handler beside per-type handlers" smell.** It's the
  honest catch-all for "any CLR object we don't have a specialized navigator for" — one path
  (registry lookup, default if unregistered), not a second execution path competing with the
  specialized ones.
- Fits the existing `clr` host-carrier decision (`.bot/compare-redesign/coder/host-carrier-spec.md`):
  we're making its *navigate/value* pluggable by shape instead of hardwired.

---

## Open questions for the architect

1. **Scalar `.Value()` typing.** Container-stays-`clr` / scalar-`.Value()`-to-plang-scalar is
   the proposed rule. Is a json scalar's plang type decided purely by `ValueKind`
   (Number→`number`, String→`text`, True/False→`bool`, Null→`null`), with `number`'s kind (int
   vs double) derived from the token? Any case where a container *should* eagerly materialize?
2. **Navigator interface shape.** Minimal is `Navigate(host, key, ctx) → item`,
   `Enumerate(host, ctx) → IEnumerable<item>`, and scalar handling. Should `Enumerate` yield
   `(key, item)` pairs (for `foreach … item= key=`)? Should `Navigate` accept an integer index
   (`steps[0]`) uniformly with a string key?
3. **Where the navigator registry lives** — alongside the type/reader registries
   (`app.type.clr.navigator` mirroring `app.type.reader`), auto-discovered by namespace like
   the readers?
4. **Write side.** `clr` already writes via `[Out]`/serialize; does a navigator also own
   *writing into* a host object (`set %json.x% = 5` → mutate the JsonElement)? JsonElement is
   immutable — likely out of scope for v1 (read/navigate only); flag it.
5. **Boundary with the existing conversion path.** `catalog/Conversion.cs` already converts
   `JsonElement` → `dict`/`list`/target when an `As<T>`/`as <Type>` genuinely asks for it. That
   stays (explicit materialization on demand); the navigator is the *lazy in-place* path. Are
   both wanted, or should the navigator subsume the conversion?
6. **Scope estimate.** New navigator registry + interface, the `clr` `Navigate`/`Value`
   dispatch rewrite, the JsonElement navigator, the reflection default, and threading it through
   the born path (`context.Ok(JsonElement)` must land as a `clr` with the navigator, not the
   current opaque blob). Ingi's read: "a feeling it's going to be a lot."

---

## First concrete deliverable

The **JsonElement navigator** alone (object→navigable, array→enumerable, scalar→plang scalar)
unblocks `plang build` at the `IndexNotSet` blocker — a clean, testable first cut before xml/yaml.
