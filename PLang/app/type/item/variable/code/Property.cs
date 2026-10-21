namespace app.type.item.variable.code;

/// <summary>
/// A member: <c>.address</c>, <c>."key.with.dots"</c> — the value navigates itself by name. A name
/// starting with <c>!</c> (<c>!cost</c>, <c>!type</c>, <c>!path</c>) reads the binding instead of
/// the value's content: its Properties, then Data's own members, then the value's members, then the
/// value's members that need the asker's context — whether <c>cost</c> is a Property is only known
/// at run, so that order is this step's own lookup.
/// </summary>
public sealed class Property : Hop
{
    /// <summary>The member's name; a <c>!</c> first reads the binding.</summary>
    public string Name { get; }

    internal Property(string text, string name, Hop? next, global::app.type.@this type) : base(text, next, type) => Name = name;

    public override string Kind => "property";

    protected override System.Threading.Tasks.ValueTask Piece(global::app.type.format.IWriter writer) { writer.String(Name); return default; }

    internal bool IsBinding => Name.StartsWith('!');

    /// <summary>A member is the value's own content; a binding (<c>%x!context%</c>) reaches past the value into
    /// what holds it.</summary>
    internal override bool IsOwn => !IsBinding;

    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Start(
        global::app.data.@this? previous, global::app.actor.context.@this context)
    {
        if (previous is null) return context.NotFound(Name);
        if (!IsBinding)
        {
            var read = await previous.Peek().Get(previous, Name);
            // a member a program added to a variable's own value is kept beside it in its binding — read after the
            // value's own, there and wherever the binding's copy went (a list's row); never a `!` property: those are
            // the variable's, read with !
            if ((!read.Success || !read.IsInitialized) && previous.Kept[Name] is { } property)
                return new global::app.data.@this(Name, await property.Value(), parent: previous);
            return read;
        }

        var key = Name[1..];
        if (previous.Properties.ContainsKey(key))
            return new global::app.data.@this(key, await previous.Properties.Value(key), parent: previous);

        // Data's own members (Name, Type, Error, Success, …), then a subclass's.
        var member = typeof(global::app.data.@this).GetProperty(key, Public)
                     ?? previous.GetType().GetProperty(key, Public);
        if (member != null)
            return new global::app.data.@this(key, member.GetValue(previous), parent: previous);

        // A fact about the thing the value is — a reference's own (a file's !path, !size, !kind; a url's !host), never
        // its content's. A plain value has none: its members are read with a dot.
        if (await previous.Peek().Fact(key, previous) is { } fact) return fact;
        // then a fact of what the value came from — a read file the use narrowed to the dict it parses to
        if (await previous.Peek().history.Fact(key, previous) is { } origin) return origin;

        return previous.Context?.NotFound(key) ?? context.NotFound(key);
    }

    /// <summary>A member's slot is the parent's child by that name, kept by the parent for the write; a <c>!</c> name's is
    /// what its read reaches.</summary>
    internal override async System.Threading.Tasks.ValueTask<global::app.data.@this> Slot(
        global::app.data.@this parent, global::app.actor.context.@this context)
        => IsBinding ? await Start(parent, context) : await parent.Peek().Child(parent, Name, isIndex: false);

    /// <summary>A <c>!</c> name written straight on the root (it is the last step) writes on the variable as it is — it
    /// must exist; any other write goes into what the root holds, born when it holds nothing.</summary>
    internal override System.Threading.Tasks.ValueTask<global::app.data.@this> Binding(
        Variable root, global::app.actor.context.@this context)
        => IsBinding && Next is null ? root.Start(null, context) : root.Ensure(context);

    /// <summary>A member takes the value as the parent's child; a <c>!</c> name lands in the
    /// binding's Properties.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Set(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context)
        => Set(parent, value, context, binding: false);

    /// <summary>A member written on a variable's binding (<paramref name="binding"/>) that its value can't take is kept
    /// in the binding (<c>set %name.lang% = "is"</c>).</summary>
    internal override async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context, bool binding)
    {
        if (parent is null) return context.NotFound(Name);
        if (!IsBinding) return await parent.Set(Name, isIndex: false, value, keeps: binding);
        if (!parent.IsInitialized)
            return context.Error(new global::app.error.@this($"Variable '{parent.Name}' is not set", "VariableNotFound", 400));

        try
        {
            parent.Properties[Name[1..]] = value is global::app.data.@this data ? await data.Value() : value;
        }
        catch (System.ArgumentException ex)
        {
            return context.Error(new global::app.error.@this(ex.Message, "InvalidPropertyValue", 400));
        }
        return parent;
    }

    private const System.Reflection.BindingFlags Public = System.Reflection.BindingFlags.Public
        | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase;
}
