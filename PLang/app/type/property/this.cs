using System.Reflection;

namespace app.type.property;

/// <summary>
/// One property of a class — a named, typed slot, as C#'s <c>PropertyInfo</c> is. A type's
/// properties (<c>type.Property</c>: a record's fields, a scalar's navigable members) and an
/// action's properties (<c>action.Property</c> / <c>action.Default</c>) are both these. Each source
/// fills what it knows: a type's come from its class (Name, Type); a catalog action's from its
/// handler class (Name, the declared Type, Nullable, the [Default] rule); a program action's from
/// its .pr (Name, the Type the step gave, the raw Value as loaded, the Properties bag). The program
/// is shared by every run, so a property holds no Data and no context: a run makes its own Data from it.
/// </summary>
public sealed class @this
{
    /// <summary>Reflects a declared property off its <see cref="PropertyInfo"/>: Name, PLang
    /// type ENTITY (Data&lt;T&gt;/Nullable&lt;T&gt; unwrap to T; bare Data is the open
    /// <c>item</c> slot), nullability (Nullable&lt;T&gt; or a nullable reference), and the [Default]
    /// value. The property builds itself — the catalog loop only filters.</summary>
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

        var value = isDataGeneric ? bare.GetGenericArguments()[0] : bare;
        Type = value == typeof(global::app.data.@this) ? types["item"] : types[value];

        // A closed-set default is born as its choice, so it names itself (`Promote`), never its number.
        var declaredDefault = prop.GetCustomAttribute<global::app.module.DefaultAttribute>()?.Value;
        Default = declaredDefault is System.Enum && value.IsGenericType
                  && value.GetGenericTypeDefinition() == typeof(global::app.type.item.choice.@this<>)
            ? System.Activator.CreateInstance(value, declaredDefault)
            : declaredDefault;
    }

    /// <summary>A property built by hand — a type's field, the synthetic channel property, a .pr row, a builder's.</summary>
    public @this() { }

    /// <summary>The property name — "Path", "Encoding".</summary>
    public required string Name { get; init; }

    /// <summary>The property's PLang type entity. Consumers read <c>Type.Name</c> / its face,
    /// never a <c>System.Type</c> — a compound like <c>list&lt;path&gt;</c> is the list entity
    /// carrying <c>path</c> as its kind.</summary>
    public required global::app.type.@this Type { get; init; }

    /// <summary>The property accepts null (either a <c>Nullable&lt;T&gt;</c> or a nullable reference).</summary>
    public bool Nullable { get; init; }

    /// <summary>The class's <c>[Default]</c> value, or null when the property is required / has no default.</summary>
    public object? Default { get; init; }

    /// <summary>The class declares a default. Asked of the property, not of <see cref="Default"/>: a
    /// template cannot tell a <c>false</c> default from none.</summary>
    public bool HasDefault => Default != null;

    /// <summary>A step must write this property: it accepts no null and has no default to fall back on.</summary>
    public bool Required => !Nullable && !HasDefault;

    /// <summary>Is <paramref name="value"/> this property's default — a value the step could have left
    /// out (its default applies: the same behaviour)? A bool compares by its word, a number by its
    /// value, a variable by its name with or without its % signs, anything else by its text.</summary>
    public bool IsDefault(global::app.type.item.@this? value)
    {
        if (Default == null || value == null) return false;
        var written = value.ToString();
        // a value read from formal may still be its JSON slice: "A" is the text A
        if (written.Length >= 2 && written[0] == '"' && written[^1] == '"')
            written = System.Text.Json.JsonSerializer.Deserialize<string>(written) ?? written;
        var fallback = Default switch
        {
            bool b => b ? "true" : "false",
            System.IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => Default.ToString() ?? "",
        };
        if (Default is bool) return string.Equals(written, fallback, System.StringComparison.OrdinalIgnoreCase);
        if (Type.Name == "variable") return written.Trim('%') == fallback.Trim('%');
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        if (double.TryParse(written, System.Globalization.NumberStyles.Float, invariant, out var a)
            && double.TryParse(fallback, System.Globalization.NumberStyles.Float, invariant, out var d))
            return a == d;
        return written == fallback;
    }

    /// <summary>The value a program action holds, raw as loaded — a lazy wire/source, or the
    /// eagerly read action/goal.call. Never loaded here; the run's Data does that.</summary>
    public global::app.type.item.@this? Value { get; init; }

    /// <summary>The value's properties bag, as the .pr carries it.</summary>
    public global::app.data.Properties Properties { get; init; } = new();

    /// <summary>Writes the property's row — <c>{name, type, value, properties?}</c>, the value as held
    /// (a wire relays its raw verbatim).</summary>
    public async System.Threading.Tasks.ValueTask Output(global::app.channel.serializer.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        if (writer is global::app.channel.serializer.formal.Writer formal)
        {
            await Row(formal, frozen: false, mode, context);
            return;
        }
        writer.BeginObject();
        writer.Name("name");
        writer.String(Name);
        if (!Type.IsNull)
        {
            writer.Name("type");
            await Type.Output(writer, mode, context);
        }
        writer.Name("value");
        await (Value ?? global::app.type.item.@null.@this.Instance).Output(writer, mode, context);
        if (Properties.Count > 0)
        {
            writer.Name("properties");
            writer.BeginObject();
            foreach (var kvp in Properties)
            {
                writer.Name(kvp.Key);
                await global::app.type.item.@this.Create(kvp.Value, context).Output(writer, mode, context);
            }
            writer.EndObject();
        }
        writer.EndObject();
    }

    /// <summary>The property's formal row — <c>Name: type = value</c>, or <c>Name: type ?= value</c> when
    /// it is a frozen default (an action's <c>Default</c> row). The type is written whole, its kind in
    /// angle brackets; the value writes itself.</summary>
    public async System.Threading.Tasks.ValueTask Row(global::app.channel.serializer.formal.Writer writer,
        bool frozen, global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.Row(Name, Type.Kind is { } kind ? $"{Type.Name}<{kind.Name}>" : Type.Name, frozen);
        await (Value ?? global::app.type.item.@null.@this.Instance).Output(writer, mode, context);
    }

    /// <summary>The run's own Data over this property — born with the run's context, the bag its own copy.</summary>
    public global::app.data.@this Data(global::app.actor.context.@this context)
        => new(Name, Value ?? global::app.type.item.@null.@this.Instance, context: context) { Properties = Properties.Clone() };
}
