# Design note: dissolving `item.clr` — every value is a real plang type

**Status:** settled with Ingi this session (2026-06-14), for architect review.
Sibling to `deserialize-flow-design.md`. No code yet — this is the record before
any of it becomes work.

> **DECISION 2026-06-16 (supersedes the role-by-role disposition below where they
> conflict).** Discussed with Ingi. Conclusions:
>
> 1. **`clr` the class is removed now.** Every value in today's runtime is, or
>    should be, a real `item.@this`. A non-item reaching the Lift value slot
>    becomes a **loud producer error** — not a silent fallback.
> 2. **The foreign-object carrier comes back later as `external`, not `clr`.**
>    The name `clr` hard-codes ".NET" into PLang's runtime-independent type
>    vocabulary (a Rust runtime has no CLR). `external` = "a value whose type
>    lives outside PLang's vocabulary." Deferred until a real host-object need
>    appears — see `Documentation/v0.2/todos.md` 2026-06-16.
> 3. **`external` behaves like every PLang value: immutable + rebind, via
>    clone-on-write.** This REVERSES the "reflect-into-live-object — rejected"
>    note in Role 1 below. The rejection was of the *dual-representation* case
>    (a live object AND a drifting dict view). With a single live object and
>    **clone-on-write** there is no second representation and nothing drifts:
>    read `%x.y%` = reflection get; write `%x.y% = 1` = clone (stays the real
>    host type), reflection-set on the clone, rebind. Nested set path-copies.
>    This beats POCO→dict (which loses the type and forces an expensive
>    clr→dict→reserialize round-trip on host-action interop) and beats
>    mutate-in-place (which would give foreign objects reference semantics while
>    PLang values are immutable — an unacceptable behavioral split).
> 4. **The courier/declared-label machinery (`_declared`, `_declaredKind`,
>    compress/signature labels) dies with `clr` and does NOT return on
>    `external`** — `external` derives identity from the host type.
> 5. **Invariant: no code branches on `is clr`/`is external`.** Everyone reaches
>    values through the uniform door (`Peek()`/`Clr<T>()`/navigate). The leaf
>    needs a concrete name only so the lattice has a bottom rung.
>
> Migration buckets (from the live probe, 2026-06-16) to land BEFORE the class is
> deleted — each as its own item, **shown to Ingi for validation before
> converting**:
> - Engine handles surfaced as `%!...%` context variables (`app`/Engine,
>   CallStack, Channels, Variables, Serializers, actor.context) — they reach Lift
>   through the `computed` factories (`item/computed.cs` `Compute()` lifts the
>   factory result). Each becomes an item (or the factory returns one). The bulk.
> - Plain-data result records → trivial items: `loop` `{itemCount, completed}`,
>   `builder.warning` `{Action, Message}`.
> - The `condition.Operator`-in-a-value smell → fix the producer (comparison error
>   path) so a behavior object never lands in a Data.
> - Genuinely foreign / test-only (anonymous types, `System.Object`/`Uri`,
>   `RuntimeAssembly`, test POCOs) → those tests get adjusted (real items/dicts)
>   or retired with a pointer to the `external` todo, since `external` is deferred.
>
> Already done this session: the two dead `clr` read-couriers in `Wire.ReadBody`
> (declared-typeRef nested-Data arm + nameDiffers declared-build arm) removed —
> verified dead by mutation probe, zero regressions.

## The decision (one line)

`app.type.item.clr` is deleted. Every value in a `Data` slot is a real plang
type carrying its own `{name, kind}`; there is no opaque CLR carrier. The CLR
primitive lives **private inside** the typed wrapper and leaves only through
`Clr<T>()` at a real .NET edge.

## The model it lands on

