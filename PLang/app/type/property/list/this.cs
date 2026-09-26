using System.Linq;
using System.Reflection;
using Property = global::app.type.property.@this;

namespace app.type.property.list;

/// <summary>
/// A class's properties. Three sources, chosen by constructor:
/// <list type="bullet">
///   <item>a PROGRAM list (<c>new()</c>) — what a .pr step set, or what the build froze as defaults;
///   filled by the .pr reader and the builder through <see cref="Add"/>;</item>
///   <item>a TYPE's list — its record fields / navigable members, filled by the type registry
///   through <see cref="Add"/>;</item>
///   <item>a CATALOG list (<c>new(module, actionName)</c>) — an action handler class's declared
///   properties, reflected on first read (the module registers its actions before App exists). The
///   reflection drops the framework slots the LLM must never author (<c>[Code]</c>, capability
///   interfaces, <c>EqualityContract</c>, host + graph-infra properties).</item>
/// </list>
/// </summary>
public sealed class @this : System.Collections.Generic.IReadOnlyList<Property>
{
    // The execution-context slots the source generator wires (not user-supplied properties) —
    // filtered out so the catalog never teaches them to the LLM.
    private readonly System.Type[] _capabilityInterfaces =
    {
        typeof(global::app.module.IContext), typeof(global::app.module.IStep),
        typeof(global::app.module.IChannel),
        typeof(global::app.module.IStatic),
        typeof(global::app.module.IAction),
    };

    private readonly System.Collections.Generic.List<Property> _rows = new();

    // A catalog list's source, until its first read reflects it.
    private global::app.module.@this? _module;
    private readonly string? _action;

    /// <summary>A program (or type) list — empty until its reader or builder adds to it.</summary>
    public @this() { }

    /// <summary>A catalog list — <paramref name="actionName"/>'s handler properties, reflected on first read.</summary>
    internal @this(global::app.module.@this module, string actionName)
    {
        _module = module;
        _action = actionName;
    }

    private System.Collections.Generic.List<Property> Rows
    {
        get
        {
            if (_module == null) return _rows;
            lock (_rows)
            {
                if (_module == null) return _rows;
                Reflect(_module.Handler(_action!), _module.App.Type);
                _module = null;
            }
            return _rows;
        }
    }

    private void Reflect(System.Type? handler, global::app.type.list.@this types)
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

            var property = new Property(prop, types);

            // A host property lowers to `clr` — naming one leaks the C# type, so it is never shown. Every
            // other property is the step's to write: an action held as a value (channel.set's Goal, a
            // callback), the goal build.fold works on (Goal=%goal%) — written in formal, typed by this row.
            if (property.Type.Name is "clr") continue;

            _rows.Add(property);
        }

        // IChannel actions: source-gen resolves the Channel slot off a "channel" property — surface
        // it so the LLM can emit a name from the actor's inventory.
        if (typeof(global::app.module.IChannel).IsAssignableFrom(handler))
            _rows.Add(new Property { Name = "channel", Type = types["string"], Nullable = true });
    }

    /// <summary>Adds a property to a program list.</summary>
    public void Add(Property property) => Rows.Add(property);

    /// <summary>Puts <paramref name="property"/> in place of the one with its name, or adds it — the
    /// builder finishing the program.</summary>
    public void Set(Property property)
    {
        var rows = Rows;
        var i = rows.FindIndex(p => string.Equals(p.Name, property.Name, System.StringComparison.OrdinalIgnoreCase));
        if (i >= 0) rows[i] = property; else rows.Add(property);
    }

    /// <summary>The property named <paramref name="name"/>, or null.</summary>
    public Property? this[string name]
        => Rows.FirstOrDefault(p => string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase));

    public Property this[int index] => Rows[index];
    public int Count => Rows.Count;
    public System.Collections.Generic.IEnumerator<Property> GetEnumerator() => Rows.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Writes the list as an array of property rows.</summary>
    public async System.Threading.Tasks.ValueTask Output(global::app.channel.serializer.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginArray(Count);
        foreach (var property in Rows) await property.Output(writer, mode, context);
        writer.EndArray();
    }
}
