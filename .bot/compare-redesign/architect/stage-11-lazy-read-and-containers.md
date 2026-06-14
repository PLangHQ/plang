# Stage 11: Lazy read + lazy containers — store raw, type on read

**Status (2026-06-14):** new — settled with Ingi this session. Resolves the coder's [`coder/deserialize-flow-design.md`](../coder/deserialize-flow-design.md) (architect review was requested there). One stage, two parts — **A** the wire read path, **B** the `list`/`dict` backings. They interlock: the read fills the raw container that B stores, so they land together.

## Why

Two places still process eagerly, against the lazy spine the rest of the branch already follows. (1) The wire read builds a throwaway JSON tree for the value slot, then re-reads it — and for the lazy path it re-stringifies and parses a third time. That is the triple-parse the coder's doc diagnosed, and the reactive patches around it (the JsonElement unwrap, the `deferredRaw` re-stringify, the shaped-value lift) are its symptoms. (2) `list` and `dict` back with `List<Data>` — a Data minted per element at load, even for `set %x% = 1,2,3,4` that may never be read. Both contradict the rule the rest of the model holds to: store the raw value, type only the piece you read, only when you read it. This stage moves the read boundary and the container backing onto that rule.

## The principle (one line)

Store raw, type on read. A value read from `.pr` is stored as it arrived; an element/entry becomes its type when (and only when) something reads it; an untouched value passes back out verbatim.

## Part A — the read path

The coder already built the right contract: a per-(type, kind) `Read(object raw, kind, ctx)` reader. The serializer turns wire bytes into a plain CLR value; the type turns that plain value into itself. That is already "the type reads its own value," and it is already format-agnostic — the type sees a `string` / `long` / list, never JSON. text/dict/list/bool readers ship today.

**Decision: keep `Read(object raw)`. Do not build `IReader`.** The write side mirrors cleanly because the JSON writer is an ordinary class you can pass around. The read side does not: `Utf8JsonReader` is a `ref struct` (stack-only) — it cannot be a field of a class and cannot cross an interface boundary — so a format-agnostic `IReader` cannot wrap it. Making `IReader` real means hand-rolling a non-stack reader, or rebuilding the reader on every pull — real work, and slower — to buy skipping one already-cheap decode. Keep the fast reader inside the JSON serializer (used by ref, where that is legal); only plain values cross the format boundary. `IReader` does not make anything more format-agnostic than `object raw` already is — both are one format-agnostic read; the only difference is whether a plain value sits in between, and for a scalar there is no difference at all.

**The fix lives in Wire, not in the contract.** Wire's value-slot handling is the wart: decode the value slot once into a plain value and hand it to the existing reader. The envelope walk (name / type / signature / properties) stays; only the value case changes. Delete the throwaway-tree machinery (demolition below).

The four open questions from the coder's doc, answered:

1. **IReader shape** — not built (above). The `object raw` contract stands. If a genuinely schema-driven non-JSON format (protobuf) ever lands and needs a type-driven pull, revisit then; the self-describing formats we would realistically add next (msgpack, CBOR) carry their own type tags like JSON, so `object raw` covers them.
2. **Type-before-value invariant** — keep it; reject value-first. Our writer always emits type before value, so a value-first `.pr` is malformed — fail loud with a clear error, no buffering to tolerate a shape we never write. (A type that is *absent* is a different case — that is question 3, not value-first.)
3. **No declared type (polymorphic)** — the `object` / `item` type owns "read whatever is there." With `object raw` it is trivial: the decoded value is already a typed CLR thing — number → `number`, string → `text`, bool → `bool`, a plain map → `dict`, a plain list → `list`.
4. **Containers own their elements** — yes, and lazily (Part B). The container stores the raw sequence / map; an element types itself on read, not at load. The per-element walk moves off Wire and onto `list` / `dict`.

## Part B — lazy containers

`list.value` is a plain `List<object>`; `dict.value` is a plain `Dictionary<key, object>`. Each slot holds **either** a raw CLR value (from a literal load) **or** a `Data` (dropped in by `add` / `set`, carrying its own type). One Data wraps the whole container — never a Data per element at rest.

- **Read normalizes per slot:** already a `Data` → return it; raw CLR → wrap into a `Data` of its natural type, and cache that back into the slot (the narrow). An element converts raw → Data once, on first touch.
- **Untouched stays raw:** `count`, verbatim write-out, and relay never wrap. A container read from `.pr` and written back out emits the raw bytes unchanged — verbatim passthrough is the never-read path, the same shape as a reference's never-narrowed path in the plan's rule 7.
- **`add` / `set` drop the value in as-is** — a `Data` when it came from a typed value (`%Now%`), a raw value for a literal. The container stays lazy either way.
- **Mixed types ride for free:** a raw `1` reads as `number`, a raw `"name"` as `text` — the value's own form tells the read its kind, so a mixed `[1,2,3,4,"name"]` needs no per-element Data. A type tag sits on an element only when the raw alone is ambiguous (a date stored as the string `"2026-01-01"`), and then that element is a `Data` / `{type, value}` in the slot.
- **dict key:** the map key is the identity, the way position is for `list`. A `Data` stored as an entry keeps its type / properties / signature; its own `Name` is vestigial — the map key holds the binding. (Today dict uses the entry `Data.Name` AS the key, via the `_index`; that moves to the map key and the entry name stops being load-bearing.)

