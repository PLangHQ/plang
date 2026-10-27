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
        Member = prop;

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
        // takes (choice.Create), born holding this property's type, so it names itself (`Promote`, keccak256 the kind),
        // never its number or a bare string. Without [Default], a type with a default of its own (IDefault<T>) gives it.
        var attribute = prop.GetCustomAttribute<global::app.module.DefaultAttribute>()?.Value;
        var @interface = value.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.IDefault<>))
            ? value.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            : null;
        Default = attribute != null && value.IsGenericType
                  && value.GetGenericTypeDefinition() == typeof(global::app.type.item.choice.@this<>)
            ? value.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, new[] { typeof(object), typeof(global::app.type.@this) })!
                .Invoke(null, new[] { attribute, Type })
            : attribute ?? @interface;
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

    // The C# member this one is, when it was read off a class — the property, or a method that asks only its
    // asker. The one place a member is reflected: it reads and writes itself through it.
    internal System.Reflection.MemberInfo? Member { get; init; }

    /// <summary>This member's answer on <paramref name="owner"/>, as its child of <paramref name="parent"/>: what the
    /// member holds (a Data whole; what an asynchronous one completes with), made by the kind of what it is. A member
    /// with no C# member behind it answers nothing.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this> Read(global::app.type.item.@this owner,
        global::app.data.@this parent)
    {
        var context = parent.Context;
        object? answer;
        switch (Member)
        {
            case PropertyInfo property: answer = property.GetValue(owner); break;
            case MethodInfo method:
                answer = method.Invoke(owner, method.GetParameters()
                    .Select(p => p.ParameterType == typeof(global::app.actor.context.@this) ? context : p.DefaultValue).ToArray());
                break;
            default: return context.NotFound(Name);
        }
        // a member that answers asynchronously answers what its task completes with — a refusal as it is
        if (answer?.GetType().GetMethod("AsTask", System.Type.EmptyTypes) is { } task) answer = task.Invoke(answer, null);
        if (answer is System.Threading.Tasks.Task pending)
        {
            await pending;
            answer = pending.GetType().GetProperty("Result")?.GetValue(pending);
        }
        if (answer is global::app.data.@this data) return data;
        var kind = answer is null ? context.App.type.list["item"].kind["*"]! : context.App.type.list.Kind(answer.GetType());
        return kind.Data(parent.Name, answer, parent, context);
    }

    /// <summary>Writes <paramref name="value"/> as this member of <paramref name="owner"/>: read as this member's type by
    /// that type's reader (its own, else as a record its Create makes), then set. A member that can't be written, or a
    /// value that doesn't read as its type, answers why.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this> Write(global::app.type.item.@this owner,
        object? value, global::app.actor.context.@this context)
    {
        if (Member is not PropertyInfo { CanWrite: true } slot)
            return context.Error(new global::app.error.@this(
                $"{Name} is {owner.Type.Name}'s own — it can't be written", "OwnMember", 400));
        var item = value is global::app.data.@this data ? await data.Value() : value as global::app.type.item.@this;
        object? member = item ?? value;
        if (item != null && !slot.PropertyType.IsInstanceOfType(item))
        {
            try
            {
                var read = item.Read(context.App.type.list.Reader.Reader(Type.Name, null, context),
                    Type.kind.IsEmpty ? null : Type.kind.Name, context, global::app.View.Out);
                // a typed list member (list<action>) holds the rows the list reader read, as its own type
                if (read is global::app.type.item.list.@this rows && !slot.PropertyType.IsInstanceOfType(rows)
                    && typeof(global::app.type.item.list.@this).IsAssignableFrom(slot.PropertyType))
                    read = System.Activator.CreateInstance(slot.PropertyType, rows);
                // a member whose C# type no plang type is (a string) takes the plang value lowered — the C# boundary
                member = read is global::app.type.item.@this plang && !slot.PropertyType.IsInstanceOfType(plang)
                    ? plang.Clr(slot.PropertyType, context) : read;
            }
            catch (global::app.error.AppException refused) { return context.Error(refused.Error); }
        }
        slot.SetValue(owner, member);
        return context.Ok(owner);
    }

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
        => Notes(context) is { } file ? new(this, file, context.App.type.list[typeof(global::app.type.item.note.@this)]) : null;

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
    /// <summary>Whether <paramref name="value"/> is this member's default: the value asks itself, against the default
    /// made as this member's type. A member with no default has none to be.</summary>
    public async System.Threading.Tasks.ValueTask<bool> IsDefault(global::app.type.item.@this? value, global::app.actor.context.@this context)
    {
        // a value holding a variable is what the variable holds at run, never the default, whatever it holds now
        if (Default == null || value == null || value.HasVariable) return false;
        // the value as it is read — a slot still its written form (a wire) opens through its door first
        var data = new global::app.data.@this(Name, value, context: context);
        var opened = await data.Value();
        return data.Success && opened != null
               && await opened.Compare(Type.Make(Default, context), context) == global::app.data.Comparison.Equal;
    }

    /// <summary>The value a program action holds, raw as loaded — a lazy wire/source, or the
    /// eagerly read action/goal.call. Never loaded here; the run's Data does that.</summary>
    public global::app.type.item.@this? Value { get; init; }

    /// <summary>The value's properties bag, as the .pr carries it.</summary>
    public global::app.data.Properties Properties { get; init; } = new();

    /// <summary>This property holding <paramref name="value"/> instead — everything else it is, kept.</summary>
    public @this Holding(global::app.type.item.@this value) => new()
        { Name = Name, Type = Type, Nullable = Nullable, Default = Default, Value = value, Properties = Properties };

    /// <summary>Writes the property's row — <c>{name, type, value, properties?}</c>, the value as held
    /// (a wire relays its raw verbatim).</summary>
    public async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("name");
        writer.String(Name);
        if (!Type.IsNull)
        {
            // the slot is the type as the format writes one
            writer.Name("type");
            await Type.Output(writer);
        }
        writer.Name("value");
        // the value as the view writes it (one that holds variables kept as written — with the variables it names — used,
        // or both in a dump)
        if (Value is { } value)
        {
            await writer.View.Write(value, writer);
            await writer.View.Variables(value, writer);
        }
        else writer.Null();
        if (Properties.Count > 0)
        {
            writer.Name("properties");
            writer.BeginObject();
            foreach (var kvp in Properties)
            {
                writer.Name(kvp.Key);
                await global::app.type.item.@this.Create(kvp.Value, writer.Context).Output(writer);
            }
            writer.EndObject();
        }
        writer.EndObject();
    }

    /// <summary>The property's formal row — <c>Name: type = value</c>, or <c>Name: type ?= value</c> when
    /// it is a frozen default (an action's <c>Default</c> row). The type is written whole, its kind in
    /// angle brackets; the value writes itself.</summary>
    public async System.Threading.Tasks.ValueTask Row(global::app.goal.step.action.formal.Writer writer, bool frozen)
    {
        writer.Row(Name, Type, frozen);
        if (Value is { } value) await value.Output(writer);
        else writer.Null();
    }

    /// <summary>The run's own Data over this property — born with the run's context, the bag its own copy.</summary>
    public global::app.data.@this Data(global::app.actor.context.@this context)
        => new(Name, (object?)Value, context: context) { Properties = Properties.Clone() };
}
