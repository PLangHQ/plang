# `collections-are-data` — collections hold `Data`; json is a format, `dict`/`list` are native

## Why

A list today still holds `[raw, Data]`. A literal `[1,"two"]` decomposes to raw primitives (`UnwrapJsonArray → List<object?>`, `data/this.cs:1329`), but `list.add` stores a whole `Data` (`ShallowClone`, `add.cs:43`). Every consumer then copes with "an element might be either" — the code-analyzer F1 that this branch exists to kill. The root cause: the `Data` ctor *decomposes* json into raw CLR on construction (Seam A), and the read side *re-wraps* each element into a `Data` on access (`WrapItem`, `Element`). Decompose-in, recompose-out — a round-trip that crosses no boundary, just churns, and loses element type/signature on the way through.

The fix is to make collections hold `Data` end to end, built once and flowed — never decomposed to raw and never re-wrapped. That kills F1, removes the churn, and lets a signed value survive at rest inside a collection.

## What the foundation already provides

The type/kind/value foundation these collections sit on is in place. The work here is the collections core; it builds on the following and must not rebuild them:

- **`Kind` is a stored axis** (`type/this.cs:41`, `public string? Kind { get; set; }`) — caller-stamped, independent of the type-name (not derived from `KindOf(Value)`). The `item`+`kind=json` state the lazy narrow needs is representable.
- **The lazy read-seam exists** (`data/this.cs`): a `_raw`/`_value` split (`:23-37`), `FromRaw` (`:249`), `Materialize` (`:281`), `ForceMaterialize` (`:314`), a reader registry (`App.Type.Readers.Of(name, kind)`), and `type.Convert(string)` owned by the type (`type/this.cs:257`). A `Data` is built lazy and parses on first access. The one gap: its json branch materializes to *raw* collections — Stages 1 and 3 repoint it at native `dict`/`list`.
- **Type-lattice ancestry exists** — `type.Is(other)` walks CLR inheritance + facets transitively (`type/this.cs:305`, `Reaches` `:317`). The `item` apex (Stage 6) only registers the top of the lattice; the query machinery is in place.
- **`add.cs` shallow-clones the `Data`** (`add.cs:43`, `Value.ShallowClone` — value/type/signature shared by reference), not a json deep-clone. Only the dot-path `SnapshotClone` (`variable/list/this.cs:298`) and the `Variables.Set` rebind remain (Stage 2).
- **`Data.Load()` runs at the serialize chokepoint** (`data/this.Load.cs:30`, called from `channel/serializer/Json.cs:77` and `channel/serializer/plang/this.cs:141`) — an async pre-pass that walks nested `Data`/dict/list and calls `ILoadable.LoadAsync()` on each reference fundamental (image bytes) before the sync STJ writers run, surfacing `StrictKindMismatch` cleanly. It already walks the `dict`/`list` shapes this work produces, so a per-element reference fundamental — a signed image inside a list — materializes for free. One seam to reconcile: it reads `.Value`, which parses an unexamined json value (row Q).

## You own the final shape

Every code snippet, file path, and signature below is a **suggestion to anchor the design** — coder owns the final shape. Where a name or arm reads wrong in code form, change it; keep the *behavior dispositions* (what gets deleted, collapsed, relocated) and the *acceptance goals*. The leaf-trace is the contract; the C# is illustration.

## The model

Six things, settled in the design session with Ingi.

**1. Format vs native — the line that dissolves the "why convert" worry.** json / protobuf / base64 are *serialization formats* (the wire). `dict` / `list` / `number` / `path` / `image` are *native representations* (in-memory). `Data` wraps the native value. Deserializing a json string into a `dict` is **not** the round-trip smell — it's just parsing, a real boundary crossed once. The smell is taking the *already-native* dict, decomposing it to raw CLR, and rebuilding it on read. That churn crosses no boundary. The refactor deletes the churn and keeps the parse.

