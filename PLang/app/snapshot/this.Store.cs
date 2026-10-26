namespace app.snapshot;

/// <summary>
/// The walk: a part stored at this node and each part it holds at a node of its own under this one, and started back the
/// same way. A part's parts are its public members whose declared type is an <see cref="ISnapshot"/>, depth first, in the
/// order they are declared; no other member is read, so no other getter runs. A node is named for the member that holds
/// it, so a part's place is its member path from where the walk began.
/// </summary>
public sealed partial class @this
{
    /// <summary>Stores <paramref name="part"/> and every part under it, from this node.</summary>
    internal data.@this Store(ISnapshot part, actor.context.@this context)
        => Store(part, new Dictionary<ISnapshot, ISnapshot>(ReferenceEqualityComparer.Instance), context);

    /// <summary>Stores <paramref name="part"/> and every part under it, from this node, each live part in
    /// <paramref name="standIns"/> written as what stands for it (the error's throw-time variables and frames). A part
    /// reached a second time (an actor's app) stays where it was first stored, and a node that holds nothing is left
    /// out.</summary>
    internal data.@this Store(ISnapshot part, Dictionary<ISnapshot, ISnapshot> standIns, actor.context.@this context)
        => Store(part, standIns, new HashSet<ISnapshot>(ReferenceEqualityComparer.Instance), context);

    private data.@this Store(ISnapshot part, Dictionary<ISnapshot, ISnapshot> standIns, HashSet<ISnapshot> stored,
        actor.context.@this context)
    {
        stored.Add(part);
        var own = (standIns.GetValueOrDefault(part) ?? part).Store(this, context);
        if (!own.Success) return own;
        foreach (var (name, child) in Parts(part))
        {
            if (stored.Contains(child)) continue;
            var node = new @this(Context);
            var result = node.Store(child, standIns, stored, context);
            if (!result.Success) return result;
            if (node.Entries.IsTruthy()) Entries.Set(new global::app.data.@this(name, node, context: Context));
        }
        return context.Ok();
    }

    /// <summary>Starts <paramref name="part"/> from this node, then every part under it from the node at its place — the
    /// order it was stored in, so a part starts after the parts declared before it (the providers before the actors). A
    /// live part with no node held nothing when it was stored, and stays as it is.</summary>
    internal System.Threading.Tasks.Task<data.@this> Start(ISnapshot part, actor.context.@this context)
        => Start(part, new HashSet<ISnapshot>(ReferenceEqualityComparer.Instance), context);

    private async System.Threading.Tasks.Task<data.@this> Start(ISnapshot part, HashSet<ISnapshot> started,
        actor.context.@this context)
    {
        started.Add(part);
        var own = await part.Start(this, context);
        if (!own.Success) return own;
        foreach (var (name, child) in Parts(part))
        {
            if (started.Contains(child) || Entries.Get(name, Context)?.Peek() is not @this node) continue;
            var result = await node.Start(child, started, context);
            if (!result.Success) return result;
        }
        return context.Ok();
    }

    // The parts a part holds, by the member that holds each: a member is read only when its declared type takes part.
    private IEnumerable<(string Name, ISnapshot Part)> Parts(ISnapshot part)
    {
        foreach (var property in part.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            if (typeof(ISnapshot).IsAssignableFrom(property.PropertyType) && property.GetIndexParameters().Length == 0
                && property.GetValue(part) is ISnapshot child)
                yield return (property.Name, child);
    }
}
