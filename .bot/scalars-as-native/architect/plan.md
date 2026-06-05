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
| `datetime` | thin (`Value` + `ToString`) | backed by `DateTimeOffset` (accepts CLR `DateTime`); build out: compare, truthiness, formatting/parts, value-equality, serializer |
| `duration` | thin (`Value` + `ToString`) | backed by `TimeSpan`; build out: compare, truthiness, parts, value-equality, serializer |
| `date` | no wrapper; `DateOnly` **folds into `datetime`** today (`ScalarComparer` coerces it to `DateTimeOffset` and classes it `datetime`) | create `date.@this`, backed by `DateOnly` — its own type; **stop the collapse into `datetime`**; compare, truthiness, parts, value-equality, serializer |
| `time` | no wrapper; `TimeOnly` **unhandled** today (`ScalarComparer` has no time arm) | create `time.@this`, backed by `TimeOnly` — its own type; compare, truthiness, parts, value-equality, serializer |
| `bool` | **none** | create `bool.@this` — the truthiness primitive; equality + serializer. (Decided — see below.) |
| `null` | **none** (flows as `Data.Null()`) | create singleton `null.@this` — truthiness (always false), `null == null` equality, bare `null` serializer; hosts null's behavior so the `is null` value-switches dissolve. (Decided — see below.) |

Out of scope: `dict`/`list` (done — but they become `: item`, see below), the wire format. **`object` is *not* out of scope** — it folds into `item` (the un-narrowed/tree type *is* `item(kind=…)`; there is no enduring PLang `object`). `path`/`image`/`code` keep their wrappers but also become `: item`. `primitive` stays registry plumbing.

### Decided — `bool` gets a wrapper

`bool` is special: it *is* the truthiness primitive, and `IBooleanResolvable.AsBooleanAsync()` always bottoms out at a raw `bool`, so the wrapper can't be turtles-all-the-way. **Decided (Ingi): create `bool.@this`** — same compare/equality/serialize surface as the others, the truthiness contract bottoming out at the raw `bool` it wraps. The reason is consistency: `Compare.Family`'s `bool =>` arm and the `is bool` sites dissolve like the rest. Simpler code, no special-case `if` — the value just flows.

### Decided — `null` gets a singleton wrapper

Reconsidered (Ingi): give null a `null/this.cs` (`null.@this`) too. It hosts null's behavior — truthiness (always false), `null == null` equality, bare `null` serialization — so the scattered `is null` / `_value == null` value-switches dissolve exactly like the other scalars, finishing the thesis for null. Two constraints keep it cheap and correct:
- **Singleton.** There is one null; `null.@this` is a shared instance (`null.@this.Instance`), not a per-value allocation. `Data.Null()` stays the factory and stamps the singleton; `null.@this : item.@this`, so it flows in `Data<item>` slots.
- **It is the null *value*, not the absence of a Data.** A Data whose value is null → `null.@this`. A missing variable / `NotFound` / uninitialised read is a null *`data` reference* (no box at all, `IsInitialized = false`) — a different axis that stays a C# null. Don't let `null.@this` try to represent "no Data."

### Decided — `item` is the apex *and* the un-narrowed type; every value is `: item`