**2. `Data` is the universal box; `item` is the apex of the type lattice — not a second box.** Every value is wrapped in `Data` (name, type-tag, value, signature). `item` is the top of the *type* tree (≈ C# `object`): `number`, `dict`, `list`, `path` all is-a `item`. The apex **stores nothing** — `set %x% = 1` is a `number` holding a raw `long`; it is-a `item` but is never *stored as* an item. C# fuses box and apex into `object`; PLang keeps them apart. So a list is `Data<List<data>>` — never `Data<List<item>>`. `item` is a predicate on the type-tag (`data.Type.Is("item")`), not a storage parameter.

**3. The type narrows to the concrete shape on examination.** A value read as json sits as `item` with `kind=json` + serialized bytes until something looks inside — the serialization format lives in `Kind`, not the type. **`Kind` is the stored, caller-stamped axis this needs** (`type/this.cs:41`) — the reader sets it from the `.json` extension, not derived from the type-name. First touch parses, sees `{` vs `[`, and narrows `data.Type` item → `dict` / `list`; the stored `kind` clears (a `dict` is a native type, not a format). IS-A is preserved through the narrow (`Type.Is("item")` stays true, `Type.Is("dict")` becomes true). The payoff: **passthrough never narrows** — `read file.json` → `write %content% to out.json` never looks inside, stays `item` / `kind=json` + raw bytes, written back byte-for-byte, zero parse. This is the same lazy pattern the value factory already runs (`FromRaw` + `Materialize`), applied to the type.

**4. Behavior lives on the value/type; a module is only language exposure.** The `list` / `dict` action modules declare the PLang-visible parameters and **forward** — they hold no algorithm. Navigation, serialization, and comparison live on the native types. `dict` and `list` are **symmetric first-class value types**: `dict` owns key-lookup and serialize-as-`{}`, `list` owns index/accessor navigation and serialize-as-`[]`, and *both* `navigator/Dictionary.cs` and `navigator/List.cs` collapse **into** their value types. Each value's **type** owns its own comparison (the `IBooleanResolvable` pattern, extended to ordering). A handler that *computes* instead of *forwards* is the smell. (Naming snag: `app/type/list/` is the type *registry* — the list value type needs a sibling folder; the registry keeps `type/list`. Folder name is coder's call.)

**5. Comparison is owned by the type, and there's one compare path.** The typed comparison `sort`/`where`/`group` need already exists as `Operator.Compare` + `NormalizeTypes` (`module/condition/Operator.cs:101,:160`) — but as static helpers reaching into raw `.Value` via CLR `IComparable`, separate from `sort.cs`'s `Comparer<object>.Default`. `number.@this` already owns a `CompareTo` for its own arithmetic (`type/number/this.Equality.cs:41`), but `Operator.cs` doesn't call it — it normalizes and compares the unwrapped CLR value. Relocate the compare onto the types (`number`/`datetime`/`primitive` own compare), and route **both** the condition operators (`>`,`<`,`==`) **and** `data.sort` through it. Then `if age > 18`, `where age > 18`, and `sort by "age"` collapse to one comparison instead of three that drift.

**6. `where` is a `dict`+`list` capability; `sort`/`group` are `list`-only.** `where` evaluates a predicate with the subject *scoped* — bare field names resolve against it. On a `dict` the subject is the dict itself (`%user% where age > 20` → keep the user or drop it; bare `age` means `%user.age%`). On a `list` the subject is each element, so `%users% where age > 20` filters — and `list.where` is *literally* `dict.where` applied per element. The win is **uniformity**: `where age > 20` reads identically on one item or a thousand. `sort`/`group` stay `list`-only: ordering needs ≥2 and grouping one thing is a bucket of one. `where` needs *fields* to scope into, so it lives on the field-bearing types (`dict`, and `list` via its elements), never the apex — `5 where age > 20` stays meaningless, correctly.

## The movie — `read file.json` → `%content.name%`

```
- read file.json, write to %content%
- ... %content.name% ...
```

