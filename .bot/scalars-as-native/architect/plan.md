# Plan — `scalars-as-native`

**Branch off `main` (runtime2), after `collections-are-data` merges.** This branch was authored from `collections-are-data` so it reads the latest code; the real branch starts from main once collections lands (it depends on the native-value-type machinery that branch establishes). Land it as its **own** merge to main — do **not** back-merge into `collections-are-data`.

## Why

`collections-are-data` made `dict`/`list` carry their own type and behavior — a value is never decomposed to a raw CLR shape and then re-identified by a type-switch. **Scalars are still half-raw.** A text value flows as a bare `string`, a date as `DateTime`, a duration as `TimeSpan`, a bool as `bool`; only `number` is sometimes its own type. Because the type-tag rides *beside* the value instead of *in* it, code re-derives the type with `is`-switches (`Compare.Family`, `value is string`, `(string)value`) to pick behavior — the OBP type-discrimination smell, ~197 sites across production C#.

The wrapper types already exist (`text.@this`, `datetime.@this`, `duration.@this`) but are **thin shells** — `Value` and a `ToString`, nothing else — and `bool` has no wrapper at all. `number.@this` is the one fully-built example (compare, truthiness, conversion). This branch makes **every scalar value flow as its native wrapper**, so behavior (compare, truthiness, length, casing, formatting, arithmetic) is a virtual member on the type and the `is`-switches collapse into method calls. It finishes the branch thesis — *the value carries its own type and behavior; you never decompose-and-switch* — down to the leaves.

**You own the final shape.** Seams, dispositions, and the type list below are the design anchors; the exact file:line census is re-grounded when the branch opens off main (collections-are-data will have shifted things). Change what reads wrong, keep the dispositions.

## The law (carried from collections-are-data)

A type-discriminating `is`-switch on a value is allowed in exactly **two** places, nowhere else:

1. **The perimeter** — where a value crosses *out* of plang into a C#/BCL API (`JsonSerializer`, `Regex`, a SQL parameter, a hash, math against a raw CLR number) and must unwrap to its raw `.Value`. This is a single `.Value` at a known edge, not an inspect-and-branch — the same "string only at the perimeter, one crossing point" discipline already in CLAUDE.md.
2. **The one binary-coercion mediator** — `"5" == 5`, numeric widening, enum↔string. This inspects the *relationship between two types*, so it can't be a method on one value. One spot (today `Operator.NormalizeTypes` + the `Compare` dispatcher).

Everywhere else, asking *what a value is* to decide behavior is the smell — it becomes a virtual member on the wrapper.

## Scope — the scalar types to promote

| Type | Wrapper today | Work |
|---|---|---|
| `number` | **complete** (`IComparable`/`IEquatable`/`IBooleanResolvable`/`IConvertible`) | reference shape — mostly ensure it always *flows* native |
| `text` | thin (`Value` + implicit `string`) | build out: ops (length/case/contains/substring/split/trim…), compare, truthiness, value-equality, serializer, `[atomic]` (no char-iterate) |
| `datetime` | thin (`Value` + `ToString`) | build out: compare, truthiness, formatting/parts, value-equality, serializer |
| `duration` | thin (`Value` + `ToString`) | build out: compare, truthiness, parts, value-equality, serializer |
| `bool` | **none** | create `bool.@this` — the truthiness primitive; equality + serializer. (Decision below: create vs. leave raw.) |

Out of scope: `dict`/`list` (done), `path`/`image`/`code` (already flow as wrappers), `null` (stays null), the wire format. `object`/`primitive` are registry plumbing, not values.

### Open decision — does `bool` get a wrapper?

`bool` is special: it *is* the truthiness primitive. A `bool.@this` would carry compare/equality/serialize like the others (uniformity), but every `IBooleanResolvable.AsBooleanAsync()` ultimately returns a raw `bool`, so the wrapper can't be turtles-all-the-way. Recommendation: **create `bool.@this`** for uniform flow (so `Compare.Family`'s `bool =>` arm and the `is bool` sites dissolve like the rest), with the truthiness contract bottoming out at the raw `bool` it wraps. Confirm with Ingi before building.

## Seam map + dispositions (the leaf-trace, at seam granularity)

Five seams carry scalar behavior today. Each gets the same treatment: the per-type arm moves onto the wrapper; the cross-type/perimeter logic stays.

1. **Construction (where scalars are born raw).** `data/this.cs` `UnwrapJsonElement` (`String → GetString()`, `Number → UnwrapJsonNumber`, `True/False → raw bool`), `UnwrapNewtonsoftToken`, the STJ-DOM path, `variable.set` parsing, CLI parsing, action results. **Disposition:** these produce the wrapper, not the raw value — `text.@this`/`number.@this`/`datetime.@this`/`duration.@this`/`bool.@this`. This is the load-bearing seam: do it **first** so every value is wrapped at birth, then sweep consumers against a world where raw scalars no longer appear mid-flight. (Mirrors how `collections-are-data` built native dict/list at the parse seam first.)

