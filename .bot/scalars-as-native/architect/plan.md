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
| `bool` | **none** | create `bool.@this` — the truthiness primitive; equality + serializer. (Decided — see below.) |
| `null` | **none** (flows as `Data.Null()`) | create singleton `null.@this` — truthiness (always false), `null == null` equality, bare `null` serializer; hosts null's behavior so the `is null` value-switches dissolve. (Decided — see below.) |

Out of scope: `dict`/`list` (done), `path`/`image`/`code` (already flow as wrappers), the wire format. `object`/`primitive` are registry plumbing, not values.

### Decided — `bool` gets a wrapper

`bool` is special: it *is* the truthiness primitive, and `IBooleanResolvable.AsBooleanAsync()` always bottoms out at a raw `bool`, so the wrapper can't be turtles-all-the-way. **Decided (Ingi): create `bool.@this`** — same compare/equality/serialize surface as the others, the truthiness contract bottoming out at the raw `bool` it wraps. The reason is consistency: `Compare.Family`'s `bool =>` arm and the `is bool` sites dissolve like the rest. Simpler code, no special-case `if` — the value just flows.

### Decided — `null` gets a singleton wrapper

Reconsidered (Ingi): give null a `null/this.cs` (`null.@this`) too. It hosts null's behavior — truthiness (always false), `null == null` equality, bare `null` serialization — so the scattered `is null` / `_value == null` value-switches dissolve exactly like the other scalars, finishing the thesis for null. Two constraints keep it cheap and correct:
- **Singleton.** There is one null; `null.@this` is a shared instance (`null.@this.Instance`), not a per-value allocation. `Data.Null()` stays the factory and stamps the singleton; `null.@this : item.@this`, so it flows in `Data<item>` slots.
- **It is the null *value*, not the absence of a Data.** A Data whose value is null → `null.@this`. A missing variable / `NotFound` / uninitialised read is a null *`data` reference* (no box at all, `IsInitialized = false`) — a different axis that stays a C# null. Don't let `null.@this` try to represent "no Data."

### Decided — the polymorphic value slot is `item`, not `object`

`item` is the apex of the value-type lattice (≈ C# `object`); every value is-a `item` (`type/this.cs` `Is`). So where C# writes `Data<object>` for a genuinely-polymorphic value (the `→ returns data` convention; the double-wrap footgun in CLAUDE.md), the plang type is **`item`** — `object` is only its CLR realization. Note it everywhere: the polymorphic slot is `Data<item>`.

Enforcement — two options Ingi raised:
- **Runtime guard:** reject `Data<T>` whose `T` isn't a known plang value type. Catches misuse, but it's a runtime check.
- **`item.@this` as a base class** all value-wrappers inherit (recommended). Then `Data<item>` is the polymorphic slot and `T : item` is enforced at compile time; `Data<object>` stops being writable for the value case, and the double-wrap footgun dies structurally — `Data` is the *box*, not an `item`, so a `Data<item>` can never nest a `Data`.

Recommendation: **introduce `item.@this` as an abstract base** and fold onto it the shared value contract this branch is already adding to each wrapper — value-equality, `IOrderableValue`, `IBooleanResolvable`, bare serialization — instead of N parallel interfaces. The box/apex rule ([[plang-value-and-type-model]]) pins three points:
- `item.@this` is **abstract** — the apex stores nothing; there is never a bare `item` instance holding a value.
- Collection element slots stay `Data<List<data>>`, **not** `List<item>` — elements are the box, carrying their own name/type/signature.
- Every *value* slot is `Data<wrapper>`, **never** `Data<raw-CLR>` (Ingi): `Data<int>` → `Data<number>`, `Data<string>` → `Data<text>`, `Data<bool>` → `Data<bool.@this>`. Raw CLR (`int`, `string`, `DateTime`) shows up only as the wrapper's backing `.Value`, unwrapped once at the BCL perimeter / conversion leaf. So `Data<T> where T : item` *does* hold for value slots — the win you're after. Two carve-outs, neither a value: `Data<Variable>` (name-binding — a *reference* to a variable, the `IRawNameResolvable` slot) is the lone non-item `Data<T>`, and `[Code] T` isn't a `Data<T>` at all (separate property kind). So the `Data<T>` class can't carry a blanket `where T : item`; the build gate (option a) enforces "value `Data<T>` → `T : item`" and exempts the name-binding slot, while the base class (option b) covers every value. **The two options are complementary, not either/or — do both.**

Scope: this is structural and ripples across every wrapper. It's tightly coupled to this branch (we build all the wrappers here), so it sequences as **step 0** — define `item.@this`, make `number` (the reference wrapper) inherit it, then each wrapper inherits as it's built out. It also turns every typed `Data<int>`/`Data<string>`/`Data<DateTime>` handler param and action return into `Data<number>`/`Data<text>`/`Data<datetime>` — mechanical but broad, folded into each wrapper's build-out. If it bloats the branch it can split to a fast-follow; the `Data<object>`→`Data<item>` rename is the visible deliverable either way. **Confirm fold-in-now vs. fast-follow with Ingi.**

## Seam map + dispositions (the leaf-trace, at seam granularity)

Five seams carry scalar behavior today. Each gets the same treatment: the per-type arm moves onto the wrapper; the cross-type/perimeter logic stays.

1. **Construction (where scalars are born raw).** `data/this.cs` `UnwrapJsonElement` (`String → GetString()`, `Number → UnwrapJsonNumber`, `True/False → raw bool`, `Null → Data.Null()`), the STJ-DOM path, `variable.set` parsing, CLI parsing, action results. **Disposition:** these produce the wrapper, not the raw value — `text.@this`/`number.@this`/`datetime.@this`/`duration.@this`/`bool.@this`; JSON `null`/absent produces `Data.Null()` (type `null`), **not** a null-wrapping scalar, so null stays unambiguous. This is the load-bearing seam: do it **first** so every value is wrapped at birth, then sweep consumers against a world where raw scalars no longer appear mid-flight. (Mirrors how `collections-are-data` built native dict/list at the parse seam first.)

   **The `Unwrap*`/`Wrap*` family is the target, not the tool.** A method named `Unwrap…`/`Wrap…` is the canonical tell of the internal round-trip smell ([[plang-value-and-type-model]]). Producing wrappers *at* the parse seam is what lets these retire: `UnwrapJsonElement` becomes parse-to-native (emits `text`/`number`/`bool`/… directly), not parse-to-raw-then-rewrap. **Delete `UnwrapNewtonsoftToken`** — Newtonsoft is not our serializer (no package ref; the shim only sniffs JTokens by namespace string for a dead v1 path). Verify nothing live still feeds JTokens, then remove it. Full elimination of every `Unwrap*`/`Wrap*` may not all land this round (some sit on other seams), but seam 1 is where the bulk goes — and none should be *added*.

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
- **Implicit-operator double-wrap** — the `Data<object>` footgun in CLAUDE.md ("Action `Run()` returns are typed"). A wrapper with an implicit `object` conversion + `Data<object>` can silently double-box. Keep implicit operators to the *raw backing type* (`text.@this ↔ string`), never to `object`. The `Data<object>`→`Data<item>` decision above kills this structurally where adopted: `Data` is not an `item`, so a `Data<item>` slot can't nest a `Data`.
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