1. **read** — the reader sees `.json` → stamps `kind=json` on the value (`Kind` is the stored axis, `type/this.cs:41`). Reads the bytes into `_raw`; does not parse yet (`FromRaw`, `data/this.cs:249`): holds the serialized bytes as `data.Type = item, kind=json` ("some json value, haven't looked inside").
2. **first touch** — `%content.name%` needs a native value. `Materialize` (`data/this.cs:281`) parses, inspects the root token, narrows the type:

   ```
   '{'    → native dict
   '['    → native list
   scalar → number / text / bool / null
   ```

   This is the one place json-token-kind maps to native PLang type — the inverse of what the writer does (`dict → {`, `list → [`). A single shape-blind `object` type cannot carry it: `.name` is a key on a dict and meaningless on an array, and on egress you'd not know whether to emit `{` or `[`. So the reader **must** branch object-vs-array; the product is `dict` or `list`.
3. **navigate** — `%content%` is now a `dict`; `.name` is `dict.Get("name")` → a `Data`.

**Array root, same line.** If the file's root is `[...]`, `%content%` narrows to `list`, and `%content.name%` falls through to the first element: `content[0].name` (the implicit-first trick, preserved — `navigator/List.cs:43-46`). Precedence: list-intrinsic accessors (`count`/`length`/`first`/`last`/`random`/index, `navigator/List.cs:22-41`) win; any other key falls through to `[0]`. Known and intended.

## The read seam — content reads and `.pr` params

Content reads (`read file.json`, an http body) happen on the execution path — parsing there wastes nothing. `.pr` parameter loads happen up front, off the execution path, so eager parsing there pays for actions an `if`/exit may never reach. One lazy seam handles both:

- `FromRaw` (`data/this.cs:249`) builds a `Data` holding `_raw` + a type, parsing nothing.
- `Materialize` (`:281`) parses on first access, dispatching through the reader registry (`App.Type.Readers.Of(name, kind)`), falling back to `type.Convert(raw)` (`type/this.cs:257`) for a string raw with a known type. `RawUntouched` (`:272`) proves an unreached param never parsed.
- `ForceMaterialize` (`:314`) is the navigate-time entry into the read-through.

The one gap: `type.Convert`'s json branch (`type/this.cs:257`) deserializes to a **raw** `Dictionary<string,object?>`, and the reader for json produces raw collections — so it materializes to *raw*, not native `dict`/`list`. Stages 1 and 3 repoint that one branch; when `dict`/`list` exist, `Materialize` narrows to them.

## The leaf-trace

Each incumbent behavior, where it lives, what it does today, and its disposition. This is the contract for the work. Rows marked **already in place** exist today — listed so coder doesn't rebuild them.

