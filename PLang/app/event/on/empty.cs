namespace app.@event.on;

/// <summary>
/// The events of an item nothing is bound on: one per class, shared, closed — the core's, and the ones the class's plang
/// type declares (<c>[Events(…)]</c>). A binding is added to an item's own events (an action's) or an app's
/// (<c>app.event</c>), never here.
/// </summary>
public sealed class empty : @this
{
    // Each class's shared events, its type's declared ones among them — a fact of the class, made once.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, empty> Emptied = new();

    private empty(IEnumerable<string> declared) : base(declared) { }

    /// <summary>The shared events of an item of <paramref name="class"/>: the core's, and the ones its plang type
    /// declares.</summary>
    internal static empty Of(System.Type @class)
        => Emptied.GetOrAdd(@class, static type => new(
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.Attributes.EventsAttribute>(type, inherit: false)?.Names ?? []));

    /// <summary>The event named <paramref name="name"/>, shared and closed: made once for the class.</summary>
    public override global::app.@event.@this? this[string name] => Add(name);

    /// <summary>The event named <paramref name="name"/>, made with lists closed to bindings.</summary>
    private protected override global::app.@event.@this Event(string name)
        => Made.TryGetValue(name, out var made) ? made(binding.list.before.None, binding.list.after.None)
            : new declared(name, binding.list.before.None, binding.list.after.None);
}
