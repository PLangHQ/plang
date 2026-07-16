using System.Reflection;
using System.Text.Json.Serialization;

namespace app.goal.steps.step.actions.action;

// The class-zoom face of the action host — the catalog view. A .pr action carries its steps;
// the same host at class zoom answers its declared parameter slots (the reflection leaf) for
// the builder catalog. Reflection happens ONCE here, cached on the element.
public sealed partial class @this
{
    /// <summary>Catalog-zoom context — stamped when the module element mints this action for the
    /// catalog; null on a .pr-loaded action (which navigates via the clr carrier, not this list).</summary>
    [JsonIgnore]
    internal global::app.actor.context.@this? Context { get; init; }

    private System.Collections.Generic.IReadOnlyList<property.@this>? _rows;

    /// <summary>The declared parameter slots as the NATIVE plang list. THE reflection site — once
    /// per element, cached. Filters exactly as the catalog: capability-interface props, [Code],
    /// EqualityContract are dropped; Data&lt;T&gt;/Nullable&lt;T&gt; unwrap to the type entity;
    /// Data&lt;variable&gt; → IsVariable; [Default] → Default; the IChannel synthetic "channel"
    /// row rides along.</summary>
    public global::app.type.item.list.@this Properties
        => new((_rows ??= Reflect()).Select(r => (object?)r).ToList(),
               Context ?? throw new System.InvalidOperationException(
                   "action.Properties needs the catalog context — stamp it at mint; a .pr-zoom action navigates via the clr carrier."));

    // The execution-context slots the source generator wires (not user-supplied params) — filtered
    // out of the catalog so the LLM never emits them.
    private static readonly System.Type[] CapabilityInterfaces =
    {
        typeof(global::app.module.IContext), typeof(global::app.module.IStep),
        typeof(global::app.module.IChannel), typeof(global::app.module.IEvent),
        typeof(global::app.module.IStatic),
    };

    private System.Collections.Generic.List<property.@this> Reflect()
    {
        var rows = new System.Collections.Generic.List<property.@this>();
        if (ParameterSchema == null) return rows;
        var type = Context!.App.Type;
        var nCtx = new NullabilityInfoContext();

        var capabilityProps = new System.Collections.Generic.HashSet<string>(
            CapabilityInterfaces.Where(i => i.IsAssignableFrom(ParameterSchema))
                .SelectMany(i => i.GetProperties().Select(p => p.Name)),
            System.StringComparer.OrdinalIgnoreCase);

        foreach (var prop in ParameterSchema.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.Name == "EqualityContract") continue;
            if (capabilityProps.Contains(prop.Name)) continue;
            if (prop.GetCustomAttribute<global::app.module.CodeAttribute>() != null) continue;

            bool isNullable = System.Nullable.GetUnderlyingType(prop.PropertyType) != null;
            if (!isNullable && !prop.PropertyType.IsValueType)
                isNullable = nCtx.Create(prop).WriteState == NullabilityState.Nullable;

            var value = UnwrapToValue(prop.PropertyType);
            rows.Add(new property.@this
            {
                Name = prop.Name,
                // bare Data is the polymorphic "object" slot; every other value type resolves its
                // entity through the identity door (compounds ride the kind axis).
                Type = value == typeof(global::app.data.@this) ? type["object"] : type[value],
                Nullable = isNullable,
                IsVariable = IsVariableNameSlot(prop.PropertyType),
                Default = prop.GetCustomAttribute<global::app.module.DefaultAttribute>()?.Value,
            });
        }

        // IChannel actions: source-gen resolves the Channel slot off a "channel" param — surface it
        // so the LLM can emit a name from the actor's inventory.
        if (typeof(global::app.module.IChannel).IsAssignableFrom(ParameterSchema))
            rows.Add(new property.@this { Name = "channel", Type = type["string"], Nullable = true });

        return rows;
    }

    // Data<T>/Nullable<T> unwrap to the value type; the door owns the rest (list.@this<T>,
    // choice<T>, scalars). Mirrors GetTypeName's Data/Nullable unwrapping.
    private static System.Type UnwrapToValue(System.Type t)
    {
        var n = System.Nullable.GetUnderlyingType(t);
        if (n != null) t = n;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(global::app.data.@this<>))
            t = t.GetGenericArguments()[0];
        return t;
    }

    private static bool IsVariableNameSlot(System.Type propType)
    {
        var underlying = System.Nullable.GetUnderlyingType(propType) ?? propType;
        if (!underlying.IsGenericType) return false;
        if (underlying.GetGenericTypeDefinition() != typeof(global::app.data.@this<>)) return false;
        return underlying.GetGenericArguments()[0] == typeof(global::app.variable.@this);
    }
}