`item` is the apex of the value lattice (≈ C# `object`) **and** the un-narrowed/lazy type a value carries before it's examined. `read file.json, write to %file%` → `%file%` is `Data<item(kind=json)>`, type not yet judged; first touch parses (`{` vs `[`) and narrows `item` → `dict`/`list`. So `item` is **thick** — it carries the lazy-narrow behavior and truthiness; it is *not* a thin marker, and it is *not* pure-abstract (an un-narrowed value sits as `item` holding its serialized form until narrowed; whether that's `item.@this` directly or a concrete un-narrowed form under it is the coder's call). "The apex stores nothing" still holds for *narrowed* values — a `number` is stored as `number`, never as a bare `item`.

**`object` folds into `item`.** The tree carries the un-narrowed tree type under the name `object` (`config.json → (object, json)`); that is the un-migrated name. This branch makes it `item` — `(item, kind=json)` — matching the design ([[plang-value-and-type-model]]: "a value read as json sits as `item` + `kind=json` … narrows `item` → `dict`/`list`"). Two senses of `object` collapse into `item`: CLR `object` in `Data<object>` (the polymorphic slot → `Data<item>`) and the PLang `object` type (→ `item`). **There is no enduring PLang `object` type.**

**What `item` carries vs. what stays opt-in** (corrects the earlier "fold all three onto `item` as abstract members"):
- **Carries: the lazy narrow** (it *is* the un-narrowed type) and **truthiness** (universal; virtual-with-default — un-narrowed / reference-ish items truthy-if-present, concrete types override: empty text / zero / empty dict / null falsy). Keep a **sync** truthiness path so a hot `if %bool%` doesn't take an async hop just for being `IBooleanResolvable` — async is only for I/O truthiness (`path`).
- **Does NOT force ordering.** Ordering is not universal — `list` is orderable, `dict` is equality-only and its `Compare.Order` throws (the model `collections-are-data` shipped, `type-system.md`). So `IOrderableValue` stays **opt-in per type** (the orderable scalars + `list` implement it; `dict`/`bool`/`null`/`Variable`/`Ask` don't). `item` must **not** implement it, or `dict : item` would inherit an order it can't honor.
- **Equality** stays opt-in via `IEquatableValue` (the interface `Compare` already dispatches on); each value type implements its own. `item` may carry a reference-identity default so reference-ish items (`Ask`) need no stub — coder's call on default-vs-pure-opt-in.

Only the genuinely universal pieces live on `item`; ordering and value-equality stay the opt-in interfaces `dict`/`list` already use, and `Compare`'s existing interface-dispatch (`lv is IOrderableValue`/`IEquatableValue`) routes them.

**Every value is `Data<wrapper>`, and every wrapper is `: item`:**
- `Data<int>` → `Data<number>`, `Data<string>` → `Data<text>`, `Data<List<data>>` → `Data<list>`, `Data<object>` → `Data<item>`. Raw CLR (`int`, `string`, `List<data>`) is the wrapper's backing `.Value`, one level down — `list.@this` holds `List<data>` (`_items`) exactly as `number.@this` holds `int` — unwrapped once at the BCL perimeter / conversion leaf. The backing stays `List<data>` (boxes), never `List<item>`.
- **Everything that rides a `Data<T>` slot is `: item`** (Ingi): the scalars, `dict`/`list`, `path`/`image`/`code`, `datetime`/`date`/`time`/`duration`, `bool`/`null`, `Variable`, and `Ask` (and `snapshot` until it's deleted). **No bare-`Data` carve-out** — clean *because* `item` forces no contract, so `Ask : item` costs nothing (it implements none of the three interfaces). `Variable` stays `IRawNameResolvable` for name-binding (orthogonal to `: item`).
- The only non-`item` handler property is `[Code] T` — not a `Data<T>` at all; it's compiled C# behavior injected from `app.Code` (`[Code] IEvaluator Evaluator` on `condition/if`), a separate property kind the PLNG001 gate splits out.

**The constraint + its cascade.** Turn on `data.@this<T> where T : item`; every remaining `Data<rawCLR>` / `Data<object>` slot is a build error — the compiler is the census for the signature swap. **The real cost is the cascade:** ~25 generic `Data<T>`/`Data<U>` infra methods (`Merge`/`Clone`/`Ok`/`Fail`) each need `where T : item` threaded through. The constraint lands as the *final* lock (Stage 7), after every slot is migrated.

**Double-wrap, killed structurally** (Stage-7 acceptance criterion): `data.@this<T> : data.@this`, and `Data` is **not** an `item`, so a `Data<item>` slot cannot nest a `Data`. The strongest single payoff of the branch.

Scope — **folded into this branch** (Ingi): this branch creates `app/type/item/this.cs` (the apex + un-narrowed type; `object` folds in), makes every value type inherit it, swaps the `Data<rawCLR>`/`Data<object>` slots, threads `where T : item` through the generic layer, and locks the constraint as the final step.

## Seam map + dispositions (the leaf-trace, at seam granularity)

Five seams carry scalar behavior today. Each gets the same treatment: the per-type arm moves onto the wrapper; the cross-type/perimeter logic stays.

1. **Construction (where scalars are born raw).** `data/this.cs` `UnwrapJsonElement` (`String → GetString()`, `Number → UnwrapJsonNumber`, `True/False → raw bool`, `Null → Data.Null()`), the STJ-DOM path, `variable.set` parsing, CLI parsing, action results. **Disposition:** these produce the wrapper, not the raw value — `text.@this`/`number.@this`/`datetime.@this`/`duration.@this`/`bool.@this`; JSON `null`/absent produces `Data.Null()` (type `null`), **not** a null-wrapping scalar, so null stays unambiguous. This is the load-bearing seam: do it **first** so every value is wrapped at birth, then sweep consumers against a world where raw scalars no longer appear mid-flight. (Mirrors how `collections-are-data` built native dict/list at the parse seam first.)

   **The `Unwrap*`/`Wrap*` family is the target, not the tool.** A method named `Unwrap…`/`Wrap…` is the canonical tell of the internal round-trip smell ([[plang-value-and-type-model]]). Producing wrappers *at* the parse seam is what lets these retire: `UnwrapJsonElement` becomes parse-to-native (emits `text`/`number`/`bool`/… directly), not parse-to-raw-then-rewrap. **Delete `UnwrapNewtonsoftToken`** — Newtonsoft is not our serializer (no package ref; the shim only sniffs JTokens by namespace string for a dead v1 path). Verify nothing live still feeds JTokens, then remove it. Full elimination of every `Unwrap*`/`Wrap*` may not all land this round (some sit on other seams), but seam 1 is where the bulk goes — and none should be *added*.

2. **Compare** — `data/Compare.cs` (`Order`, `AreEqual`, `AreEqualValues` — already dispatch `lv is IOrderableValue`/`IEquatableValue` → self, else fall to `ScalarComparer`), `data/ScalarComparer.cs` (the raw-scalar type-switch: `Name()`, `IsNumeric`, `IsDateTime`, the `TimeSpan`/`DateTimeOffset`/`DateOnly` arms), `Operator.NormalizeTypes`. **Disposition:** each scalar wrapper **opts into** the interfaces it honors — `IEquatableValue` (all) and `IOrderableValue` (the orderable ones: number/text/datetime/date/time/duration; **not** bool/null) — exactly as `dict`/`list` do today. `item` does **not** implement these (see the contract decision); `Compare`'s existing `lv is IOrderableValue`/`IEquatableValue` dispatch catches each wrapper automatically. As construction stops producing raw scalars, **`ScalarComparer` collapses** — its `Name()` switch and per-type arms delete, leaving only coercion + a thin `IComparable` fallback. The coercion mediator stays, but its **internals are rewritten** to inspect wrapper types (the one blessed type-discrimination site), not raw CLR. *(The plan's older `Family()`/`Orderable`-set wording is superseded — the collections-are-data handoff already landed as `ScalarComparer` + interface dispatch.)*

3. **Truthiness** — already `IBooleanResolvable`. **Disposition:** `text`/`datetime`/`duration`/`bool` implement it (empty text falsy, etc.); `number` already does. The `Data.ToBoolean()` raw-scalar fallbacks (`is string ""`, `is bool`) become unreachable for wrapped values and are kept only for the perimeter.

4. **Serialization** — `data/this.Normalize.cs` + the json writer + per-type `[JsonConverter]`. **Disposition:** each wrapper renders **bare** (`text.@this → "abc"`, `number.@this → 5`, `datetime.@this → ISO`, `bool.@this → true`) on `application/json`, and rides the `application/plang` wire as a self-describing Data — exactly the dict/list pattern (`Normalize` keeps it native, the converter emits the value-only view). Add the wrapper to `Normalize`'s tree-leaf set and give it a converter. **Do not** add a parallel envelope (the "Data is not enveloped" rule).

5. **Conversion** — `type/catalog/Conversion.cs` (value → typed `T`). **Disposition:** unwrap the wrapper to its raw `.Value` at the typed-conversion leaf (like `dict.ToRaw`/`list.ToRaw`), so `→ returns string`/`int`/`DateTime` reconstruct unchanged. One unwrap at the leaf, not at every call site.

## The consumer sweep — body sites (the ~197)

Two sweeps, two mechanisms — don't conflate them:
- **Signatures** (`Data<int>` params, `Task<Data<bool>>` returns): the `where T : item` constraint makes the **compiler** enumerate them — every raw slot is a build error. No manual census; covered by the construction/swap step.
- **Bodies** (the ~197 here): these still **compile** after the swap but go silently wrong — `.Value` is now a wrapper, so `value is string` is just false. The compiler can't see them; **grep + tests are the only backstop.**

Census every `is string` / `(string)value` / `value is int|long|double|decimal|bool` / `is System.DateTimeOffset|TimeSpan|DateOnly|TimeOnly` (and legacy `DateTime`) / `.Value is <scalar>` in production C#, and classify each — this is the bulk of the branch and where the OBP win lands:

- **Behavioral** (`is string str => str.Length`, `.ToUpper()`, `.Contains()`, arithmetic, date math) → **becomes a method on the wrapper.** The if disappears. This is the majority and the point of the branch.
- **Perimeter** (`is string s` right before `JsonSerializer.Serialize(s)`, a `Regex`, a BCL call) → **becomes a single `.Value` unwrap** at the edge. Legitimate, not a smell.
- **Coercion** (comparing/relating two values of possibly-different type) → **routes to the mediator.** Already covered by seam 2.

Disposition rule: if removing the `is` requires asking the value to *do* something, it's behavioral → method. If it's handing the value to non-plang code, it's perimeter → unwrap. If it's reconciling two values, it's the mediator.

## Risks / footguns

- **Transition leakage.** Mid-sweep, a raw scalar slips through a not-yet-converted producer and a consumer that now expects a wrapper NREs (or vice-versa). Mitigation: seam 1 (construction) first, then a wrapper-side **implicit operator** (`text.@this ↔ string`, like `number`'s and `Variable`'s) so a missed perimeter site still compiles and runs — but treat implicit operators as a transition aid, not a license to leave `.Value` reaches un-swept. Tests are the backstop.
- **Implicit-operator double-wrap** — the `Data<object>` footgun in CLAUDE.md ("Action `Run()` returns are typed"). A wrapper with an implicit `object` conversion + `Data<object>` can silently double-box. Keep implicit operators to the *raw backing type* (`text.@this ↔ string`), never to `object`. The `Data<object>`→`Data<item>` decision above kills this structurally where adopted: `Data` is not an `item`, so a `Data<item>` slot can't nest a `Data`.
- **String atomicity.** `foreach %s%` must not char-iterate a `text.@this`. The `IsPlangAssignable`/`IsPlangIterable` carve-out (`data/this.cs`) that already exempts raw `string` must be extended to `text.@this` (it is not `IEnumerable`, so it should be safe — verify).
- **Value-equality of wrappers.** Two `text.@this("a")` must be equal (`HashSet`, list-element dedup). Value-equality is opt-in via `IEquatableValue` (the interface `Compare` dispatches on), overridden per wrapper with value (not reference) semantics — `number` shows the shape. Also wire up `Equals`/`GetHashCode`. **Mid-migration hazard** (coder #3): the implicit `text.@this ↔ string` operator compiles but does *not* hash-equal a raw `string` to a `text.@this` — so a `HashSet`/list built across a not-yet-swept window can miss-match. Bound the construction flip to within the type's stage and add a regression test. (dict *keys* are string-indexed, so the hazard is element/value equality, not keys.)
- **Allocation.** Every scalar boxes into a wrapper. Marginal — values already box into `Data`'s `object?` slot — but measure if a hot list-of-ints path regresses.

## Sequencing within the branch

1. **Census the body sites** — grep the patterns above, classify behavioral / perimeter / coercion. Output the list before editing — it scopes the branch. (Signatures are *not* in this census; the constraint finds those in step 5.)
2. **Create `item.@this`** — the apex + un-narrowed type (`object` folds in); carries **truthiness + the lazy narrow** only. Ordering and value-equality stay opt-in interfaces. Make `number`/`dict`/`list` inherit it (proof: `dict : item` keeps no order).
3. **Build the wrappers** — build out `text` / `datetime` / `duration`; create `bool` / `null` (singleton) / `date` / `time`; all inherit `item.@this`; `Variable : item`.
4. **Construction seam** — materialisation produces wrappers (before the sweep); JSON `null` → `Data.Null()`; start retiring `Unwrap*`/`Wrap*` (delete `UnwrapNewtonsoftToken`).
5. **Swap the `Data<T>` slots + lock the constraint** — every `Data<rawCLR>` handler param + action return → its wrapper; turn on `Data<T> where T : item`; the compiler enumerates the rest; fix until it builds. `Data<object>` → `Data<item>`.
6. **Compare** — relocate scalar arms onto the wrappers; delete `Family`/`Orderable`; rewrite the coercion mediator to inspect wrappers.
7. **Serialization + conversion** — bare render; unwrap at the conversion leaf.
8. **Body sweep** — execute the step-1 census: behavioral → method, perimeter → `.Value`, coercion → mediator.
9. **Delete the dead** — `Family()`, raw-scalar special-cases in `ToBoolean`/compare; the shared-C# scalar comparer collapses to coercion + a thin `IComparable` fallback.

## Done when

- Both suites green.
- `Data<T> where T : item` is in place and **compiles** — no `Data<rawCLR>` slot survives; `Data<object>` is gone (→ `Data<item>`).
- No `is <scalar>` / `(string)value` / `.Value is <scalar>` survives outside the two legal sites (perimeter unwrap, coercion mediator); `ScalarComparer`'s raw type-switch (`Name()`, the per-type arms) has collapsed to coercion + a thin `IComparable` fallback.
- `item.@this`, `bool.@this`, `null.@this`, `date.@this`, `time.@this` exist; every value-wrapper (and `Variable`) inherits `item`.
- A scalar read from a variable is its wrapper, and `→ returns string`/`int`/`DateTime`/`DateTimeOffset` still reconstruct at the conversion leaf.
