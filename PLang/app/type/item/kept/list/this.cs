namespace app.type.item.kept.list;

/// <summary>
/// The members a thing keeps beyond its own — added by a program (<c>set %!app.home% = …</c>), kept in place for as
/// long as the thing lives, each the Data it was given (its name, type and properties travel with it). A thing that
/// lives on (the app, a call, an actor, a module, a goal, a step) holds one; a plain value keeps none. The app's rides
/// its snapshot.
/// </summary>
public sealed class @this : global::app.snapshot.ISnapshot
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, global::app.data.@this> _kept =
        new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>The member kept as <paramref name="name"/>; null when none is.</summary>
    public global::app.data.@this? this[string name] => _kept.TryGetValue(name, out var kept) ? kept : null;

    /// <summary>Keeps <paramref name="value"/> as <paramref name="name"/>, over what was kept by that name.</summary>
    internal void Add(string name, global::app.data.@this value) => _kept[name] = value;

    /// <summary>Its section in a snapshot.</summary>
    public string Section => "Kept";

    /// <summary>Writes the kept members, by name, each as the Data it is.</summary>
    public void Capture(global::app.snapshot.@this s)
        => s.Write("kept", new System.Collections.Generic.Dictionary<string, global::app.data.@this>(_kept, System.StringComparer.OrdinalIgnoreCase));

    /// <summary>Puts back the kept members the section holds — a section without them leaves what is kept as it is.</summary>
    public async System.Threading.Tasks.Task Restore(global::app.snapshot.@this s, global::app.actor.context.@this context)
    {
        var entry = s.Entries.Get("kept", context);
        if (entry == null) return;
        var kept = await entry.Value<global::app.type.item.dict.@this>();
        _kept.Clear();
        foreach (var member in kept.Entries(context)) _kept[member.Name] = member;
    }
}
