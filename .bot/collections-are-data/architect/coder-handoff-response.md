# Architect response — Stage 3 forks

**To:** coder · **From:** architect · Re: `.bot/collections-are-data/coder/handoff.md`

Both forks ruled. Stages 1–2 look right — green, symmetric `dict`, rebind via `WalkContainerVars`. Proceed.

## Fork 1 — list in-memory shape → **(a) `list.@this` wrapper**

Symmetric to `dict`. A bare `List<data>` reflects its element surface into junk under raw STJ — the exact failure `dict`'s wrapper + `[JsonConverter]` exists to fix; the list type gets the same treatment. Behavior (nav, `[]`-render, compare) lives on the type, not scattered as statics. The test-designer's `ListValueType` tests already assume it.

## Namespace — **promote `list` to `app.type.list.@this`** (Ingi's call)

Not the nested `app.type.list.list.@this`. The list value type takes the clean `app.type.list.@this`, a peer of `app.type.dict.@this`. The type **registry** currently on that path (name↔CLR identity, `[Choices]`, conversions) renames to **`app.type.catalog.@this`**.

Prerequisite mechanical step, do it first in Stage 3:
- `type/list/` → `type/catalog/`; `namespace app.type.list` → `app.type.catalog` (6 files: `this.cs`, `Registry.cs`, `Loader.cs`, `Conversion.cs`, `Exit.cs`, `ITypeRenderer.cs`).
- Sweep refs: ~28 `app.type.list.@this`, ~20 `global::app.type.list`, 1 `using` alias. `App.Type` property name is **unchanged** (property names on `app.@this` stay PascalCase) — only the class/namespace behind it moves.
- `type/list/this.cs` is then free for the new `list.@this` value type.
- `Conversion.cs` is registry/type-system conversion → moves to `catalog`. The list-element coercion arms (typed `List<T>`, unwrap `Data` elements — row I) belong on the new `list.@this`. Your split.

## Fork 2 — element wire → **(a-adjacent, sharpened): `.json` always bare; signatures ride `.plang`**

Not your (a) bare-unless-signed-in-json, and not (b) always-envelope. The clean rule, consistent with what Stage 1's `dict` already set (raw-STJ = bare value view; wire = self-describing):

- **Plain `.json` (`application/json`) is always the bare value view.** `[1,"two"]` → `[1,"two"]`. A signed element's signature is **not** written to `.json`. No per-element envelope, so no envelope-vs-dict discriminator to invent in the json parse.
- **The signed round-trip rides the `application/plang` wire (`.plang`).** There every value self-describes as Data by construction (via `Normalize`), so a signature survives with zero special-casing.

This makes F1 a property of the wire format, where it actually lives — a stronger test than saving to `.json`.

**Test-designer reshape (flagged):** `SignedListSurvivesJsonRoundTrip` saves to `.json`. Under this ruling it round-trips via `.plang` — rename `...PlangRoundTrip` / `...WireRoundTrip`, point save/read at a `.plang` path, and add a separate assertion that the same signed list saved to `.json` is **bare** (value present, signature absent). Stage-3 doc + test-strategy/coverage updated.

## You're unblocked — the (corrected) Stage-3 path

1. Registry rename `app.type.list` → `app.type.catalog` (above).
2. Stand up `list.@this` at `app.type.list.@this`, mirroring `dict` (holds `List<data>`, `[JsonConverter]` for raw-STJ, `IBooleanResolvable`, `[]` serializer).
3. `UnwrapJsonArray` → `list.@this` at parse; `Materialize` array branch narrows to it; retire the raw `List<object>` primitive-map entry.
4. Collapse `navigator/List` into `list.@this`; `Element` returns the element `Data`; delete `WrapItem` + the raw branch; intrinsics + implicit-first move onto the type.
5. List-coercion arms unwrap `Data` elements (row I).
6. Drop `add.cs`'s `ShallowClone` to a reference (Stage 2's rebind makes it safe).
7. Reconcile `Data.Load()` row Q: short-circuit on a `RawUntouched` serialization-format value so byte-passthrough stays parse-free; the per-element `ILoadable` walk already covers `dict`/`list`.
8. Sweep residual `is List<object?>` value sites.

Land F1 via `.plang`. Full detail: [stage-3-arrays-as-data.md](stage-3-arrays-as-data.md).
