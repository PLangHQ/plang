using System.Reflection;

namespace app.goal.steps.step.actions.action.property;

/// <summary>
/// One declared parameter slot — the class-zoom row. A row is more than a type: the type ENTITY
/// (compound generics ride as the type's kind, e.g. list&lt;path&gt;), plus nullability, default,
/// and the %var% marker. A HOST (never authored, never created from values) read by templates
/// through its own members.
/// </summary>
public sealed class @this
{
    /// <summary>Reflects a declared parameter slot off its <see cref="PropertyInfo"/>: Name, PLang
    /// type ENTITY (Data&lt;T&gt;/Nullable&lt;T&gt; unwrap to T; bare Data is the polymorphic
    /// "object"), nullability (Nullable&lt;T&gt; or a nullable reference), the %var% marker
    /// (Data&lt;variable&gt;), and the [Default] value. The row builds itself — the catalog loop
    /// only filters.</summary>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public @this(PropertyInfo prop, global::app.type.list.@this types)
    {
        var propType = prop.PropertyType;
        Name = prop.Name;

        var bare = System.Nullable.GetUnderlyingType(propType) ?? propType;
        bool isDataGeneric = bare.IsGenericType
            && bare.GetGenericTypeDefinition() == typeof(global::app.data.@this<>);

        bool isNullable = System.Nullable.GetUnderlyingType(propType) != null;
        if (!isNullable && !propType.IsValueType)
            isNullable = new NullabilityInfoContext().Create(prop).WriteState == NullabilityState.Nullable;
        Nullable = isNullable;

        IsVariable = isDataGeneric && bare.GetGenericArguments()[0] == typeof(global::app.variable.@this);

        var value = isDataGeneric ? bare.GetGenericArguments()[0] : bare;
        Type = value == typeof(global::app.data.@this) ? types["object"] : types[value];

        Default = prop.GetCustomAttribute<global::app.module.DefaultAttribute>()?.Value;
    }

    /// <summary>The synthetic channel row and other hand-built slots. Reflected slots use the
    /// <see cref="PropertyInfo"/> ctor.</summary>
    public @this() { }

    /// <summary>The parameter name — "Path", "Encoding".</summary>
    public required string Name { get; init; }

    /// <summary>The parameter's PLang type entity. Consumers read <c>Type.Name</c> / its face,
    /// never a <c>System.Type</c> — a compound like <c>list&lt;path&gt;</c> is the list entity
    /// carrying <c>path</c> as its kind.</summary>
    public required global::app.type.@this Type { get; init; }

    /// <summary>The slot accepts null (either a <c>Nullable&lt;T&gt;</c> or a nullable reference).</summary>
    public bool Nullable { get; init; }

    /// <summary>The <c>[Default]</c> value, or null when the slot is required / has no default.</summary>
    public object? Default { get; init; }

    /// <summary>The slot NAMES a variable (<c>Data&lt;variable&gt;</c>) — it advertises as
    /// <c>%var%</c>, not its type, because what the variable resolves to is unconstrained.</summary>
    public bool IsVariable { get; init; }
}
