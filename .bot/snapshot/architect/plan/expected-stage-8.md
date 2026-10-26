# Expected: stage 8, `kept` → `property`, saved with every value

What I expect the coder to write (Ingi's rule: shown to Ingi, not to the coder; compared when the coder reports). Decisions 66 and 67. Stage 8 goes before 7: it needs no answer still open, and once 7 makes the frame a tagged bag, the frame's added properties ride with it with no further code.

## The list: `type/item/kept/list/this.cs` → `type/item/property/list/this.cs`

```csharp
namespace app.type.item.property.list;

/// <summary>What a program added to a thing (<c>set %!app.home% = …</c>): each the Data it was given, under its name. A
/// thing that lives on (the app, an actor, a call, a module, a goal, a step) holds one; a plain value's ride on its Data.</summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>   // NEW: an item, so plang selects in it
{
    private readonly ConcurrentDictionary<string, data.@this> _property = new(StringComparer.OrdinalIgnoreCase);

    public @this(global::app.type.@this type) : base(type) { }                                  // NEW: born with its type

    public data.@this? this[string name] => …;              // as today
    internal void Add(string name, data.@this value) => …;  // as today
    internal int Count => …;                                // as today
    internal IEnumerable<KeyValuePair<string, data.@this>> Members => …;   // as today, ordered by name
    internal @this Copy() => …;                             // as today

    /// <summary><c>%!app.property["home"]%</c>: one by name; <c>%!app.property.list%</c>: all.</summary>
    public override ValueTask<data.@this> Get(data.@this parent, string key) => …;              // NEW: a node's own members, then one by name

    /// <summary>Writes itself as its members, each the Data it is, under its name; an empty list says nothing.</summary>
    protected internal override ValueTask Write(IWriter writer) => …;                            // NEW: replaces Dict(types)

    /// <summary>Made from the members it wrote, each read as the Data it is, in <paramref name="data"/>'s context.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, data.@this data) => …; // NEW: replaces Replace(dict, context)
}
```

- `Dict(types)` and `Replace(dict, context)` go: the list writes itself and is made from what it wrote.
- Its type is `Internal` (the decider is never offered it).

## Item's base and the keepers

```csharp
// type/item/this.cs — the fall-through, renamed; still internal, so it never shows in plang's member list
internal virtual global::app.type.item.property.list.@this? <hook> => null;     // :232, was Kept

// Set (:203) and Get (:222): Kept → <hook>; NoAddedMember's message says "property"
```

`<hook>`'s C# name is the coder's, cleared with me. It can't be `Property`: an action (`goal/step/action/this.cs:38`) and a type (`type/this.cs:660`) already have a public `Property`, their declared slots, which would hide it.

Each of the six keepers holds one list and shows it to plang as its `property` node:

```csharp
// app/this.cs:47–48, actor/this.cs:23–24, call/this.cs:23–24, goal/this.Item.cs:15–16, goal/step/this.Item.cs:10–11, module/this.cs:17–18
private global::app.type.item.property.list.@this _property;                      // born with its type
internal override global::app.type.item.property.list.@this <hook> => _property;
[Store, Debug] public global::app.type.item.property.list.@this property            // NEW, plang-visible (66)
{ get => _property; private set => _property = value; }
```

- `[Store]`: wherever a keeper is written by the general write in a view that keeps (Store, later Snapshot), its added properties go with it, and the general read reads them back. An empty list is left out (`type/item/this.cs:901`), so a goal's `.pr` is unchanged.
- `[Out]` is not on it: what a program adds to the app doesn't cross the wire.

## A Data's value

```csharp
// data/attachment.cs:21–30, data/this.cs:223–226
public global::app.type.item.property.list.@this Property { get …; set …; }    // was Kept
public bool HasProperty => _property is { Count: > 0 };                         // was HasKept

// type/item/wire/kind/plang/writer.cs:74–85 — the member written "property" (was "kept"); data/reader/this.cs:43–136 reads it
// type/item/variable/code/Property.cs:37, data/this.Navigation.cs:61 — renamed
```

A Data's own `Properties` (read with `!`) is untouched. In the wire a Data now shows `properties` (the Data's) and `property` (its value's added ones). Ingi accepted that.

## The app's snapshot part

```csharp
// app/this.Snapshot.cs:41,48–52 — renamed only; it goes in stage 7, when the app is written by its tagged members
snapshot.Write("property", property);
```

## Tests (red first where it's new)

- Each of the six keepers: `set %!x.y% = 1`, then `%!x.property["y"]%` is 1, `%!x.y%` is 1, and `%!x.property.list%` lists it. A read per keeper also pins that no keeper has two members differing by case (AmbiguousMember).
- A value's added property through the plang wire: written as `property`, read back.
- An actor's added property through a snapshot's text comes back. Red today: nothing saves an actor's.
- A goal's `.pr` is unchanged (the page and pick goldens unmoved).
- The surface golden is re-pinned citing "snapshot 66, 67".

## Not in this stage

- A call frame's added properties are saved once stage 7 makes the frame a tagged bag; no code here.
- Goal, step and module properties come back only after 4b (Ingi, later).