Worked trace (the example we settled on):

```
set %x% = 1,2,3,4             →  Data{ list, value: [1,2,3,4] }              (raw, one Data)
add 'name' to %x%            →  Data{ list, value: [1,2,3,4,"name"] }       (append the raw value)
set %t% = %Now% ; add %t%    →  Data{ list, value: [1,2,3,4,"name", Data{datetime}] }   (Data dropped in)
read %x%[2]                   →  raw 3 wraps to number(3), cached back into the slot
read whole %x%, write out     →  [1,2,3,4,…] verbatim — untouched slots are never wrapped
```

dict is identical, keyed: `set %u% = {name:"ingi", age:40}` stores one Data over a raw map; `%u%.age` wraps the raw `40` to a number on read; `set %u%.created = %Now%` drops a datetime Data in under the `created` key.

## Scope

In: Wire's value-slot read; the `list` / `dict` backings and their element / entry access; the typing-on-read + cache-back. Out: the `Read(object raw)` contract itself (kept); the signing / compress / schema layers (a value stored as its own Data still signs as before); the typed-key question for dict (`Dictionary<text, …>` vs `<string, …>`) — note it, decide it when you carve the backing, do not let it gate the stage.

## Demolition worklist

Dies in Part A (Wire — `PLang/app/data/Wire.cs`):
- `LiftDataIfShaped` (`:465`) and `HasDataMarker` (`:497`) — the "value slot is a nested Data" rehydrate. The target model has no nested-Data-in-value (one layer; an element in a list/dict is the only place a Data rides, handled by Part B).
- `LiftArrayElements` (`:481`) — array-element lift moves onto `list` (Part B): the container stores the raw array and types elements on read.
- `IsDeferrableShape` (`:380`) plus the `deferredRaw` capture and the `GetRawText` re-stringify (`:240`, `:246-256`, `:328-332`) — lazy capture becomes the value's raw bytes uniformly, not a re-stringified subset of "deferrable" shapes.
- `_readDepth` / `MaxReadDepth` (`:135-136`, `:155-174`) — the depth counter existed because `LiftDataIfShaped` restarted STJ recursively. With a single-pass decode it goes. If recursion still needs a bound, keep one and say why — do not drop the guard silently.
- The three-branch value switch (`StartObject` / `StartArray` / else, `:340-358`) collapses to one decode-to-plain-value.
- The two `SetValueDirect` courier branches in `ReadBody` (`:262-296`) — re-judge against the single-layer model; they are marked transitional debt already.

Dies in Part B (containers):
- `list._items : List<Data>` (`PLang/app/type/list/this.cs:27`) → `List<object>` (raw-or-Data). The construct-time wrap (`@this(IEnumerable<Data>)`, `:30`) and every `_items[r].Peek()`-style op (`At`, `Insert`, `RemoveAt`, `SortByValue`, `ResetTo`, `Items`, `LeafCount`) re-expressed to normalize-on-read.
- `dict._entries : List<Data>` + `_index : Dictionary<string,int>` keyed on `Data.Name` (`PLang/app/type/dict/this.cs:33-34`) → `Dictionary<key, object>`. The key moves to the map; entry `Name` stops being the key.

Recorded as NOT built (so it does not return): the pull-model `IReader`, and any `serializer/<kind>.cs` reader signature that takes a live reader instead of `object raw`.

Stays (do not touch): the `Read(object raw, kind, ctx)` registry (`PLang/app/type/reader/this.cs`) and the per-type `serializer/<kind>.cs` readers (text / dict / list / bool already shipped); `Data.FromRaw` / the `item.source` carrier for scalar lazy values; the entire wire writer side (`IWriter`, the renderers).

## Leaf trace

- `Wire.ReadBody` (`PLang/app/data/Wire.cs:233`) is the incumbent value-slot reader — the three-branch decode, the deferral, and the lifts above. Part A rewrites its value case only; the envelope walk for name / type / signature / properties is unchanged.
- `list.@this` (`PLang/app/type/list/this.cs`) backs with `List<Data> _items`; every op reads `_items` as Data today. Part B re-expresses each op to "the slot is raw-or-Data, normalize on read."
- `dict.@this` (`PLang/app/type/dict/this.cs`) backs with `List<Data> _entries` + a name→index `_index`. Same re-expression, plus the key relocation off `Data.Name`.
- `Data.FromRaw` (`PLang/app/data/this.cs:373`) already builds a source-backed lazy Data for a scalar value — the container path is the same idea applied to the whole container value.

## Dependencies

Stage 9 (born-typed — Data holds a typed instance) and Stage 10 (typed interior). No dependency on the comparison stages (1, 4–6).

## You own this (coder)

The code shapes above — the `List<object>` / `Dictionary<key, object>` backing, the normalize-on-read branch, the line references — are the design intent, not the implementation. You own the final member names, whether normalize-and-cache lives on the container or a shared helper, whether the dict key is `string` or `text`, and how `add` / `set` insert. Non-negotiable: store raw, type on read; one Data over the whole container (never a Data per element at rest); untouched containers pass through verbatim; the `Read(object raw)` contract stays and `IReader` is not built. If implementing surfaces a reason any of that is wrong, stop and say so — don't paper over it.
