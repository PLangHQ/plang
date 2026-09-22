# The graft is written, and it stamps — the format bridge cannot carry the parent

**Branch:** `goal-graph-singular` · **Author:** coder · **Uncommitted** — I am not landing a stamp.

You ruled: implement Addendum 2 §E (the step constructs its children), then door 2 has zero
customers and dies. I implemented it. It does not reach that state, and the reason is one seam.

## What is written

```csharp
// goal/step/this.Item.cs — the step takes its own children
public override async ValueTask<item.@this> Set(string key, bool isIndex, object? value)
{
    if (isIndex || !string.Equals(key, "action", StringComparison.OrdinalIgnoreCase))
        return await base.Set(key, isIndex, value);

    var binding = value as data.@this;
    var incoming = binding != null ? await binding.Value() : value as item.@this;
    var context = binding?.Context ?? throw new NotSupportedException(…);

    var node = new action.list.@this();
    foreach (var row in Rows(incoming, context))
        node.Add(action.@this.Create(row, context, this));      // hands itself over
    _action = node;
    return this;
}
```

`Rows` asks the incoming value to enumerate itself (`EnumerateItems`) — a plang list yields its
elements, a json array its own; each value owns how it is walked, so the step knows no format.

`ClrJsonActionsWriteTests` is reshaped as you asked and passes, including the new assertion
`Action[i].Step == step`.

## And here is the stamp

```csharp
// goal/step/action/this.Item.cs
internal static @this Create(item.@this row, context.@this context, step.@this step)
{
    if (row is @this built) return built;
    if (row.Read(new serializer.Reader(), null, context) is @this made) { made.Step = step; return made; }
    //                                                                    ^^^^^^^^^^^^^^^^^
    throw new NotSupportedException(…);
}
```

The test is green on a lie: the action is constructed **stepless** and stamped afterwards. The doc
comment I wrote says "born holding its step". It is not.

## Why — the bridge has no room for a parent

A value arriving as json reaches a reader through the kind's format bridge:

```csharp
// type/item/kind/json/this.cs:114 — the format→type read
public override object? Read(object obj, ITypeReader reader, string? kind, context.@this context)
{
    var utf8 = new Utf8JsonReader(Encoding.UTF8.GetBytes(((JsonElement)obj).GetRawText()));
    utf8.Read();
    var stream = new json.Reader(utf8);
    return reader.Read(ref stream, kind, new ReadContext(context, Verify: false));
}
```

It takes an `ITypeReader` and calls **door 2**. The construction door (`Read(ref reader, ctx, step)`)
is not on that interface and there is nowhere in the call to put a step. So every value→action
construction goes through the parentless door, and the only way to attach the parent afterwards is
the stamp.

Which also answers the door-2 question from the other side: **the graft does not remove door 2, it
depends on it.** Current references after the graft:

```
step/serializer/Reader.cs:89      the step's legacy door calling the action's legacy door
action/serializer/Reader.cs:127   the child-step fallback, reachable only from that legacy door
build/code/Default.cs:487         now the ICreate pass-through, as you predicted — no longer a customer
action/this.Item.cs Create        MINE, via the json kind's bridge — the new customer
```

## Three ways out, none obviously right

1. **The row hands over its own bytes; the step drives the construction door.**
   ```csharp
   var bytes = row.Raw();                       // a new seam on item: "your own wire form"
   var action = actionReader.Read(bytes, ctx, step);
   ```
   The value still owns its format (it answers with its own bytes); the step never parses. Door 2
   dies. Cost: a new member on `item`, and a second entry point on the action reader taking bytes.

2. **The bridge carries the parent** — `kind.Read(obj, reader, kind, context, parent)`. Every kind
   grows a parameter that only the action case uses, and `ITypeReader` grows a parent-aware door.
   This is the `Wrap(next, context, modifier)` mistake again, one layer down.

3. **`ReadContext` carries it.** Ingi rejected this explicitly ("ReadContext is bad"), and he was
   right: it reintroduces ambient coupling in the one place we just cleaned.

I lean 1. Asking a value for its own wire form is the same move as asking it to enumerate itself —
the value owns the answer, the caller owns the intent. But it adds a member to `item`, which is the
kind of surface change I am not making without you.

## The question behind it

Is there a reading where value→construction does not need the parent at all? The rows arriving here
are a compiled step's actions; they are program the moment they are written. If some other
mechanism made the write itself the construction — the way the .pr reader's shell-first walk does —
the bridge would never be asked to carry anything. I cannot see it, but I have been wrong about
this door twice now, so I would rather ask than build.

## Separately — an item cannot answer a question about itself

`item.@this` carries no `Context`; only `data.@this` does. So a step needing one takes it from
whatever write reached it (`binding?.Context`, exactly as the default `Set` does at
`type/item/this.cs:188`). A carrier has one (`clr.@this` does `context ?? Context`); a plain item
does not.

Ingi noticed it in review: a step is a plang value, the rule is values are born with context, and
this one is not. It is the same birth-fact question one level up, and it affects every item rather
than just `step`. Raising it rather than working around it — no proposal attached.
