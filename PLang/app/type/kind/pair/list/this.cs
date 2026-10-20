namespace app.type.kind.pair.list;

/// <summary>
/// An output kind's pairs — what a value of another type looks like in this kind, when it isn't the value's own write
/// (a list in formal is a list of arguments; a type in plang is its identity). Each pair lives in its input type's folder,
/// <c>&lt;type folder&gt;/format/&lt;output kind&gt;.cs</c>, a pair for that folder's class and every class under it; found when this
/// kind is first asked; an assembly loaded later adds its own (<see cref="Add"/>). The pair for a value is its own
/// class's, else its nearest base's: the action list's wins over list's, and a clause is written as the action it is.
/// </summary>
public sealed class @this
{
    // the output kind these pairs are written in
    private readonly global::app.type.kind.@this _kind;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, global::app.type.format.IOutput> _pairs = new();
    private volatile bool _found;

    /// <summary>The pairs written in <paramref name="kind"/>.</summary>
    public @this(global::app.type.kind.@this kind) => _kind = kind;

    /// <summary>A pair for values of <paramref name="clr"/> (and every class under it) in this kind; a pair for the same
    /// class replaces it.</summary>
    public void Add(System.Type clr, global::app.type.format.IOutput pair) => _pairs[clr] = pair;

    /// <summary>
    /// The one door a value is written through in this kind: the pair for its class (or its nearest base) when this kind
    /// has one, else the value writes itself. A value writing a member writes it through here, never
    /// <c>member.Output(writer, …)</c> directly — that skips the member's pair.
    /// </summary>
    public System.Threading.Tasks.ValueTask Output(global::app.type.item.@this value, global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        if (context != null) Find(context.App.type.list);
        for (var clr = value.GetType(); clr != null && clr != typeof(global::app.type.item.@this); clr = clr.BaseType)
            if (_pairs.TryGetValue(clr, out var pair)) return pair.Output(value, writer, mode, context);
        return value.Output(writer, mode, context);
    }

    // The pairs the app's assemblies ship for this kind, found once: each IOutput class named for this kind in a
    // `format` folder (the code's facts list them, read once for every app), a pair for the class whose folder holds it.
    private void Find(global::app.type.list.@this types)
    {
        if (_found) return;
        lock (_pairs)
        {
            if (_found) return;
            foreach (var assembly in types.Assemblies)
                foreach (var pair in (assembly == types.Code.Clr ? types.Code : new global::app.assembly.@this(assembly)).pair)
                    if (string.Equals(pair.Kind, _kind.Name, System.StringComparison.OrdinalIgnoreCase)
                        && ReferenceEquals(types.Format(pair.Kind), _kind))
                        _pairs.TryAdd(pair.Class, pair.Output);
            _found = true;
        }
    }
}
