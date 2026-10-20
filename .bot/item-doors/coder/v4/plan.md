# coder — item-doors — v4 plan: stage 5 (#4), a type's members through its own list

Rulings: 138, 139 (architect log). Steps (1) marks and (2) probe are done (b233e0b66). This is step (3), the switch.

## Shape
- `property` keeps its member's door, captured where it is built from a class (the PropertyInfo, or a method that
  asks only its asker). Reflection lives only there.
  - `Read(owner, parent)` — the member's answer, made into its child Data the way the reflection kind does today
    (`kind.Data(parent.Name, node, parent, ctx)`; an awaited task's result).
  - `Write(owner, value, context)` → Data — the value read as the member's plang type through `Reader.Reader`
    (133's door), then set; the C# lowering only when the member's C# type is not a plang one, after the read; a
    member that can't be written answers why (`OwnMember`).
  - `IsDefault(value, context)` async — the value compared with the default made as the member's type.
- `item.Get`: `Type.Property[key]` (a member, not a method with arguments) → `on` → `setting` → `Kept` → NotFound.
- `item.Set` answers a Data: a member writes itself; else a living thing keeps it (Kept); else a typed answer that a
  plain value can't hold an added member yet (NotFound status, so a variable's own binding keeps it in its
  Properties, as `data.Set` does today) — marked with Ingi's open question.
- `item.Member` goes (list/dict's count/first read through `Type.Property`).
- Overrides of `Set` (dict, list, clr, step, settings, snapshot, node root) answer a Data; `data.Set` takes it.

## Reds first
- identity's PrivateKey is not navigable (flip `DotNavigation_PrivateKey_ReturnsPrivateKey`).
- a refused write answers its error (a read-only member through `item.Set`).
- a dict into a record member through `property.Write` lands the record (ItemReaderTests, on a member a type lists).

## Out of scope, logged
- `type.list.Property(...)` builds a bare `new type(...)` (stage 1 said no bare type) — obp-cleanup.
- PascalCase members plang navigates — the architect raises with Ingi.
