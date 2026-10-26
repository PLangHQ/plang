# The type system

For someone changing the runtime. The question this page answers: what is a value, what is a type, and who writes a value out?

## An item is a value

Every plang value is an `item` (`PLang/app/type/item/this.cs:29`, `[PlangType("item")]`): `text`, `number`, `bool`, `list`, `dict`, `path`, `datetime`, `duration` and the others under `PLang/app/type/item/`. `item` is the top of the lattice, like C# `object` (`item/this.cs:6-13`): a `Data<T>` slot can say `where T : item`, so a raw CLR value never rides a value slot. An item owns behaviour, never a blob field (`:14-20`).

`item.Create(raw, context)` (`:41`) makes the item for any raw value, or hands back the item it already is. A bare `Data` is not a value; storing one throws (`:45-48`).

Ordering and equality belong to the value: every value compares through `Compare`, and the higher-ranked side's `Order` answers (`:22-26`).

## A type is a value too

A type is `app.type.@this` (`PLang/app/type/this.cs:32`). It carries `{Name, Kind, Strict}` and the facts of the type (`:9-19`). `Name` is the family ("text", "number", "image"). `Kind` is the optional refinement ("md", "gif", "int"). `Strict` turns the kind into a requirement. A program reaches a type as `%!app.type["text"]%` (`type/this.cs:21-23`).

A type is itself an `item` (`type/this.cs:27-31`): it can be held in a variable and written in a `.goal`. It writes its identity as `{name, kind?, strict?, template?}` (`:38-51`).

The app's types are in `app.type.list`, a layer over what the app is made from, so `text` is one object in every app made from the same code (`Documentation/v0.2/apps.md`, "An app is made from an origin").

## A kind

A kind refines a type. A file format is a kind of the type that reads it: json and jsonl are kinds under `PLang/app/type/item/kind/`. A kind that only changes how its value is written or parsed is a subclass of its type that writes and parses itself: a duration's `short` and `iso` are `PLang/app/type/item/duration/short/this.cs` and `iso/this.cs`, a size's `iec` is `type/item/size/iec/this.cs` (`CLAUDE.md`, "A value always writes itself").

## A value writes itself

Every write is `value.Output(writer)`. `Output` is not virtual (`item/this.cs:848`): it hands the value to `writer.Write(this)`, and the writer picks the value's overload for that writer's own class. A value overrides the write for its own look:

- `Write(IWriter)`, its own write in any format with no look of its own: `type/this.cs:54` writes a type's identity.
- `Write(text.Writer)` where it looks different in text: `type/this.cs:63` writes a type as its word (`text`, `list<path>`).
- `Write(formal.Writer)` where it looks different in formal.

So the code for a look is in the value's class. A new domain type ships by tagging the members that should show: `[Out]` (the wire), `[Store]` (kept), `[Debug]` (a dump). The base `Write` writes the tagged members (`app/this.cs:28-33` documents it for the app). Do not add a `JsonConverter` for a domain type.

The view decides what shows (`PLang/app/View.cs:7-14`): **Store** is what plang keeps, the `[Store]` members, nothing masked; **Out** is the wire, the `[Out]` members, with `[Sensitive]` members left out; **Debug** is a dump of every member, sensitive ones masked as `****`. A type that tags nothing shows every public member in Store and Out.

## Data is the wire shape

`data.@this` is the carrier (`PLang/app/data/this.cs`): `{name, type, value, properties, signature}`. It is written as itself, filtered by `[Out]`; do not build a parallel wrapper type for its serialization (`CLAUDE.md`, "Data is not enveloped"; the gate is review, see the `envelope` smell in `Documentation/v0.2/obp-smells.md`).

## Truthiness belongs to the value

A value's yes or no is its own: `item.IsTruthy()` is virtual (`item/this.cs:653`), and `Data.ToBoolean()` asks it (`data/this.cs:696`). A type that knows its own truth implements `IBooleanResolvable` (`PLang/app/data/IBooleanResolvable.cs`); `item` implements it (`item/this.cs:29`) and a `path` answers it asynchronously, where truth means "does it exist" (`type/item/path/this.Operations.cs:189-197`). Do not add type-specific cases to `Data.ToBoolean`.

## Where to read more

`Documentation/v0.2/type-system.md` and `Documentation/v0.2/object_pattern_formal.md` are the design notes. They are older than this page and the code is newer than they are.
