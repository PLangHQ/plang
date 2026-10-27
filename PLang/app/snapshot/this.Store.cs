namespace app.snapshot;

/// <summary>
/// The walk: a part stored at this node and each part it holds at a node of its own under this one, and started back the
/// same way. A part's parts are its public members whose declared type is an <see cref="ISnapshot"/>, depth first, in the
/// order they are declared; no other member is read, so no other getter runs. A node is named for the member that holds
/// it, so a part's place is its member path from where the walk began. A part's parts are walked in the context its own
/// answer carries — a part answers in the context it was handed, an actor's context in itself — both ways.
/// </summary>
public sealed partial class @this
{
    /// <summary>Stores <paramref name="part"/> and every part under it, from this node, each live part named in
    /// <paramref name="thrown"/> written as it stood at an error's throw (its variables and frames). A part reached a
    /// second time (an actor's app) stays where it was first stored, and a node that holds nothing is left out.</summary>
    internal data.@this Store(ISnapshot part, actor.context.@this context, params (ISnapshot Live, ISnapshot Thrown)[] thrown)
        => new walk(thrown).Store(this, part, context);

    /// <summary>Starts <paramref name="part"/> from this node, then every part under it from the node at its place — the
    /// order it was stored in, so a part starts after the parts declared before it (the providers before the actors). A
    /// live part with no node held nothing when it was stored, and stays as it is.</summary>
    internal System.Threading.Tasks.Task<data.@this> Start(ISnapshot part, actor.context.@this context)
        => new walk([]).Start(this, part, context);

    // One walk over the parts: the parts standing for live ones (an error's throw), and the parts it has reached, each
    // reached once.
    private sealed class walk
    {
        private readonly Dictionary<ISnapshot, ISnapshot> _thrown = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<ISnapshot> _reached = new(ReferenceEqualityComparer.Instance);

        internal walk((ISnapshot Live, ISnapshot Thrown)[] thrown)
        {
            foreach (var (live, standing) in thrown) _thrown[live] = standing;
        }

        internal data.@this Store(@this node, ISnapshot part, actor.context.@this context)
        {
            _reached.Add(part);
            var own = (_thrown.GetValueOrDefault(part) ?? part).Store(node, context);
            if (!own.Success) return own;
            foreach (var (name, child) in Parts(part))
            {
                if (_reached.Contains(child)) continue;
                var under = new @this(node.Context);
                var result = Store(under, child, own.Context);
                if (!result.Success) return result;
                if (under.Entries.IsTruthy()) node.Entries.Set(new global::app.data.@this(name, under, context: node.Context));
            }
            return context.Ok();
        }

        internal async System.Threading.Tasks.Task<data.@this> Start(@this node, ISnapshot part, actor.context.@this context)
        {
            _reached.Add(part);
            var own = await part.Start(node, context);
            if (!own.Success) return own;
            foreach (var (name, child) in Parts(part))
            {
                if (_reached.Contains(child) || node.Entries.Get(name, node.Context)?.Peek() is not @this under) continue;
                var result = await Start(under, child, own.Context);
                if (!result.Success) return result;
            }
            return context.Ok();
        }

        // The parts a part holds, by the member that holds each: a member is read only when its declared type takes part.
        private static IEnumerable<(string Name, ISnapshot Part)> Parts(ISnapshot part)
        {
            foreach (var property in part.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                if (typeof(ISnapshot).IsAssignableFrom(property.PropertyType) && property.GetIndexParameters().Length == 0
                    && property.GetValue(part) is ISnapshot child)
                    yield return (property.Name, child);
        }
    }
}
