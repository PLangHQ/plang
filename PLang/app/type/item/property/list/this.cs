namespace app.type.item.property.list;

/// <summary>
/// What a program adds to a thing — its properties (<c>set %!app.home% = …</c>), each the Data it was given (its name,
/// type and properties travel with it), kept for as long as the thing lives. A thing that lives on (the app, an actor, a
/// call, a goal, a step, a module) holds one as its <c>property</c> node (<see cref="IOwner"/>); a variable's own value
/// holds one on its binding. Brackets select one by name (<c>%!app.property["home"]%</c>), a dot reads <c>list</c> and
/// then one by name; a dot on the thing itself falls through to it (<c>%!app.home%</c>). It writes each property whole
/// under its name, is made back from what it wrote, and takes part in a snapshot as the run's state.
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, global::app.snapshot.ISnapshot
{
    /// <summary>Plang keeps it for its things: no step names it as a type.</summary>
    public static bool Internal => true;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, global::app.data.@this> _property =
        new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>A thing's properties, none yet.</summary>
    public @this() { }

    /// <summary>A structure, navigated by name.</summary>
    public override bool IsLeaf => false;

    /// <summary>A collection: holding none, it says nothing where it is written.</summary>
    public override bool IsSequence => true;

    /// <summary>Whether a program added anything.</summary>
    public override bool IsTruthy() => !_property.IsEmpty;

    /// <summary>The property named <paramref name="name"/>; null when none is.</summary>
    public global::app.data.@this? this[string name] => _property.TryGetValue(name, out var property) ? property : null;

    /// <summary>Keeps <paramref name="value"/> as <paramref name="name"/>, over what was kept by that name.</summary>
    internal void Add(string name, global::app.data.@this value) => _property[name] = value;

    /// <summary>How many properties were added.</summary>
    internal int Count => _property.Count;

    /// <summary>The properties, each the Data it was given, under its name — by name, as names are case-blind, so the
    /// same properties answer in the same order however they were added (a Data's digest covers them).</summary>
    internal System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, global::app.data.@this>> Members
        => _property.OrderBy(member => member.Key, System.StringComparer.OrdinalIgnoreCase);

    /// <summary>A list holding the same properties — the list is copied, the properties shared, as a variable's copy keeps
    /// what its value was given.</summary>
    internal @this Copy()
    {
        var copy = new @this();
        foreach (var (name, property) in _property) copy._property[name] = property;
        return copy;
    }

    /// <summary>A copy holds the same properties: each the Data it was given, never a deep copy of its context.</summary>
    protected internal override global::app.type.item.@this Clone() => Copy();

    /// <summary>Brackets select one property by name; a dot reads <c>list</c>, then one by name.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key, bool isIndex)
        => isIndex ? Named(parent, key) : await Get(parent, key);

    /// <summary><c>list</c> is the properties as a list, each the Data it was given; any other name is one property.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => new(string.Equals(key, "list", System.StringComparison.OrdinalIgnoreCase)
            ? new global::app.data.@this(key, new global::app.type.item.list.@this(Members.Select(member => member.Value),
                parent.Context.App.type.list[typeof(global::app.type.item.list.@this)]), parent: parent)
            : Named(parent, key));

    // One property by name, or why there is none.
    private global::app.data.@this Named(global::app.data.@this parent, string key)
        => this[key] ?? parent.Context.NotFound(key);

    /// <summary>Each property under its name, the Data it is, in name order.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        foreach (var (name, property) in Members)
        {
            writer.Name(name);
            await property.Output(writer);
        }
        writer.EndObject();
    }

    /// <summary>The pure core: a list passes through.</summary>
    public static @this? Create(object? raw) => raw as @this;

    /// <summary>A list passes through; what it wrote (a dict of its properties) is made back into one, each property the
    /// Data it is; anything else declines.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this list) return list;
        if (raw is global::app.type.item.dict.@this written)
        {
            var made = new @this();
            foreach (var property in written.Entries(data.Context)) made._property[property.Name] = property;
            return made;
        }
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name} — a thing's properties are made from what they wrote",
            "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>The run's state: each property the Data it is, under its name, as variables are stored.</summary>
    global::app.data.@this global::app.snapshot.ISnapshot.Store(global::app.snapshot.@this snapshot, global::app.actor.context.@this context)
    {
        foreach (var (name, property) in Members) snapshot.Entries.Set(name, property.Clone());
        return context.Ok();
    }

    /// <summary>Back as they were saved: exactly the stored properties, each through the snapshot's door, in the part's
    /// context.</summary>
    System.Threading.Tasks.Task<global::app.data.@this> global::app.snapshot.ISnapshot.Start(global::app.snapshot.@this snapshot, global::app.actor.context.@this context)
    {
        _property.Clear();
        foreach (var property in snapshot.In(context)) _property[property.Name] = property;
        return System.Threading.Tasks.Task.FromResult(context.Ok());
    }
}
