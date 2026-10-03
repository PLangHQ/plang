# app-systems-property — demolition list

What this branch holds open on purpose, and must close before it merges. Each entry says what it is, why it is
still here, and what removes it.

## 1. `Property` hidden on the action, the type entity and `type.list` (Q2)

`item.@this` has `internal virtual type.property.list.@this? Property => null`, overridden by the things that keep
added members (app, call, actor, module, goal, step). Three members hide it:

- `goal/step/action/this.cs`: `public type.property.list.@this Property { get; init; }` (the action's parameters), CS0114
- `type/this.cs`: `public property.list.@this? Property { get; init; }` (the type entity's declared properties), CS0114
- `type/list/this.cs`: the `Property(string, Type, …)` factory method, CS0108

From item's own code (Set, Get) they read as null, so an action and a type entity refuse an added member, as they did
under `Kept`. **Closes when Ingi answers Q2:** one list for declared and added members (the hiding becomes overrides,
with the refusal on the item that takes no added member), or two lists (the item-level member is renamed so nothing
hides it). `type.list`'s factory is renamed either way.

## 2. `property.list.Set(string, object?)` takes raw CLR values

The list has three `Set` doors: a row (`Set(Property)`), a Data (`Set(Data, name?)`), and `Set(string, object?)`, which
lifts a raw CLR value (string, bool, number, date, guid, bytes, IEnumerable) into a plang item. That last one is the
"owners take values" smell, a domain API taking CLR primitives. It came over from the old `data.Properties` bag. Its
callers: OpenAi (25 sites), http's `code/Default.cs`, TypeSafe, `event/binding/action`, and the tests.

**Closes when the branch closes:** each C# caller lifts at its own boundary, and the list takes a row or an item.

## 3. Wire and `.pr` key `properties` (Q1)

A Data's property list is still written as the old object `{"properties": {name: value}}` (`data/this.Output.cs`,
`json/writer.cs` EndRecord), and read by `property.list.Read`. **Closes when Ingi answers Q1:** rows
`[{name, type, value}]` or the object as it is, and the key renamed `properties` → `property` in both, no shim.
