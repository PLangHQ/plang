# Stage 3 — arrays hold `Data` — F1 dies

**Leaf-trace rows:** A (Seam A array branch), B+J (json-array branch of `Materialize`), D (`List` navigator + `WrapItem`), I (`Conversion` arms), K (residual sweep). **The load-bearing stage.**

**You own the final shape.** Anchors for the design — change what reads wrong, keep the dispositions.

## Do

- **A** — `data/this.cs:1329` (`UnwrapJsonArray`): build `List<data>` at parse, not `List<object?>`. The ctor (`:151`) stops decomposing array tokens to raw.
- **B+J** — `data/this.cs:281` (`Materialize`) array branch / `type/this.cs:257` (`Convert`): narrow a json array → `list` holding `Data` elements; register `list` as a native type (`type/primitive/this.cs:48`), retiring the raw `List<object>` entry.
- **D** — `navigator/List.cs:54-58`: `Element` returns the element `Data` directly; the raw branch (`if (raw is @this inner) … else new @this(...)`) deletes because every element already *is* a `Data`. Delete `WrapItem` (`data/this.cs:516`) and update its callers (`:485,:493,:502`). Implicit-first nav (`:43-46`) and the intrinsics (`:22-41`) stay.
- **I** — `type/list/Conversion.cs:233-325`: when coercing to a typed `List<T>`, unwrap each element `Data` to its value. The `IList` / `JsonElement`-array / `JsonArray` arms stay; they just read `Data` elements now.
- **K** — sweep the one `is List<object?>` site (`module/builder/code/Default.cs:854`) and the ~35 `List<object?>` refs that are *value* lists to `List<data>`. Most `IDictionary` refs are infra (callstack flags, goal params) — leave those; scope the sweep to value containers.

## F1 dies here

Now that an array element is always a `Data`, a signed `Data` survives at rest inside a collection, and the writer serializes it via the `Data` wire shape. The `[raw, Data]` split — "an element might be either" — is gone. This is the finding the branch exists to kill.

The writer collision the plan warns about lands now: `List<data>` *is* `List<app.data.@this>`, the same CLR type the property-bag used (deleted in Stage 1). The writer disambiguates by type: `Dictionary<string,data>`→`{}` (from `dict`), `List<data>`→`[]`.

## Drop the `add.cs` clone

With Stage 2's rebind in place, `add.cs:43` can drop `ShallowClone` and store the element `Data` by reference — the variable's `Data` is never mutated underfoot.

## Reconcile with `Data.Load()` (row Q)

The serialize chokepoint runs `Data.Load()` (`data/this.Load.cs:30`, called from `Json.cs:77` / `plang/this.cs:141`) before writing — it walks `dict`/`list` and loads any `ILoadable` reference fundamental (an image inside the collection). That walk already covers the new shapes, so a signed image at `%list[2]%` materializes for the write for free. The one snag: `Load()` reads `.Value`, which parses an unexamined json value. For byte-passthrough to stay parse-free, `Load()` must short-circuit on a `RawUntouched` value whose `kind` is a serialization format (nothing in-memory to load) and the writer emits `_raw`. Land this with the lazy narrow.

## Acceptance

- `sign %x%` → `add %x% to %list%` → `verify %list[0]%` verifies. **(F1.)**
- `sign %img%` (an image) → `add %img% to %list%` → serialize the list → `%list[0]%` loads + verifies (the `Load()` per-element walk).
- literal `[1,"two"]` → both elements are `Data` (no raw primitives); `%list[0]%` = 1, `%list[1]%` = "two".
- array round-trips as `[]`; `read file.json` (array root) → `%content[0].name%`.
- `read file.json` → `write %content% to out.json` passthrough: `RawUntouched` stays true (no parse) — `Load()` short-circuits, writer emits `_raw` (row Q).

## Green

Both suites pass. The F1 test (`sign → add → verify %list[0]%`) is the load-bearing new test — it must fail before this stage and pass after.
