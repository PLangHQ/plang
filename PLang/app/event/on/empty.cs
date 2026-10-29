using System.Collections.Frozen;

namespace app.@event.on;

/// <summary>
/// The moments of an item nothing is bound on: one per class, shared, closed — the core's, and the ones the class's plang
/// type declares (<c>[Events(…)]</c>). An event is added to an item's own moments (an action's) or an app's
/// (<c>app.event</c>), never here.
/// </summary>
public sealed class empty : @this
{
    // Each class's shared moments, its type's declared ones among them — a fact of the class, made once.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, empty> Emptied = new();

    private empty(FrozenSet<string> names) : base(null, "start", names, open: false) { }

    private empty(empty start, string name) : base(start, name, start.Declared, open: false) { }

    /// <summary>The shared moments of an item of <paramref name="class"/>: the core's, and the ones its plang type
    /// declares.</summary>
    internal static empty Of(System.Type @class)
        => Emptied.GetOrAdd(@class, static type => new(
            (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.Attributes.EventsAttribute>(type, inherit: false)?.Names ?? [])
                .ToFrozenSet(StringComparer.OrdinalIgnoreCase)));

    /// <summary>The moment named <paramref name="name"/>, shared and closed: made once for the class. Null when there is
    /// no moment of that name.</summary>
    public override @this? this[string name]
        => Core.Contains(name) || Declared.Contains(name) ? Add(name) : null;

    /// <summary>The moment named <paramref name="name"/>, made with sides closed to events.</summary>
    private protected override @this Moment(string name) => new empty((empty)this, name);
}
