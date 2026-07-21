using System.Linq;
using System.Reflection;
using Property = global::app.goal.step.action.property.@this;

namespace app.goal.step.action.property.list;

/// <summary>
/// An action's declared parameter slots — the class-zoom catalog view, a first-class plang list of
/// <see cref="Property"/> rows. THE one reflection site: the collection reflects a handler's public
/// properties (Name / type ENTITY / nullability / default / the %var% marker) and drops the framework
/// slots the LLM must never author (<c>[Code]</c>, capability interfaces, <c>EqualityContract</c>,
/// host + graph-infra params). A plang <c>list</c> — NOT a naked CLR IReadOnlyList — so navigation
/// holds it directly (no clr carrier) and templates iterate it the one way. Read by build validation
/// (required / nullable / default checks) via its rows, and rendered by the catalog templates.
/// </summary>
public sealed class @this : global::app.type.item.list.@this
{
    // The execution-context slots the source generator wires (not user-supplied params) — filtered
    // out so the catalog never teaches them to the LLM.
    private static readonly System.Type[] CapabilityInterfaces =
    {
        typeof(global::app.module.IContext), typeof(global::app.module.IStep),
        typeof(global::app.module.IChannel), typeof(global::app.module.IEvent),
        typeof(global::app.module.IStatic),
    };

    /// <summary>Reflects a handler's declared parameter slots into property rows — the catalog filter
    /// lives HERE, the one crossing (a null handler → empty). The rows ride as this list's raw
    /// elements: a plang list of property hosts, navigable + renderable like any other.</summary>
    public @this(System.Type? handler, global::app.type.list.@this types, global::app.actor.context.@this context)
        : base(Reflect(handler, types), context) { }

    private static System.Collections.Generic.List<object?> Reflect(System.Type? handler, global::app.type.list.@this types)
    {
        var rows = new System.Collections.Generic.List<object?>();
        if (handler == null) return rows;

        var capabilityProps = new System.Collections.Generic.HashSet<string>(
            CapabilityInterfaces.Where(i => i.IsAssignableFrom(handler))
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

            rows.Add(row);
        }

        // IChannel actions: source-gen resolves the Channel slot off a "channel" param — surface it
        // so the LLM can emit a name from the actor's inventory.
        if (typeof(global::app.module.IChannel).IsAssignableFrom(handler))
            rows.Add(new Property { Name = "channel", Type = types["string"], Nullable = true });

        return rows;
    }

    /// <summary>The declared parameter rows, typed — build validation reads Name / Nullable / Default
    /// off these (each list row Peeks to the property host it wraps).</summary>
    public System.Collections.Generic.IReadOnlyList<Property> Rows
        => Items.Select(d => d.Clr<Property>()).OfType<Property>().ToList();
}
