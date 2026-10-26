namespace app.type.item.kept.list;

/// <summary>
/// The members a thing keeps beyond its own — added by a program (<c>set %!app.home% = …</c>), kept in place for as
/// long as the thing lives, each the Data it was given (its name, type and properties travel with it). A thing that
/// lives on (the app, a call, an actor, a module, a goal, a step) holds one; a plain value keeps none. Its owner writes
/// it into the owner's snapshot (the app's rides the app's).
/// </summary>
public sealed class @this
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, global::app.data.@this> _kept =
        new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>The member kept as <paramref name="name"/>; null when none is.</summary>
    public global::app.data.@this? this[string name] => _kept.TryGetValue(name, out var member) ? member : null;

    /// <summary>Keeps <paramref name="value"/> as <paramref name="name"/>, over what was kept by that name.</summary>
    internal void Add(string name, global::app.data.@this value) => _kept[name] = value;

    /// <summary>How many members are kept.</summary>
    internal int Count => _kept.Count;

    /// <summary>The kept members, each the Data it was given, under its name — by name, as its keys are case-blind, so
    /// the same members answer in the same order however they were added (a Data's digest covers them).</summary>
    internal System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, global::app.data.@this>> Members
        => _kept.OrderBy(member => member.Key, System.StringComparer.OrdinalIgnoreCase);

    /// <summary>A list keeping the same members — the list is copied, the members shared, as a variable's copy keeps
    /// what its value was given.</summary>
    internal @this Copy()
    {
        var copy = new @this();
        foreach (var (name, member) in _kept) copy._kept[name] = member;
        return copy;
    }

    /// <summary>Keeps exactly the members <paramref name="members"/> holds, each the Data it is — what was kept before
    /// goes. Its owner's snapshot start puts back what the owner stored.</summary>
    internal void Replace(global::app.type.item.dict.@this members, global::app.actor.context.@this context)
    {
        _kept.Clear();
        foreach (var member in members.Entries(context)) _kept[member.Name] = member;
    }
}