2. **Compare** — `data/Compare.cs` (`Family`, `Order`, `AreEqualValues`), `Operator.NormalizeTypes`. **Disposition:** the scalar arms (`"text" => CompareOrdinal/ignore-case`, `"datetime" => offset compare`, `"duration"`, the `lf switch`) move onto each wrapper as `AreEqual`/`Order` (the `IEquatableValue`/`IOrderableValue` interfaces from the collections-are-data compare handoff). `Family()` and the `Orderable` HashSet **delete** — orderability is "implements `IOrderableValue`." Coercion stays in the mediator. *This seam is the direct continuation of the compare handoff on `collections-are-data` — that one leaves scalars in a shared C# comparer; this one relocates them onto the wrappers and the shared comparer shrinks to coercion + a thin `IComparable` fallback.*

3. **Truthiness** — already `IBooleanResolvable`. **Disposition:** `text`/`datetime`/`duration`/`bool` implement it (empty text falsy, etc.); `number` already does. The `Data.ToBoolean()` raw-scalar fallbacks (`is string ""`, `is bool`) become unreachable for wrapped values and are kept only for the perimeter.

4. **Serialization** — `data/this.Normalize.cs` + the json writer + per-type `[JsonConverter]`. **Disposition:** each wrapper renders **bare** (`text.@this → "abc"`, `number.@this → 5`, `datetime.@this → ISO`, `bool.@this → true`) on `application/json`, and rides the `application/plang` wire as a self-describing Data — exactly the dict/list pattern (`Normalize` keeps it native, the converter emits the value-only view). Add the wrapper to `Normalize`'s tree-leaf set and give it a converter. **Do not** add a parallel envelope (the "Data is not enveloped" rule).

5. **Conversion** — `type/catalog/Conversion.cs` (value → typed `T`). **Disposition:** unwrap the wrapper to its raw `.Value` at the typed-conversion leaf (like `dict.ToRaw`/`list.ToRaw`), so `→ returns string`/`int`/`DateTime` reconstruct unchanged. One unwrap at the leaf, not at every call site.

## The consumer sweep (the ~197 sites)

Census every `is string` / `(string)value` / `value is int|long|double|decimal|bool` / `is System.DateTime|TimeSpan` in production C#, and classify each — this is the bulk of the branch and where the OBP win lands:

- **Behavioral** (`is string str => str.Length`, `.ToUpper()`, `.Contains()`, arithmetic, date math) → **becomes a method on the wrapper.** The if disappears. This is the majority and the point of the branch.
- **Perimeter** (`is string s` right before `JsonSerializer.Serialize(s)`, a `Regex`, a BCL call) → **becomes a single `.Value` unwrap** at the edge. Legitimate, not a smell.
- **Coercion** (comparing/relating two values of possibly-different type) → **routes to the mediator.** Already covered by seam 2.

Disposition rule: if removing the `is` requires asking the value to *do* something, it's behavioral → method. If it's handing the value to non-plang code, it's perimeter → unwrap. If it's reconciling two values, it's the mediator.

## Risks / footguns

- **Transition leakage.** Mid-sweep, a raw scalar slips through a not-yet-converted producer and a consumer that now expects a wrapper NREs (or vice-versa). Mitigation: seam 1 (construction) first, then a wrapper-side **implicit operator** (`text.@this ↔ string`, like `number`'s and `Variable`'s) so a missed perimeter site still compiles and runs — but treat implicit operators as a transition aid, not a license to leave `.Value` reaches un-swept. Tests are the backstop.
- **Implicit-operator double-wrap** — the `Data<object>` footgun in CLAUDE.md ("Action `Run()` returns are typed"). A wrapper with an implicit `object` conversion + `Data<object>` can silently double-box. Keep implicit operators to the *raw backing type* (`text.@this ↔ string`), never to `object`.
- **String atomicity.** `foreach %s%` must not char-iterate a `text.@this`. The `IsPlangAssignable`/`IsPlangIterable` carve-out (`data/this.cs`) that already exempts raw `string` must be extended to `text.@this` (it is not `IEnumerable`, so it should be safe — verify).
- **Value-equality of wrappers.** Two `text.@this("a")` must be equal (dict keys, `HashSet`, dedup). Every wrapper implements `IEquatable<@this>` with value semantics, not reference — `number` shows the shape.
- **Allocation.** Every scalar boxes into a wrapper. Marginal — values already box into `Data`'s `object?` slot — but measure if a hot list-of-ints path regresses.

## Sequencing within the branch

1. **Census + classify** the ~197 sites (behavioral / perimeter / coercion). Output the list before editing — it scopes the branch.
2. **Build out the wrappers** — `text`, `datetime`, `duration` get their ops + `IEquatableValue`/`IOrderableValue`/`IBooleanResolvable` + value-equality + serializer; create `bool.@this` (pending decision).
3. **Construction seam** — materialization produces wrappers (do this before the sweep).
4. **Compare** — relocate scalar arms onto wrappers; delete `Family`/`Orderable`.
5. **Serialization + conversion** — bare render; unwrap at the conversion leaf.
6. **Consumer sweep** — behavioral → methods, perimeter → unwrap.
7. **Delete the dead** — `Family()`, raw-scalar special-cases in `ToBoolean`/compare, the shared-C# scalar comparer collapses to coercion + `IComparable` fallback.

## Done when

Both suites green; no `is <scalar-type>` / `(string)value` survives outside the two legal sites (perimeter unwrap, coercion mediator); `Compare.Family` and the `Orderable` set are gone; a scalar value read out of a variable is its wrapper, and `→ returns string`/`int`/`DateTime` still reconstruct at the conversion leaf.
