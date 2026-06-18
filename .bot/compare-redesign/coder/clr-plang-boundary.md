# The CLR ⇄ plang value boundary — current state, smells, and a future direction

**Audience:** an analyst (human or LLM) reasoning about this design *without* repo
access. Everything needed to reason is inline; file:line refs are anchors, not
prerequisites.

**Status:** descriptive (how it works today) + a proposed future direction Ingi
wants analyzed. No decision committed.

---

## 0. One-paragraph context

PLang is a programming language whose runtime ("Runtime2") is written in C#/.NET.
A PLang *value* (a string a user typed, a number, a list, a parsed JSON document,
a file reference, a date) is modelled at runtime by a **plang value type** — a C#
class in the `app.type.*` namespace. The base of that hierarchy is
`app.type.item.@this` (think of it as plang's `object`). Concrete leaves:
`text`, `number`, `@bool`, `date`/`datetime`/`time`/`duration`, `binary`,
`guid`, `choice`, `path` (and its `file`/`url`/`http` kinds), `list`, `dict`,
`type` (yes — a *type descriptor is itself a value*), and `item` itself as the
"unknown/any" apex.

Around a value sits `app.data.@this` (aliased `Data`): the **carrier** — it holds
the value plus metadata (name, declared type, signature, template flag, error,
success). The guiding design rule of the current redesign:

> **The type instance IS the value.** There is no "value object" separate from
> its type. A `text` instance is the string. `Data` is a courier that carries a
> value; it must not inspect or transform the value it carries (OBP Rule 9 —
> "only leaves touch `Data.Value`").

## 1. The core tension: a value has three shapes today

The same logical value can exist in three forms depending on where in the code
you stand:

1. **Raw CLR** — a .NET `string`, `int`/`long`, `List<object?>`,
   `Dictionary<string,object?>`, `DateTime`, `byte[]`. Produced by .NET BCL calls,
   3rd-party libraries, JSON deserialization, and a lot of internal C# that simply
   hasn't been migrated.
2. **plang value** — an `item.@this` subclass (`text.@this`, `list.@this`, …).
   The intended internal representation.
3. **Carried** — a `Data` wrapping a plang value (`Data{ Value = text.@this }`),
   or generically `Data<text>`.

This trinity is the root of a recurring bug class: double-wrapping
(`Data<object>{ Value = Data<bool> }`), `.Clr` peeling ambiguity, and
narrow-on-read ambiguity. The redesign is steadily collapsing (1) into (2): raw
CLR should exist only at the edges.

## 2. CLR → plang: `Data.Lift` (the inbound boundary)

When raw CLR crosses into the value model, it goes through **one** method:

```
internal static item.@this Lift(object? v, context ctx = null)
```

`Lift` takes `object?` — a value with **no plang self-knowledge** (we don't own
`System.String`/`List<T>`, so there's nowhere on *them* to hang "become a plang
value") — and returns the owning plang value. Its body is a dispatch cascade
(simplified, in order):

```
if (v is null)              return null-value singleton;
if (v is item.@this already) return already;          // (A) already plang → identity
if (v is Data)              throw;                     // bare Data may not be a value (double-wrap guard)

if (v is IEnumerable<Data> dataSeq)        return new list(dataSeq);        // (B)
if (v is IEnumerable<item.@this> itemSeq)  return new list(itemSeq);        // (B') — added this branch
if (v is List<object?> objList)            return new list(objList);        // (C) alias by reference, O(1)
if (v is Dictionary<string,object?> d)     return new dict(d);              // (C)
if (v is IDictionary || (v is IList && not byte[]))                         // (D) JSON round-trip
        return json.Parse(JsonSerializer.SerializeToElement(v));

var (family, _) = convert.OwnerOf(v.GetType());                            // (E) scalar registry
if (family owns a value type) return family.Convert(v);
return item;                                                               // unknown → "any" apex
```

Key observations for analysis:

- **Arm (A) is the proof that plang values own themselves.** A plang value walks
  in and is handed straight back. *Every arm below (A) handles foreign CLR only.*
  The type-switch is therefore intrinsic to a **boundary**, not misplaced
  behavior — there is genuinely no method on a `System.Collections.List<int>` to
  dispatch to.
- **Arms (B/B'/C) preserve native plang structure** (sequences of `Data` or of
  `item.@this`, and the two canonical object-slot containers aliased by reference
  in O(1)). **Arm (D) is the lossy fallback**: a foreign container that can't be
  aliased (e.g. `List<int>`, a `List<type.@this>` before B' existed) gets
  serialized to JSON and reparsed — which *mangles* any element that was already a
  plang value (a `type` descriptor reparses as a `dict`). Arm (B') was added
  specifically to stop a `list<type>` (and goal-result lists, etc.) degrading
  through (D).
- **Arm (E) is a real registry.** Scalar families *self-register* the CLR types
  they own via a static `OwnedClrTypes` declaration; `convert.OwnerOf(clrType)`
  resolves the owner, and the family's `Convert` hook constructs the value. A new
  scalar type joins by declaring its CLR mates on itself — **never** by editing
  `Lift`. This is the clean shape.
- **The asymmetry / the wart:** the container arms (B/C/D) are **hand-coded
  `if`s**, not registry entries — because they dispatch on **interface**
  (`is IList`, `is IEnumerable<item.@this>`), not concrete type, and the
  `OwnerOf` table is keyed by concrete type. So containers can't sit in the same
  registry the scalars use.

## 3. plang → CLR: the `.Clr` exit door

The reverse crossing (a plang value handed to a .NET API that wants a `string` /
`List<int>` / etc.) goes through:

```
T item.@this.Clr<T>()           // and the virtual object? Clr(System.Type target)
```

- A scalar (`text`, `number`, …) delegates to `ClrConvert(backing, target)`:
  identity if already assignable, `IConvertible.ChangeType` for primitives, else
  the `convert` catalog's `TryConvert`. Throws `InvalidCastException` on a real
  mismatch (honest failure, not silent).
- `list.Clr(target)`: if the backing never diverged (all-raw) it hands the
  backing **straight back** (same reference, O(1)) — this is the
  alias-by-reference contract from arm (C). If it diverged (a row was elevated to
  a `Data`/wrapper), it peels each slot to raw first.

So the model already has a clean **two-door** boundary: `Lift` inbound, `.Clr`
outbound. The problem is not the doors — it's how *often* they're crossed
internally because internal C# still traffics in raw CLR.

## 4. The smell that motivates the direction (OBP smell #5)

> "Producer hands back raw; consumers transform identically."

Concrete instance found this week: `type.@this` (a plang type) exposes its
identity chain as

```
public IReadOnlyList<type.@this> List { get { var c = new List<type.@this>{this}; c.AddRange(_priors); return c; } }
```

i.e. a **plang type hands out a raw `System.Collections.List<>` of itself.** Its
real consumers are *internal C# hot paths* — `Is()` (`_priors.Any(...)`),
`Facet()` (`List.FirstOrDefault(...)`), `Accumulate()` (`_priors.Add(...)`) —
which genuinely want C# semantics and never cross into plang. But the navigation
layer *also* exposes `List` to PLang as `%x!type.list%`, and at that moment the
raw C# list has to be re-narrowed by `Lift` (arm B') back into a plang `list`.

So a value that is *conceptually* plang from birth takes a detour out to raw CLR
(because the storage and the internal accessor are C#) and back. Multiply this
across the codebase: most internal producers hand raw CLR, and `Lift`/`.Clr` fire
constantly mid-engine instead of only at the true perimeter.

## 5. Existing enforcement (the direction is already in motion)

The codebase already has analyzers ratcheting toward "plang types internally":

- **PLNG002 (error):** no `System.IO.*` in production C# — filesystem access must
  go through the `path` value type's verbs (which gate on an auth model). The
  only place raw `string` paths are allowed is the perimeter.
- **PLNG003 (warning):** a public member returning a raw CLR type
  (`string`/`int`/`Dictionary<…>`) is flagged — "return the PLang equivalent
  (text/number/@bool/list/...) or make it internal."

These two analyzers are, in effect, the seed of the full direction below applied
to two slices (IO, and public return types).

## 6. The proposed future direction (to be analyzed)

**Make plang value types the internal lingua franca.** Every property on every
plang class, every method parameter and return, is a plang value type. Raw CLR
(`string`, `int`, `List<T>`, `DateTime`, …) appears **only** at the boundary with
.NET BCL / 3rd-party APIs. Consequences:

- Each plang scalar type implements its CLR-owned operations as methods on itself:
  `text.Replace/Substring/IndexOf/Split/Trim/…`, `number` arithmetic/compare/
  format, `date`/`duration` arithmetic, `list.Map/Filter/Sort/Slice/…`. Each is a
  thin "convert→call BCL→wrap" delegate, bounded by what PLang code actually uses
  (~20–30 methods cover the bulk; the long tail stays reachable via `.Clr`).
- `Lift` shrinks to a true perimeter dispatcher — fired only right after a BCL/
  3rd-party call returns raw CLR, never mid-engine.
- OBP smell #5 vanishes structurally: producers hand plang, consumers take plang,
  no raw-to-transform seam except the marked perimeter.

### Claimed benefits
- One internal representation → eliminates the three-shape trinity (§1) and its
  bug class (double-wrap, peel ambiguity, narrow ambiguity).
- Behavior genuinely lives on the value (OBP fulfilled, not aspired).
- Provenance (type, signature, template, name) rides with every value instead of
  being re-derived by walks.

### Open questions / risks (the actual analysis targets)

1. **Scalar performance — the make-or-break.** `string`/`int`/`Span<T>` are
   JIT-intrinsic, zero-alloc, struct-packed. Wrapping every scalar in an
   `item.@this` *class* adds an allocation + virtual dispatch per value and defeats
   those optimizations. The hot paths are precisely scalar-soup: the parser, the
   serializer, and the navigation engine itself. A million-element numeric loop
   would box a million objects.
   - Sub-question: can scalars be made cheap — `readonly struct`, interning of
     common values, or CLR fast-paths that bypass wrapping in tight loops —
     **without** losing the polymorphism the value model relies on (`item.@this`
     is a virtual class hierarchy today; structs lose virtual dispatch)?
   - This likely decides scope: if scalars can't be cheap, the right boundary may
     be "collections + domain types (`path`/`date`/`type`) fully plang; scalars
     plang-at-rest but with CLR fast-paths in hot loops."

2. **BCL reimplementation surface.** Bounded and mechanical, but must match (or
   *deliberately* differ from, and document) .NET semantics: culture, overflow,
   null handling, comparison/ordering. Correctness surface is non-trivial even if
   each method is small.

3. **Partial-migration hazard.** A half-migrated codebase has *more* conversion
   churn than either pure state (raw↔plang ping-pong at every internal call).
   Migration must be committed staged sweeps with PLNG003 ratcheted file-by-file
   and perimeter files explicitly marked — never a long-lived 50% state.

4. **Ergonomics / operator design.** Internal C# loses `a + b`, string
   interpolation, LINQ over `List<T>`. Implicit operators (`text`↔`string`) would
   restore readability but *reintroduce hidden `Lift` calls* and the double-wrap
   footgun the codebase already fights. The operator surface needs careful design.

5. **Async creep.** Some plang values have async "doors" (e.g. `path` truthiness
   does a filesystem stat / HTTP HEAD), which already forced the condition-
   evaluation pipeline async. Pushing plang types everywhere risks async infecting
   code paths that are synchronous today.

6. **Generics & equality.** `list<text>`/`dict` keyed by plang values need plang
   equality/hashing/ordering to be correct and fast. Partly exists; more surface
   to get right.

### Rough sizing
A major version's worth — comparable to or larger than the entire current
redesign. Dimensions: hundreds of `app/**` signature changes (PLNG003 already
flags the shape) + the core BCL surface + an enforcement analyzer + perimeter
marking + a **scalar performance workstream** that may force `item.@this` from a
class toward a struct/hybrid representation.

### Suggested first step
Do **not** start with the migration. Start with a **scalar performance spike**:
prototype `text`/`number` as cheap values and benchmark the parser, serializer,
and navigation hot paths. Cheap scalars → commit to the full direction. Otherwise
scope to collections + domain types and leave scalars with CLR fast-paths.

---

## Appendix — anchor map (for someone with repo access)

- `PLang/app/data/this.cs` — `Lift` (inbound boundary, ~line 194), the dispatch
  arms, the double-wrap guard.
- `PLang/app/type/item/this.cs` — `item.@this` base; `Clr<T>()` / `Clr(target)` /
  `ClrConvert` (outbound boundary).
- `PLang/app/type/list/this.cs`, `.../dict/this.cs` — native container model
  (raw-slot, alias-by-reference, `_hasWrapped`, `.Clr` same-ref fast path).
- `PLang/app/type/convert/*` — `OwnerOf` / `OwnedClrTypes` scalar registry
  (the clean self-registration pattern).
- `PLang/app/type/this.cs` — `type.@this`; `List`/`_priors`/`Facet`/`Is`/
  `Accumulate` (the smell-#5 example).
- Analyzers PLNG002 (System.IO ban) / PLNG003 (public-member raw-CLR return) —
  the in-motion enforcement of the direction.
