# Stage 3 — arrays hold `Data` — F1 dies

**Leaf-trace rows:** A (Seam A array branch), B+J (json-array branch of `Materialize`), D (`List` navigator + `WrapItem`), I (`Conversion` arms), K (residual sweep). **The load-bearing stage.**

**You own the final shape.** Anchors for the design — change what reads wrong, keep the dispositions.

## Settled forks (architect ruling)

- **List shape — `list.@this` wrapper, symmetric to `dict`.** A bare `List<data>` reflects each element's `Data` C# surface into junk under raw STJ (`application/json`) — the same failure that gave `dict` its wrapper + `[JsonConverter]`. Stand up the list value type holding `List<data>`, mirroring `app/type/dict/`. It owns `[]`-rendering, index/accessor navigation, and (Stage 4) compare.
- **Namespace — promote `list` to `app.type.list.@this`** (clean peer of `app.type.dict.@this`). The type *registry* currently squatting that path (`app.type.list.@this` — owns name↔CLR identity, `[Choices]`, conversions) renames to **`app.type.catalog.@this`**. This is a prerequisite mechanical step for Stage 3 (see "Registry rename" below).
- **Element wire — `.json` always bare; signatures ride `.plang`.** Plain `.json` (`application/json`) is the bare value view: `[1,"two"]` → `[1,"two"]`, a signed element's signature is *not* written to `.json`. The signed round-trip rides the `application/plang` wire (`.plang`), where every value self-describes as Data by construction (via `Normalize`) — so a signature survives there with no envelope-vs-dict discriminator in json parse. This keeps the invariant Stage 1 set for `dict` (raw-STJ = bare value view; wire = self-describing).

## Registry rename (prerequisite mechanical step)

Before standing up the list value type, move the type registry out of the `app.type.list` slot:

- Move `type/list/` → `type/catalog/`; `namespace app.type.list` → `app.type.catalog` across its files (`this.cs`, `Registry.cs`, `Loader.cs`, `Conversion.cs`, `Exit.cs`, `ITypeRenderer.cs`).
- Sweep the references: ~28 `app.type.list.@this`, ~20 `global::app.type.list`, the one `using` alias. The `App.Type` property name is unchanged (OBP: property names on `app.@this` stay PascalCase) — only the class/namespace behind it moves.
- Then `type/list/this.cs` is free for the new `list.@this` value type.
- Split note for row I: `type/list/Conversion.cs` is registry/type-system conversion — it moves to `type/catalog/`. The list-element coercion arms (coerce to typed `List<T>`, unwrap `Data` elements) belong on the new list value type. Coder owns where each arm lands.

## Do

- **A** — `data/this.cs:1329` (`UnwrapJsonArray`): build a `list.@this` (holding `List<data>`) at parse, not `List<object?>`. The ctor (`:151`) stops decomposing array tokens to raw.
- **B+J** — `data/this.cs:281` (`Materialize`) array branch / `type/this.cs:257` (`Convert`): narrow a json array → `list.@this`; register the list value type (`type/primitive/this.cs:48`), retiring the raw `List<object>` entry.
- **D** — `navigator/List.cs:54-58`: navigation collapses **into** `list.@this` (symmetric with `dict`); `Element` returns the element `Data` directly; the raw branch (`if (raw is @this inner) … else new @this(...)`) deletes because every element already *is* a `Data`. Delete `WrapItem` (`data/this.cs:516`) and update its callers (`:485,:493,:502`). Implicit-first nav (`:43-46`) and the intrinsics (`:22-41`) move onto the type.
- **I** — the list-coercion arms (today `type/list/Conversion.cs:233-325`, `IList` / `JsonElement`-array / `JsonArray`): when coercing to a typed `List<T>`, unwrap each element `Data` to its value. After the registry rename these arms live on the new list value type (or `type/catalog/`, coder's split).
- **K** — sweep the one `is List<object?>` site (`module/builder/code/Default.cs:854`) and the ~35 `List<object?>` refs that are *value* lists. Most `IDictionary` refs are infra (callstack flags, goal params) — leave those; scope the sweep to value containers.

## F1 dies here

Now that an array element is always a `Data`, a signed `Data` survives at rest inside a collection. Round-tripped through the `application/plang` wire (`.plang`), the element self-describes and `verify %list[0]%` passes. The `[raw, Data]` split — "an element might be either" — is gone. This is the finding the branch exists to kill.

No writer collision: `dict.@this` and `list.@this` are distinct wrapper types, so the json writer disambiguates by wrapper type (`dict.@this`→`{}`, `list.@this`→`[]`) — not by a raw CLR collection type. (The property-bag arm was already deleted in Stage 1.)

## Drop the `add.cs` clone

With Stage 2's rebind in place, `add.cs:43` can drop `ShallowClone` and store the element `Data` by reference — the variable's `Data` is never mutated underfoot.

## Reconcile with `Data.Load()` (row Q)

The serialize chokepoint runs `Data.Load()` (`data/this.Load.cs:30`, called from `Json.cs:77` / `plang/this.cs:141`) before writing — it walks `dict`/`list` and loads any `ILoadable` reference fundamental (an image inside the collection). That walk already covers the new shapes, so a signed image at `%list[2]%` materializes for the write for free. The one snag: `Load()` reads `.Value`, which parses an unexamined json value. For byte-passthrough to stay parse-free, `Load()` must short-circuit on a `RawUntouched` value whose `kind` is a serialization format (nothing in-memory to load) and the writer emits `_raw`. Land this with the lazy narrow.

## Acceptance

- `sign %x%` → `add %x% to %list%` → save to `.plang` → read back → `verify %list[0]%` verifies. **(F1, via the `application/plang` wire.)**
- `sign %img%` (an image) → `add %img% to %list%` → serialize the list → `%list[0]%` loads + verifies (the `Load()` per-element walk).
- literal `[1,"two"]` → both elements are `Data` (no raw primitives); `%list[0]%` = 1, `%list[1]%` = "two".
- save `[1,"two"]` to `.json` → bare `[1,"two"]` (no envelopes); a signed element saved to `.json` writes its bare value, **not** its signature (signatures are wire-only).
- array round-trips as `[]`; `read file.json` (array root) → `%content[0].name%`.
- `read file.json` → `write %content% to out.json` passthrough: `RawUntouched` stays true (no parse) — `Load()` short-circuits, writer emits `_raw` (row Q).

## Green

Both suites pass. The F1 test (`sign → add → save `.plang` → read → verify %list[0]%`) is the load-bearing new test — it must fail before this stage and pass after.

**Test-designer note:** the F1 test currently saves to `.json` (`SignedListSurvivesJsonRoundTrip`). Under this ruling it round-trips via `.plang` — rename to `...PlangRoundTrip` / `...WireRoundTrip` and point the save/read at a `.plang` path. Plain `.json` gets a separate, bare-output assertion (signature absent).