| # | incumbent | where | does today | disposition |
|---|---|---|---|---|
| A | `UnwrapJsonElement` / `Array` / `Object` (Seam A) | `data/this.cs:151` (ctor), `:1248-1293`, `:1319-1327` (→`Dictionary<string,object?>`), `:1329-1337` (→`List<object?>`) | decompose json token → raw collections on **every** `new Data(…)` | **retarget** — build `dict` / `List<data>` once at parse; ctor stops decomposing. The lazy seam (`Materialize`) narrows to native, not raw |
| B | navigate-time materialize (Seam B) | `data/this.cs:314` (`ForceMaterialize`), `:281` (`Materialize`), `type/this.cs:257` (`Convert`) | the lazy read-through; json branch produces a **raw** dict | **repoint** — `Convert`/reader json branch produces native `dict`/`list`. Not a delete — the seam is correct, the target is wrong |
| C | `Dictionary` navigator | `variable/navigator/Dictionary.cs:38-83` (3 arms + reflection + Count rule `:34`) | navigate any `IDictionary` shape by key, dispatching per shape | **collapse into the `dict` value type**: `if (data.Value is dict d) return d.Get(key)` — one shape, no per-arm dispatch |
| D | `List` navigator (`Element` raw branch + `WrapItem`) | `navigator/List.cs:54-58`; `WrapItem` `data/this.cs:516`, callers `:485,:493,:502` | navigate a list + re-wrap raw elements on read | **collapse into the `list` value type** (symmetric with C): navigation moves onto `list`; elements are already `Data` so the raw branch + `WrapItem` delete; implicit-first nav stays |
| E | property-bag writer case | `channel/serializer/json/writer.cs:152` (`case List<app.data.@this>`) | emit a list-of-named-`Data` as `{}`; arrays fall to the `IEnumerable` arm `:165` as `[]` | **delete** — `dict` serializes via its renderer; `List<data>` serializes as `[]` by type |
| F | `NormalizeObject` | `data/this.Normalize.cs:170-210` | reflect a C# domain record → `List<@this>` property bag | **retarget** → produce a `dict`; C# records fold into the one object form |
| G | `Operator.Compare` / `NormalizeTypes` / `AreEqual` | `module/condition/Operator.cs:101,:160,:87`; `IsNumeric` `:194` already knows `number.@this` | typed compare on raw `.Value` via CLR `IComparable`; static, not type-owned | **relocate** onto the types; operators **and** `data.sort` route through the type's compare |
| H | `list` action handlers (`sort`/`group`/`where`/…) | `module/list/*.cs` (18 actions, **no `where.cs`**); `sort.cs:19` (`Comparer<object>.Default`), `group.cs:18-27` (keys via `GetChild`, stores raw `item.Value`) | reach into `data.Value as List<object?>`, reinvent element handling | **thin dispatch** to `data` + element-type compare; `where` lands on `dict` (scoped keep/drop) **and** `list` (filter, delegating per-element to `dict.where`); `sort`/`group` stay list-only |
| I | `Conversion` list arms | `type/list/Conversion.cs:233-325` (`IList` / `JsonElement`-array / `JsonArray` arms — *not* `is List<object?>`) | coerce to/from raw lists via `System.Collections.IList` | **unwrap `Data` elements** when coercing to a typed list |
| J | primitive type map | `type/primitive/this.cs:48` (`list`→`List<object>`), `:51` (`dict`→`Dictionary<string,object>`), `:53` (`object`→`object`), `:55` (`json`→`JsonNode`) | maps `dict`/`list` to **raw** CLR generics; no native value type | **register** `dict`/`list` native types and **retire** the raw-generic entries; reader narrows by root token. Ownership: object/`dict` Stage 1, array/`list` Stage 3, `item` Stage 6 |
| K | residual `is List<object?>` sites | only **1** `is List<object?>` (`module/builder/code/Default.cs:854`); ~35 `List<object?>`, ~80 `IDictionary` refs in `app/` | branch on raw list | **sweep** to `List<data>` where it's a value list (most `IDictionary` refs are infra, not value containers — scope carefully) |
| L | `SnapshotClone` | def `data/this.cs:1241`; use `variable/list/this.cs:298` (dot-path `set`). `add.cs:43` uses `ShallowClone`, **not** this | json deep-clone for value-independence on a dot-path `set` | **delete the dot-path use** — independence comes from `set` rebinding; `add.cs` already shallow-clones |
| M | `Variables.Set` in-place — **raw-value branches only** | `variable/list/this.cs` — Data-value branch `:137-191` (already rebinds, carries `OnCreate/OnChange/OnDelete`), raw frame-overlay `:199`, raw underlying-dict `:227` | mutate `Data.Value` in place on a same-type raw `set` | **rebind** the two raw branches (`:199`, `:227`). The Data-value branch already rebinds — replicate its subscriber-carry on the raw rebind. **Don't miss `:199`** — skipping it reintroduces the alias bug inside channel-fire / parallel-foreach flows |
| N | `.pr` lazy params | `FromRaw`/`Materialize`/`RawUntouched`/reader registry | lazy build + parse-on-first-access | **already in place** — the lazy seam materializes; Stages 1/3 only repoint its target to native. Confirm `Wire.Read` mints a `FromRaw` lazy `Data` |
| O | `Kind` stored axis | `type/this.cs:41` (`public string? Kind { get; set; }`) | `Kind` is stored/caller-stamped, independent of type-name | **already in place** — the `item`+`kind=json` state is representable; no decoupling work left |
| P | `type.Is` ancestry | `type/this.cs:305` (`Is(@this? other)`), `Reaches` `:317`, facets via static `Type` list | transitive type-lattice query | **already in place for the query** — Stage 6 only registers `item` as the apex and (if needed) adds a name-string `Is(string)` overload |
| Q | `Data.Load()` serialize chokepoint | `data/this.Load.cs:30`; calls `Json.cs:77`, `plang/this.cs:141`; walks `IDictionary`/`IEnumerable` | pre-serialize pass; loads `ILoadable` reference fundamentals, then serializer reads `.Value` | **reconcile with the lazy narrow (Stage 3)** — `Load()` reads `.Value`, which parses an unexamined json value, defeating byte-passthrough. For a `RawUntouched` value whose `kind` is a serialization format (nothing in-memory to load), `Load()` short-circuits and the writer emits `_raw`. Per-element `ILoadable` materialization inside `dict`/`list` already works — the walk covers the new shapes |

