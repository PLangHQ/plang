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
public sealed class @this : global::app.type.item.note.IAbout
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

        // A closed-set default — an enum member or a name — is born as its choice through the door a written value
        // takes (choice.Create), so it names itself (`Promote`, keccak256 the kind), never its number or a bare string.
        // Without [Default], a type with a default of its own (IDefault<T>) gives it.
        var declaredDefault = prop.GetCustomAttribute<global::app.module.DefaultAttribute>()?.Value;
        var ownDefault = value.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.IDefault<>))
            ? value.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            : null;
        Default = declaredDefault != null && value.IsGenericType
                  && value.GetGenericTypeDefinition() == typeof(global::app.type.item.choice.@this<>)
            ? value.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, new[] { typeof(object) })!.Invoke(null, new[] { declaredDefault })
            : declaredDefault ?? ownDefault;
        IsInput = prop.GetCustomAttribute<global::app.Attributes.InputAttribute>() != null;
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

    /// <summary>A type's member a step calls with arguments (<c>%x.replace("a", "b")%</c>, <c>%x.toupper()%</c>): what
    /// it is called with, each a name and a plang type, and <see cref="Type"/> what it answers. Null for a member read
    /// as it is (<c>%p.relative%</c>, <c>%s.length%</c>) — a property, or a method that asks only for its asker.</summary>
    public System.Collections.Generic.IReadOnlyList<@this>? Arguments { get; init; }

    /// <summary>Whether a step calls this member with arguments, rather than reading it.</summary>
    public bool IsMethod => Arguments != null;

    /// <summary>The type this is a member of, when it is one (<c>text</c> for <c>replace</c>) — whose teaching folder
    /// holds its notes.</summary>
    public string? Owner { get; init; }

    /// <summary>What the builder is taught about this member — <c>/system/type/&lt;owner&gt;/&lt;member&gt;.notes.md</c>, its
    /// first line the member's one-liner; falsy when it has none, or when this is no type's member.</summary>
    [LlmBuilder]
    public global::app.type.item.file.@this? Notes(global::app.actor.context.@this context)
        => Owner == null ? null
            : new(global::app.type.item.path.@this.Resolve($"/system/type/{Owner}/{Name}.notes.md", context), context);

    /// <summary>This member's notes, read line by line (<see cref="Notes"/>, parsed): a line for the member itself (its
    /// one-liner and how a step says it), one per argument, and <c>Returns</c>; warnings for a line naming none of
    /// them, or an argument no line names. Null when this is no type's member.</summary>
    [LlmBuilder]
    public global::app.type.item.note.@this? Note(global::app.actor.context.@this context)
        => Notes(context) is { } file ? new(this, file) : null;

    // What a member's notes are about: the member — its arguments the parts each line names, its own name a line too
    string global::app.type.item.note.IAbout.Noted => $"{Owner}.{Name}";
    string global::app.type.item.note.IAbout.Part => "argument";
    System.Collections.Generic.IEnumerable<string> global::app.type.item.note.IAbout.Parts
        => Arguments?.Select(argument => argument.Name) ?? [];
    bool global::app.type.item.note.IAbout.Names(string name)
        => string.Equals(name, Name, System.StringComparison.OrdinalIgnoreCase)
           || (Arguments?.Any(argument => string.Equals(argument.Name, name, System.StringComparison.OrdinalIgnoreCase)) ?? false);

    /// <summary>The class's <c>[Default]</c> value, or null when the property is required / has no default.</summary>
    public object? Default { get; init; }

    /// <summary>The class declares a default. Asked of the property, not of <see cref="Default"/>: a
    /// template cannot tell a <c>false</c> default from none.</summary>
    public bool HasDefault => Default != null;

    /// <summary>The action reads this property's value and answers a new value of it, changing nothing (its class
    /// marks it <c>[Input]</c>): a step that names no destination writes the answer back here.</summary>
    public bool IsInput { get; init; }

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

    /// <summary>The value's own properties, as the .pr carries them.</summary>
    public global::app.type.property.list.@this Property { get; init; } = new();

    /// <summary>This property holding <paramref name="value"/> instead — everything else it is, kept.</summary>
    public @this Holding(global::app.type.item.@this value) => new()
        { Name = Name, Type = Type, Nullable = Nullable, Default = Default, Value = value, Property = Property };

    /// <summary>Writes the property's row — <c>{name, type, value, properties?}</c>, the value as held
    /// (a wire relays its raw verbatim).</summary>
    public async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        if (writer is global::app.goal.step.action.formal.Writer formal)
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
            Type.Write(writer);   // the slot is the type's identity in every view
        }
        writer.Name("value");
        // A template is program text: a diagnostic shows it as written, never run against live
        // variables. Any other value writes in the asked view (a [Sensitive] member stays masked).
        var asWritten = mode == global::app.View.Debug && Value is { HasVariable: true } ? global::app.View.Store : mode;
        await (Value ?? global::app.type.item.@null.@this.Instance).Output(writer, asWritten, context);
        // A stored row names the variables its value holds, parsed once at build.
        if (mode == global::app.View.Store && Value is { HasVariable: true } held)
        {
            writer.Name("variable");
            new global::app.type.item.variable.serializer.Entry().Write(writer, held.Variable);
        }
        if (Property.Count > 0)
        {
            writer.Name("properties");
            writer.BeginObject();
            foreach (var property in Property)
            {
                writer.Name(property.Name);
                await (property.Value ?? global::app.type.item.@null.@this.Instance).Output(writer, mode, context);
            }
            writer.EndObject();
        }
        writer.EndObject();
    }

    /// <summary>The property's formal row — <c>Name: type = value</c>, or <c>Name: type ?= value</c> when
    /// it is a frozen default (an action's <c>Default</c> row). The type is written whole, its kind in
    /// angle brackets; the value writes itself.</summary>
    public async System.Threading.Tasks.ValueTask Row(global::app.goal.step.action.formal.Writer writer,
        bool frozen, global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.Row(Name, Type.kind.IsEmpty ? Type.Name : $"{Type.Name}<{Type.kind.Name}>", frozen);
        await (Value ?? global::app.type.item.@null.@this.Instance).Output(writer, mode, context);
    }

    /// <summary>The run's own Data over this property — born with the run's context, its properties its own copy.</summary>
    public global::app.data.@this Data(global::app.actor.context.@this context)
        => new(Name, Value ?? global::app.type.item.@null.@this.Instance, context: context) { Property = Property.Clone() };
}
