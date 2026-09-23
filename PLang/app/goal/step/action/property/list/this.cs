using System.Linq;
using System.Reflection;
using Property = global::app.goal.step.action.property.@this;

namespace app.goal.step.action.property.list;

/// <summary>
/// An action's declared parameter slots — the class-zoom catalog view, a HOST list of
/// <see cref="Property"/> rows (catalog entries, like the module element). THE one reflection site:
/// the list reflects a handler's public properties (Name / type ENTITY / nullability / default / the
/// %var% marker) and drops the framework slots the LLM must never author (<c>[Code]</c>, capability
/// interfaces, <c>EqualityContract</c>, host + graph-infra params). Read by build validation
/// (required / nullable / default checks) and rendered by the catalog templates.
/// </summary>
public sealed class @this : System.Collections.Generic.IReadOnlyList<Property>
{
    // The execution-context slots the source generator wires (not user-supplied params) — filtered
    // out so the catalog never teaches them to the LLM.
    private readonly System.Type[] _capabilityInterfaces =
    {
        typeof(global::app.module.IContext), typeof(global::app.module.IStep),
        typeof(global::app.module.IChannel),
        typeof(global::app.module.IStatic),
    };

    private readonly System.Collections.Generic.List<Property> _rows = new();

    /// <summary>Reflects a handler's declared parameter slots into property rows — the catalog filter
    /// lives HERE, the one crossing (a null handler → empty).</summary>
    internal @this(System.Type? handler, global::app.type.list.@this types)
    {
        if (handler == null) return;

        var capabilityProps = new System.Collections.Generic.HashSet<string>(
            _capabilityInterfaces.Where(i => i.IsAssignableFrom(handler))
                .SelectMany(i => i.GetProperties().Select(p => p.Name)),
            System.StringComparer.OrdinalIgnoreCase);

        foreach (var prop in handler.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.Name == "EqualityContract") continue;
            if (capabilityProps.Contains(prop.Name)) continue;
            if (prop.GetCustomAttribute<global::app.module.CodeAttribute>() != null) continue;

            var row = new Property(prop, types);

            // Slots the LLM must never author: host params lower to `clr` (naming one leaks the C#
            // type); the graph-infra items (goal/step/action/modifier) are STRUCTURE the compiler
            // injects, never LLM vocabulary. Drop both by the row's own type name.
            if (row.Type.Name is "clr" or "goal" or "step" or "action" or "modifier") continue;

            _rows.Add(row);
        }

        // IChannel actions: source-gen resolves the Channel slot off a "channel" param — surface it
        // so the LLM can emit a name from the actor's inventory.
        if (typeof(global::app.module.IChannel).IsAssignableFrom(handler))
            _rows.Add(new Property { Name = "channel", Type = types["string"], Nullable = true });
    }

    public Property this[int index] => _rows[index];
    public int Count => _rows.Count;
    public System.Collections.Generic.IEnumerator<Property> GetEnumerator() => _rows.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