## The stages

Each stage builds and leaves both suites green. Sequencing is forced by one fact: **once arrays become `List<data>`, the writer cannot tell an array from the property-bag — they are the same CLR type** (`List<data>` *is* `List<app.data.@this>`). So you cannot land arrays-as-Data without first removing the property-bag overload. That is why `dict` comes first.

**The transitional writer state is the subtle part.** In Stage 1 the writer is `dict`→`{}`, arrays still `List<object?>`→`[]` — the CLR-type collision *doesn't exist yet*. It appears only in Stage 3, when arrays become `List<data>` and finally share a CLR type with the property-bag overload — which Stage 1 already deleted. So that deletion becomes load-bearing *exactly then*, not in Stage 1 — don't read the collision as something Stage 1 resolves.

1. [`dict` — the native object type](stage-1-dict.md). Stand up `app/type/dict/` (mirrors `path`): holds `Dictionary<string,data>`, `Get`/`Keys`/`Has`, `IBooleanResolvable`, build-at-edge, a serializer that emits `{}`. Reader narrows json object → `dict`; `Materialize`/`type.Convert` json-object branch repointed to it (B, J). Collapse the `Dictionary` navigator (C). Retarget `NormalizeObject` and delete the property-bag writer case (E, F). **Arrays untouched.** Unblocks Stage 3.
2. [`set` rebinds, not mutates](stage-2-set-rebinds.md). Rebind the two raw `Variables.Set` branches (`:199`, `:227`), carrying subscribers like the Data-value branch already does (M). Delete the dot-path `SnapshotClone` use (`:298`, L). The `add.cs` deep-clone is already gone, so once `set` rebinds, `add.cs`'s `ShallowClone` can drop to storing the reference. Prerequisite for Stage 3.
3. [arrays hold `Data` — F1 dies](stage-3-arrays-as-data.md). `UnwrapJsonArray → List<data>` at parse; `Materialize` array branch narrows to `list` (A, B, J); `navigator/List` returns the element `Data` directly; delete the `Element` raw branch + `WrapItem` (D); `Conversion`/`IList` arms unwrap (I); sweep residual `is List<object?>` (K). **F1 dies here.** A signed `Data` survives as an element; json serializes it via the `Data` wire shape.
4. [comparison onto the type — one compare path](stage-4-comparison-on-type.md). Relocate `Operator.Compare`/`NormalizeTypes` onto the types; route the condition operators and `data.sort` through it (G), honoring the settled compare contract below.
5. [list/dict ops as exposure — `where` on `dict`+`list`](stage-5-list-dict-ops.md). `list.sort`/`group`/`unique` → thin dispatch; `where` becomes a `dict`+`list` capability — `dict.where` (scope = self, keep/drop) is the leaf, `list.where` delegates per element (H). Bare field names scope against the subject. Phase B.
6. [`item` apex — register the top of the lattice](stage-6-item-apex.md). `type.Is` ancestry already works (P); register `item` as the apex type so `if %x% is item` / `if %x% is dict` resolve, and add a name-string `Is(string)` overload if the query surface needs it. Separable follow-on — off the F1 critical path, parallelizable once `dict`/`list` exist.

