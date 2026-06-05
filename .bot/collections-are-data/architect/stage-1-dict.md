# Stage 1 — `dict` is the native object type

**Leaf-trace rows:** C (Dictionary navigator), E (property-bag writer), F (`NormalizeObject`), B+J (json-object branch of `Materialize`/`Convert` + primitive map). **Arrays untouched.**

**You own the final shape.** File paths, class names, and signatures below are anchors for the design — change any that read wrong in code. Keep the dispositions (what collapses into `dict`, what deletes) and the acceptance goals.

## Why this is first

Once arrays become `List<data>` (Stage 3) they share a CLR type with the property-bag (`List<app.data.@this>`), so the writer can't tell `[]` from `{}`. `dict` has to own the object shape *before* that collision exists, so the property-bag overload can be deleted while arrays are still `List<object?>`.

## Build

Stand up `app/type/dict/` mirroring `app/type/path/`:
- holds `Dictionary<string,data>` (named `Data` values), `Get(key)` / `Keys` / `Has(key)`, `IBooleanResolvable` (truthy when non-empty), a build-at-edge factory.
- a serializer that emits `{}` keyed by entry name — the dict owns its own `{}` rendering.
- register `dict` as a native type in the primitive map (`type/primitive/this.cs:51`), replacing the raw `Dictionary<string,object>` entry (J).

## Collapse / retarget

- **C** — `variable/navigator/Dictionary.cs:38-83`: the 3-arm shape dispatch collapses to `if (data.Value is dict d) return d.Get(key)`. The reflection arm and the Count rule (`:34`) move onto `dict` (`count` is an intrinsic key that loses to a real `count` key).
- **F** — `data/this.Normalize.cs:170-210`: `NormalizeObject` returns a `dict` (`Dictionary<string,data>`), not `List<@this>`. C# domain records (`permission`, etc.) normalize to the one object form.
- **E** — `channel/serializer/json/writer.cs:152`: delete the `case List<app.data.@this>` property-bag arm; `dict` serializes via its own renderer.
- **B+J** — `data/this.cs:281` (`Materialize`) / `type/this.cs:257` (`Convert`): the json branch that deserializes an object currently builds a raw `Dictionary<string,object?>`. Repoint it to build a `dict`. The lazy seam is already correct — only its product changes.

## Acceptance

- `set %u% = {name:"a", age:30}` → `%u.name%` = "a", `%u.age%` = 30.
- a C# record (`permission`) round-trips as `{}`.
- nested `%person.address.city%`.
- `dict` with a `count` key returns the key, not the length; with no `count` key, `%d.count%` returns the size.

## Green

Both suites pass. Arrays still `List<object?>` → `[]` (unchanged this stage). Confirm the writer still emits arrays correctly via the `IEnumerable` arm (`writer.cs:165`).