A `Data` has exactly one value field — `Data._type : item.@this` (the "value
slot"). It is **always** a typed item, never bare CLR.

```
Data._type = number.@this   { private object _value = (object)42L }   ← CLR hidden inside
Data._type = text.@this     { private string _value = "hi"        }
Data._type = dict.@this     { _keys + _map (raw-or-Data slots)     }   ← own backing, no _value
Data._type = list.@this     { List<object?> _items                }
```

- `Data.Value()` is the async **door** (a method) → returns an `item.@this`,
  resolving/parsing if needed. Not a CLR pointer.
- `Data.Peek()` → `_type` (sync, what's there now).
- `instance.Clr<T>()` → the one door OUT to CLR, used only at .NET edges. For a
  container it *builds* the raw `List`/`Dictionary` on demand from the backing;
  there is no universal `_value` slot.

## Why `clr` existed — five overloaded roles (a junk drawer)

`clr` was constructed at ten sites doing five unrelated jobs. "Avoid the
exception" only works once each job goes to its own home.

| # | Role | Construction sites | Disposition | Status |
|---|------|--------------------|-------------|--------|
| 1 | Unowned-POCO carrier | `data/this.cs:243` | → `dict` (reflect on entry) | **safe — strands nothing** |
| 2 | Declared-type label | `Wire.cs:269,293`; `type/this.cs:420,432,451` | → narrow-at-read + stored kind | **the one real work item** |
| 3 | Compress/encrypt envelope | `this.Transport.cs:92` | → `@schema` owning types (`archive`/`encryption`) | already planned |
| 4 | Courier passthrough | `this.cs:518` (`SetValueDirect`) | → delete (debt) | flagged "do not add callers" |
| 5 | Parse-failed fallback | `this.cs:227` | → throw | throw already drafted in-place |

## Role 1 — POCO → dict (the core, empirically safe)

**Decision:** a plain-data C# object becomes a native `dict` the instant it is a
value. One representation, one source of truth.

**Why not keep the live object:** the drift argument. If a `Data` held a live
`User` *and* exposed a dict view, then —

```
set %user.age% = 29     // mutate which one? the dict drifts from the C# object,
                        // or we reflect INTO the live object (mutation-by-reflection — rejected)
```

— two representations that desync on mutation (OBP smell #6). Holding both is
the bug. With dict-as-truth, `set %user.age% = 29` just mutates a dict entry.

**Getting the C# object back still works** — as a *reconstruction at the edge*,
not a held reference:

```
%user%  →  dict { name, age:29 }            // single truth
dict.Clr<User>()  →  fresh User from data   // dict→object deserialize, at the .NET boundary
```

This is the harmless direction of reflection (reading *from* data, the same as
`Clr<int>()`), not the rejected one (mutating a held object). Price: a fresh
instance per `Clr<User>()` (no reference identity) — accepted, it's the cost of
"no held object." A POCO that *can't* round-trip through a dict (private state,
required-arg ctor, a handle/closure) **must not be a plang value** — it's an
engine-internal handle kept in a C# field. The dict-only rule enforces that
boundary; that's a feature.

**Strand check (empirical, this session).** Every consumer that pulls a concrete
C# object back out of a value goes through a **first-class plang type**, never
`clr`:

| Pulled back as | Declared |
|---|---|
| `Goal` | `goal/this.cs:22` — `class @this : item.@this` |
| `GoalCall` | `GoalCall.cs:13` — `: item.@this` |
| `BuildResponse` | `BuildResponse.cs:10` — `: item.@this` |
| `PermissionRecord` | `= app.type.path.permission.@this` |

`action.Goal.Peek() as Goal` works because the value **is** a `Goal` item. The
pattern already in the codebase: *an object that must be pulled back live earns a
real plang type; everything else is data.* No `HttpClient`/`Stream`/delegate is
ever stuffed into a Data value. **POCO→dict strands nothing.**

## Role 2 — the declared-type label (the only real work)

`clr` carries a *declared* `{name, kind}` that differs from the value's natural
type, so the wire/signed type slot survives. Sub-cases and their real homes:

- **hash → `binary/keccak256`:** already correct — `binary` has a `Kind` slot and
  `Judge` (`type/this.cs:410`) sets `new binary.@this(b.Value) { Kind = Kind }`.
  No `clr`.
- **gzip → `archived`:** the declared *name* differs from `binary` →
  `type/this.cs:431` wraps in `clr`. The proper home is the **`archive` type** —
  the same owning type as role 3. One missing type covers both.
- **domain → property-bag + `type:permission`:** a domain value serializes as a
  dict (`{type: permission, value: {…}}`); on read it's a dict but the tag says
  `permission`. The fix is `dict + type-tag → reconstruct the type` (the conversion
  catalog) — role 1 running in reverse on read, not a label.
- **`5 as int` read as `long`:** `Wire.cs:293` slaps an "int" label because the
  read doesn't coerce. **Fix: the read honors the declared kind — born `int`.**
  This is the genuine work item, in the lazy-read path (adjacent to Stage 11;
  `item.serializer.json.RawSlot` currently keeps `long` and ignores the declared
  kind).

**Universal kind.** The type entity is already `{name, kind?, strict?}` — kind is
universal at the type level. What varies is whether an *instance* can **derive**
its kind from its backing (`number` does — the CLR type of `_value` IS the kind;
`text` does) or must **store** it (`binary` can't derive `keccak256` from bytes →
carries `Kind`). Rule: **a type whose kind isn't derivable from its backing
carries a stored kind slot.** The `clr`-label was the workaround for instances
lacking that slot; give them the slot and it dies.

## Role 3 — compress/encrypt/signature → `@schema` envelopes

Already an admitted placeholder. `this.Transport.cs:92` comment: *"this nesting
belongs in an owning wrapper type (an `archive`, like `encryption` seals its
inner Data); until that type exists the construction uses the explicit no-lift
bypass."* The wire shape is

```
{ @schema: "compress" | "encrypt" | "signature", value: { @schema: "data", … } }
```

— a dedicated owning type that always wraps a Data. Retires `clr` here when those
types land (the schema-layer / born-typed work).

## Role 4 — courier passthrough → delete

`this.cs:518` (`SetValueDirect`) is flagged in its own doc as *"transitional debt
the schema-layer branch deletes; do not add callers."* Exists only for wire
reconstruction of nested Data. Dies with the schema layers.

## Role 5 — parse-failed → throw

`this.cs:227`: `json.Parse(SerializeToElement(v)) as item ?? new clr(v)`. The
`?? clr(v)` silently swallows a container that can't be made native — a producer
bug under native-only. It should be the **loud throw already drafted** in the
`Lift` TODO directly above it. (And the whole `IDictionary/IList` arm narrows
because producers build native off the wire; see `deserialize-flow-design.md`.)

## Sequencing

1. **Now (clean):** role 1 (POCO→dict) and role 5 (throw) — independent, no
   signing/schema entanglement. Fix the `Lift` `IList<object?>` predicate
   (generic invariance: a `List<Data>`/`List<int>` is non-generic `IList`, not
   `IList<object?>`) so typed lists narrow to `list.@this` instead of `clr`.
2. **Read-path work:** role 2's "honor declared kind at read" (born `int`) +
   give non-derivable types a stored kind slot.
3. **Rides existing arcs:** role 3 + role 2's `archive` type ride the schema-layer
   / born-typed work; role 4 dies with the schema layers; the domain→property-bag
   reconstruction rides the typed read.

## Open / watch

- Role 2 is entangled with signing (the signed type slot) — and signing changes
  next branch (Ingi). So role 2 naturally sequences behind that, leaving roles
  1/5 as the clean immediate move.
- Confirm the conversion catalog's `dict → record/POCO` populate covers the
  shapes producers actually need back (it does for plain records today).
- Decide the `archive` type's exact shape when role 3 lands (shared with
  encryption's existing inner-Data pattern).

## One-line summary

`clr` was never one concept — five accidents sharing a class. Every value being a
real typed thing is true the moment each accident goes to its proper home:
POCO→dict, gzip→`archive`, compress/encrypt→`@schema` envelopes, courier→deleted,
parse-fail→throw, declared-kind→honored-at-read. The only new work is the last
one; the rest is deletion or a type that's already planned.