## `set` rebinds, not mutates (Stage 2 detail)

`Variables.Set`'s two raw branches *update the binding in place* for a same-type `set` (`:199`, `:227`: `existing.Value = value`). The Data-value branch already rebinds — mints/replaces and carries `OnCreate/OnChange/OnDelete` across (`:137-191`). Make the raw branches do the same: **`set` always mints/replaces and carries subscribers across.**

```
set %x% = "a"        → Data_A{x:"a"}
add %x% to %list%    → list holds Data_A         (the reference)
set %x% = "b"        → mint Data_B{x:"b"}, x → Data_B; Data_A untouched
%list[0]%            → "a"
```

This matches how most languages behave — reassignment rebinds the variable, it doesn't reach back and mutate a value already stored elsewhere. Once it's in, `add.cs`'s `ShallowClone` (`:43`) can drop to storing the variable's `Data` reference, and the dot-path `SnapshotClone` (`:298`) deletes — value-independence falls out of rebinding, not defensive copying. Land it **before** Stage 3.

## The compare contract (settled)

The type owns compare. The semantics, settled with Ingi:

- **Within a type — natural order.** number numerically (across kinds via numeric widening), datetime chronologically, duration by length, text lexically.
- **Nulls sort last.**
- **Ordering two genuinely different value types throws** a clear error ("cannot order X against Y") — no invented cross-type order. The operator coercions `NormalizeTypes` already does (numeric widening, string↔number) are preserved on the `if` path; this rule governs ordering distinct value types — the sort/list case.
- **Orderable:** `number`, `datetime`, `duration`, `text`. **Equality-only:** `dict`, `list`, `bool`, `table`, `null`. `sort` on an equality-only type throws (no order defined); `group`/`unique`/`==` work on any type.

The adapter seam is coder plumbing: the entry that takes two element `Data`, picks the type, and compares — dispatching to the element type's compare, throwing the mixed-type error when the two differ, placing nulls last. `number.@this : IComparable<@this>` already compares two `number` instances (`type/number/this.Equality.cs:41`); the seam reaches the typed instance, not the raw CLR value. Stages 4–5 implement against this contract; they are no longer blocked.

## Resolved by the model (no longer open)

- **Per-element signing** — resolved by "native list = `List<data>`": elements are `Data` in memory, so signature/type ride along; json serializes them via the `Data` wire shape. A consequence, not a feature to design.
- **C# records on the wire** — fold into the `dict` object form (F). One object shape.
- **`type.list` wrapper** — `value` becomes `List<data>`, `count` derivation unaffected. Mechanical.
- **`Kind` decoupling, `type.Is` ancestry, lazy `.pr` loading** — already in place (O, P, N).

## Acceptance goals

- `read file.json` → `%content.name%` resolves for an object-root file; for an array-root file it resolves `content[0].name`.
- `sign %x%` → `add %x% to %list%` → `verify %list[0]%` still verifies (the F1 gap, Stage 3).
- object round-trips as `{}`, array as `[]`, a C# record (`permission`) as `{}`.
- nested navigation `%person.address.city%`.
- read-json → write-json passthrough does not parse: `RawUntouched` stays true and the writer emits `_raw`. Requires `Data.Load()` to short-circuit on a `RawUntouched` serialization-format value (row Q) — without that, the serialize chokepoint parses it.

## Cross-cutting decisions

- No back-compat: `.pr` and wire shapes adapt; no migration shims.
- One object shape on the wire — `dict`. No parallel "property bag" type survives.
- `item` never appears as a storage generic (`List<item>` is wrong); it is a type-tag predicate only.

Test handoff: [plan/test-strategy.md](plan/test-strategy.md) (layer mapping, the F1 load-bearing proof, per-stage integration cuts) and [plan/test-coverage.md](plan/test-coverage.md) (coverage matrix, failure/mutation matrix, new-surfaces inventory).
