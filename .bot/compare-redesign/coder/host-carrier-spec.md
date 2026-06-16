# Foreign-object carrier spec — a C# object is an `item`

**Status:** supersedes the v15 "remove `clr` → hard error" decision and the
`external` deferral. Settled with Ingi 2026-06-16 after his gut flagged
"itemizing the runtime objects" as over-engineering, and refined the same day to
"a C# object is just an `item`". **Architect-reviewed** (`host-carrier-review.md`,
HEAD `805699509`); this rev folds in the four findings — navigation reframed as a
repair (already broken on HEAD), inventory 7→10, cycle guard, write deferred. The
`kind` rule is settled on **declared `[PlangType]` names** (both registry- and
namespace-tail derivation were shown to fail — see "What goes in `kind`").

## The decision in one line

A C# object PLang can't narrow is just an **`item`** — the apex of the value
lattice (≈ C# `object`), the most un-narrowed value there is. The runtime handles
(`%!app%`, `%!callStack%`, `%!serializers%`, `%!channels%`, `%!variables%`,
`%!context%`, `%!trace%`, `%!test%`) are such objects: they report **`type=item`**
(with `kind` naming the C# type) and ride a **single closed carrier** that
reflect-reads, reflect-writes-where-a-setter-exists, and reflect-serializes.
There is **no `host`/`external`/`clr` family** in a plang dev's vocabulary — the
type is `item`. The carrier class (today named `clr`) is invisible plumbing.

## Why one carrier reporting `item`, not a dedicated item type per class

The over-engineering was making each runtime class its **own narrowed item type** —
a per-class subclass carrying the full value-lattice apparatus (truthiness,
`ICreate` "construct yourself from a value", a leaf/wire form, immutable-rebind).
None of that means anything for an `app` or a `CallStack`, and you'd write
dozens of them.

A foreign object isn't a *narrowed* value — it's the **un-narrowed apex**, which
`item` already is (its own definition: *"the apex ≈ C# `object`, the un-narrowed
type tag a value carries before it is examined"*). So one carrier holds any
foreign object and reports `item`. It doesn't pretend to be a `number`-like value
type; it's honestly "the thing we haven't narrowed."

Dedicated item types stay for genuine PLang values (`text`, `number`, `dict`, …)
and the domain entities that really are values (`goal`, `step`, `error` — already
items; leave them).

## What the carrier is

A small, **closed** type holding one live host object and owning three
operations over it by reflection. "Closed" = no consumer ever sees the carried
object except through the carrier's own door or the explicit `.Clr<T>()` exit.

### The three operations

1. **navigate (read)** — reflect-get the named property; re-wrap the result in a
   `Data`. A nested host object becomes another carrier; a `string`/`int`/etc.
   becomes a real item (it Lifts on the way back). Deep paths
   (`%!callStack.Current.Caller.Tags.owner%`) just recurse the carrier.

2. **write** *(DEFERRED — a later, gated step; see "Decision" below)* — reflect-set
   **iff the property has a setter**. No setter → the write declines (returns
   false; the caller surfaces the failure). Read-only vs writable is **not a map
   we author** — it is inherited from the C# shape. `CallStack.Current` is
   `{ get; }`, so it is read-only, full stop; `App.Serializer` is writable iff it
   is `{ get; set; }`.

3. **serialize** — the carrier's wire form = reflect its carried object's
   `[Out]` properties into the writer (the property bag). This **is** the
   snapshot: `write %!app% to %snapshot%` walks the app's `[Out]` graph.
   Replaces today's "clr has no wire form → throws".

### Behavior trace

```
- read %!app.callStack.Current.Depth%      / navigate("Current")→carrier; navigate("Depth")→number
- set  %!app.callStack.Current.Depth% = 5  / write: Depth has no setter → declines (correct)
- set  %!app.serializer% = "json"          / write: setter exists → reflect-set the LIVE app
- write %!app% to %snapshot%               / Write(IWriter): reflect [Out] graph → property bag
```

Note: writes **mutate the live app in place** — you are configuring the real
running system, not a clone. (This is the deliberate divergence from the old
`external` clone-on-write note, which only ever applied to genuinely foreign
*data* you are handed and choose to treat as an immutable value — a separate,
later concern, not the runtime handles.)

### Reference semantics

`set %x% = %!app%` binds `%x%` to the **same live app** (the carrier holds a
reference to the singleton). `%x.callstack%` reads the same running app as
`%!app.callstack%` — identical current state. To freeze a point-in-time copy you
**serialize** (the snapshot), you do not bind.

## Type identity — `type=item`, the C# type in `kind`

A foreign value reports the **apex** in `type` and its C# identity in `kind`:

```
%n%                              → type=number, kind=int
%!app%                           → type=item,   kind=app
%!app.callstack%                 → type=item,   kind=callstack
%!app.callstack.current.depth%   → type=number, kind=int     ← leaf has a real plang type
```

`type=item` is honest: it means "the apex; not narrowed to anything more
specific" — exactly what a C# object PLang doesn't own *is*. `kind` carries the
specific identity (the same shape as `number`/`int`: type = the value's lattice
position, kind = the specialization).

### What goes in `kind` — a cross-runtime mapping key

`kind` is the C# type's identity, and it doubles as a **mapping key** for a
non-.NET PLang runtime. Two cases, two granularities:

- **PLang runtime's own objects** (`app`, `callstack`, `variable`, …) — these are
  PLang *vocabulary*; another-language runtime has its own equivalents and maps
  by the **canonical short name**. `kind=app`, `kind=callstack`.
- **External / anybody's custom type** — no shared contract, so the honest
  identity is the **`FullName`** (`MyCompany.Models.Customer`), globally unique.
  *Not* `AssemblyQualifiedName` — that pins assembly + version and would churn on
  every bump; `FullName` is the version-independent mapping key.

**Discriminator: a declared `[PlangType]` name — NOT derivation.** (Settled with
Ingi after both the registry *and* namespace-tail derivations were shown to fail.)
The canonical name cannot be *derived* from the CLR type:

| handle | CLR type | namespace-tail gives | `type.Name` gives |
|--------|----------|----------------------|-------------------|
| `!app` | `app.@this` | `app` ✓ | `@this` |
| `!callStack` | `app.callstack.@this` | `callstack` ✓ | `@this` |
| `!variables` | `app.variable.list.@this` | **`list`** ✗ | `@this` |
| `!channels` | `app.channel.list.@this` | **`list`** ✗ (collides w/ variables) | `@this` |
| `!trace` | `app.error.trail.@this` | **`trail`** ✗ (≠ trace) | `@this` |
| `!test` | `app.tester.@this` | **`tester`** ✗ (≠ test) | `@this` |

The collection handles are `X.list.@this` → both collapse to `list`; `trace`/`test`
don't even match their namespace. No namespace rule recovers the right name, and
`type.Name` is always `"@this"`. So the name must be **declared**, via the
existing `PlangTypeAttribute` the registry already reads:

```
[PlangType("app")]       on app.@this
[PlangType("variable")]  on app.variable.list.@this
[PlangType("channel")]   on app.channel.list.@this
[PlangType("trace")]     on app.error.trail.@this
…one per concept handle (~8)
```

The rule, clean and unambiguous:

- carried type has a `[PlangType]` / registry name → `kind` = that declared name
  (canonical short — and exactly the cross-runtime vocabulary another-language
  runtime maps by);
- no declaration (any third-party POCO) → `kind` = `clrType.FullName`.

(This refines the architect's Fix 1: the **registry was the right discriminator**;
it was just *empty*. The fix is to populate it on the concept types, not to derive
from the namespace — derivation is what breaks, per the table above.)

The **leaf rule** (see the flow) keeps this safe: the carrier only ever holds the
*structural* foreign objects. The instant navigation reaches a value a PLang
family owns (an `int`, a `string`), it peels off into that real item — so
`%!app.callstack.current.depth%` is a `number`, never an opaque item, never a
raw `int`.

## How a C# object becomes an `item` (the Lift flow)

`Lift` (`data/this.cs:194`) is the one chokepoint every slot write passes
through. A C# object becomes a carrier (reporting `item`) only by **falling
through every "does a PLang family own this?" gate** to the fallback:

```
  A C# object needs to enter a value slot
  (set %x% = …, navigation reflect-get, an action return,
   or a DynamicData factory like  () => app )
                │
                ▼
        ┌───────────────┐
        │  Lift(value)  │   the ONE chokepoint — every slot write goes through it
        └───────┬───────┘
                ▼
   is it null? ───────────────────────────── yes ─▶ null citizen (null.@this)
                │ no
   is it ALREADY an item.@this? ───────────── yes ─▶ return as-is (text/number/…/clr)
                │ no
   is it a bare Data? ──────────────────────── yes ─▶ THROW (double-wrap bug)
                │ no
   is it a sequence/dict/list of values? ──── yes ─▶ native list.@this / dict.@this
                │ no
   does a TYPE FAMILY own this CLR type? ──── yes ─▶ that family's item
   (int→number(kind=int), string→text,                int 3 → number
    bool→bool, DateTime→datetime, byte[]→…)
                │ no
   is it a CLR enum? ───────────────────────── yes ─▶ choice<TEnum>
                │ no
                ▼
   ╔══════════════════════════════════════════════╗
   ║  FALLBACK: nothing in PLang owns this object  ║──▶ the carrier, reporting `item`
   ║         new clr(value)                        ║    type = item   (the apex)
   ║   (app, CallStack, a 3rd-party POCO)   ║    kind = registry short name, else FullName
   ╚══════════════════════════════════════════════╝
```

The concrete `%!app%` path, and how a leaf peels off one gate earlier:

```
%!app%   = DynamicData, factory = () => app   (the live app singleton)
   │ read → computed.Compute() → Lift(app)
   ▼  app: not null · not item · not Data · not a collection ·
   │       no family owns app.@this · not an enum
   new clr(app)  →  carrier { Value = live app }   type=item kind=app

%!app%            → Lift(app)    → item (no family owns it)
   .callstack     → Lift(CallStack) → item (no family owns it)
   .current.depth → Lift(int 3)     → number (the int family owns it) ✔
```

So the carrier is purely the **"no PLang family claimed it"** terminal of `Lift`:
structural host objects stop there; every scalar with a real type peels off into
its own item one gate earlier.

## What changes in `clr` (the fix)

Today `clr` is **half-built** — its own comment admits the door was left open:
*"Tightening the door to answer the carrier itself is deferred — too many
raw-shape consumers remain."* Two concrete defects fall out of that:

- **Defect 1 — open box.** `Peek()` returns the raw carried object, so the
  code reaches *past* the carrier and branches on `is clr` / `.Value is X`
  (OBP smell #7). The leak is the half-migration, not the concept.
- **Defect 2 — no wire form.** `Write(IWriter)` throws, which blocks every
  snapshot.

The fix:

1. **Own navigate** — add the carrier's reflect-get + re-wrap (move the host
   reflection out of the generic `Object` navigator and into the carrier).
2. **Own write** *(DEFERRED — later step, gated)* — reflect-set-if-setter,
   decline otherwise. Does **not** ship in the first landing; needs the actor
   permission gate first (see "Decision — reflect-write is deferred").
3. **Own serialize** — `Write(IWriter)` reflects `[Out]` props instead of
   throwing. **Must carry a cycle/identity guard** — the `[Out]` graph is deeply
   cyclic (`app → CallStack → Action → Step → Goal → app`); `[Out]` narrows but
   does not break cycles. Use a reference-equality visited set, the same guard
   `Data.Normalize` already runs.
4. **Close the box** — `Peek()` returns the carrier itself; the only raw-object
   door is `.Clr<T>()` (leaf actions only). Then nothing in any relay layer can
   branch on the carried value — the `is clr` smell cannot recur.
5. **Delete the courier-label cruft** — `_declared` / `Labeled` /
   `_declaredStrict` (schema-layer transitional state the comments already mark
   as dying).
6. **Declare `[PlangType]` on the concept handle types** (~8: `app`, `context`,
   `variable`, `channel`, `callstack`, `trace`, `test`, `serializers`). This is
   the canonical-name source AND the discriminator. Required because the name
   can't be derived (see "What goes in `kind`").
7. **Fix `Mint()` to stamp `item` + kind** — today it puts the carried type's
   name in the *type name*; instead set `type` = `item` (the apex) and
   `kind` = the type's registered `[PlangType]` name when it has one, else
   `clrType.FullName`. Mirrors `number` stamping its precision as `kind`.
   See "Type identity" above.

### `Peek` / `Value` vs `Mint` — three questions, don't conflate

`Mint()` is **not** called by `Peek`/`Value` (verified: its callers are `.Type` /
`.Kind`, comparison, navigation type-checks, and error messages). The three are
independent and answer different questions — true for every item, not special to
`clr`:

| method   | question                              | returns                                   |
|----------|---------------------------------------|-------------------------------------------|
| `Peek()` | "what value is in memory now?" (sync) | **the item itself (self)**                |
| `Value()`| "give me the ready value" (async)     | the item (or a narrowed item)             |
| `Mint()` | "what is my *type*?"                  | a **separate** `type.@this` `{Name, Kind}`|

`Mint()` builds a `type.@this` **descriptor** — it does **not** mint a new `clr`.
The `clr` value is constructed once, by `new clr(app)`; reading it (`Peek`/`Value`)
hands back that same instance (self). Minting is the orthogonal "describe my type"
step.

**The whole fix, in one line:** `clr` is today the *only* item whose `Peek()`
returns its raw `_value` instead of `self` — closing the box is simply removing
that divergence so `clr` behaves like every other item (`Peek() => this`), with
`.Clr<T>()` as the single door to the raw object.

## What keeps it from re-rotting

The discipline already on the books: **OBP Rule #9 — only leaves touch
`.Value`.** Today it is violated because the box is open. Once the carrier owns
navigate/write/serialize, no courier *needs* to look inside, so the rule
actually holds. A regression is then a single grep: `\.Value (is|as|switch)`
outside leaf files.

## Naming

There is **no user-visible foreign type name to bikeshed** — the type is `item`.
The earlier `host`/`external`/`clr` debate is moot: that was a *family* name, and
there is no family. The C# *carrier class* (today `clr`) is pure plumbing a plang
dev never sees, so rename it freely whenever (or leave it). The only naming that
reaches a user is `kind` — and that's mechanical: registry short name, else
`FullName`.

## Consumer inventory (scan 2026-06-16) — sizing the close-the-box work

**The decisive finding: not one production reach-in reads a live foreign
object.** Every `is clr { Value: … }` branch reads *parked data* (a nested
`Data`, a raw `JsonElement`, a raw container). The genuine host use — the
`%!...%` runtime handles — branches on `is clr` **nowhere**; it goes through the
generic reflection navigator (`variable/navigator/Object.cs`).

**Correction (architect, verified):** that navigator reflects over
`await data.Value()`, **not** `Peek()` — and a carrier's `Value()` returns the
carrier *itself* (it doesn't override the door), so reflection runs on the
wrapper (`Value`/`Context`) and never reaches the host. So **host navigation is
already broken on HEAD** (`ContextVar_AppProperty_AccessibleViaDotNotation` fails:
`!app.Name` → `"!app"`; regressed by the door-resolution commit `c3910993a` that
flipped `data.Value` → `await data.Value()`). This flips the framing below:
carrier-owns-navigate is a **repair**, not a way to preserve a working path.

### A. Construction sites — KEEP (carrier is correct here)
- `data/this.cs:252` — Lift fallback `new clr(v)` (returns the fixed carrier)
- `data/this.cs:548` — `SetValueDirect` `new clr(value)`

### B. Open-box reach-ins (10) — all read PARKED DATA, not host objects
(Architect re-scan: was 7, three were missed — `http/code/Default.cs` was wholly
unlisted, plus two more.) Group by carried shape; each shape should land as a
real item at Lift, after which the reach-in is dead code and gets deleted:
- **nested `Data`** (the Data-in-Data / SetValueDirect courier debt):
  - `data/this.Navigation.cs:291` — `clr { Value: @this dataVal }`
  - `llm/code/OpenAi.cs:951` — `clr { Value: data.@this d }`
- **raw `JsonElement`** (should be a dict/parsed item):
  - `llm/code/OpenAi.cs:1013` — `clr { Value: JsonElement }`
  - `test/discover.cs:294` — `clr { Value: JsonElement }` (goal-name read)
- **raw container** (Lift already narrows these to native dict/list, so likely
  near-dead already):
  - `data/this.cs:500-503` — `case clr c when c.Value is IDictionary/IList` (StampedForm)
  - `llm/query.cs:33` — `clr { Value: IList }` (Messages validation)
  - `llm/code/OpenAi.cs:1032` — `clr { Value: Dictionary<string,object?> }` ← was missed
  - `http/code/Default.cs:951` — `clr { Value: Dictionary or JsonElement }` ← whole file was missed
  - `test/discover.cs:299` — `clr { Value: IDictionary }` ← was missed
- **raw `string`**:
  - `data/this.cs:491-494` — `case clr ct when ct.Value is string` (StampedForm template scan)

### C. Courier-label cruft (5) — DELETE with `_declared`/`Labeled`
All in `type/this.cs` `Judge`: `:451, :452, :464, :482, :483`
(`carrier.Labeled(...)` / `new clr(value, Name, Kind, Strict)`).

### D. Tests
- `DataTests.cs:864, :868` — cast-and-read `((item.clr)…).Value` (Data-in-Data).
- Three files carry `[Skip]` reasons that assumed clr *deletion*
  (`NormalizeTreeShapeTests`, `NormalizeFilterTests`, `DataTests`) — revisit:
  the "delegate parks in clr and the carrier leaks" skip is *fixed* by closing
  the box, not by deleting clr.

### What this means for sequencing
Close-the-box ≈ **3 data-shape families that should never ride the carrier**
(nested `Data`, `JsonElement`, raw containers) → make each land as a real item
at Lift → their reach-ins die → delete them. Plus delete the courier-label set
(C). After that, the carrier holds *only* genuine host objects and `Peek()` can
return self.

**The one navigator change — a repair, not an open call.** There are *no
per-call-site* changes: nothing branches on `is clr` to navigate runtime handles.
And — per the correction above — host navigation is **already broken** on HEAD,
because the generic `Object` navigator reflects over `Value()` (which returns the
carrier wrapper), not the host. So **carrier-owns-navigate is the fix:** put the
host-unwrap on the one type that wraps — `clr` overrides the navigation hook to
reflect-get/-set over its `_value`; the generic `Object` navigator keeps serving
items that *are* their own object (`error`, domain values, where the item IS the
object and reflection over it is correct). The earlier "option 2 (navigator uses
the exit door)" is dropped: it only looked competitive under the false premise
that navigation works today.

## Decision — reflect-write is deferred (Ingi, 2026-06-16)

**Ship read + serialize now; defer reflect-write and its authorization gate to a
separate later step.** Navigate-**read** is already gated for untrusted input
(`skipInfrastructure` keeps `%!app.AbsolutePath%` literal — `SecurityFixTests`),
so read is safe to land. Reflect-**write** mutating the live singleton
(`set %!app.serializer% = "json"`) is a new capability that PLang must route
through the actor permission model like every other capability — so it does not
ship until that gate is designed. The first landing is the **repair**
(navigate-read) + **snapshot** (serialize); write comes later, gated.

## Out of scope (explicitly deferred, unchanged)

- Clone-on-write value semantics for handed-in foreign *data* (the original
  `external` story). The runtime handles do **not** want this — they mutate in
  place. Revisit only if/when a real "treat this host POCO as an immutable
  value" need appears.

## Migration impact vs v15 plan

- **Dropped from v15:** "flip Lift fallback to a hard error", "delete `clr`",
  and the whole bucket-1 "runtime handles → items" conversion. The Lift fallback
  **stays** and returns the (fixed) carrier.
- **Kept from v15:** buckets 2 & 3 already done (loop result → dict,
  builder.warning → dict, operator test sites → string). Those were genuine
  plain-data values wrongly parked in `clr`; making them dicts was correct
  regardless.
- **Still true:** every value slot holds an `item` — and the carrier *is* an
  `item` (the apex, `type=item`), so the invariant holds without exception. A raw
  C# object never rides a slot bare; it rides the carrier. No producer parks plain
  *data* in the carrier — plain data has a real type (`dict`/`list`/`text`/…); the
  carrier is reserved for live foreign objects with no plang type of their own.
