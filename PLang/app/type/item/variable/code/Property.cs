namespace app.type.item.variable.code;

/// <summary>
/// A member: <c>.address</c>, <c>."key.with.dots"</c> — the value navigates itself by name. A name
/// starting with <c>!</c> (<c>!cost</c>, <c>!type</c>, <c>!path</c>) reads the binding instead of
/// the value's content: its Property, then Data's own members, then the value's members, then the
/// value's members that need the asker's context — whether <c>cost</c> is a Property is only known
/// at run, so that order is this step's own lookup.
/// </summary>
public sealed class Property : Hop
{
    /// <summary>The member's name; a <c>!</c> first reads the binding.</summary>
    public string Name { get; }

    internal Property(string text, string name) : base(text) => Name = name;

    public override string Kind => "property";

    protected override void Piece(global::app.type.format.IWriter writer) => writer.String(Name);

    internal bool IsBinding => Name.StartsWith('!');

    /// <summary>A member is the value's own content; a binding (<c>%x!context%</c>) reaches past the value into
    /// what holds it.</summary>
    internal override bool IsOwn => !IsBinding;

    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Start(
        global::app.data.@this? previous, global::app.actor.context.@this context)
        => Start(previous, context, own: false);

    internal override async System.Threading.Tasks.ValueTask<global::app.data.@this> Start(
        global::app.data.@this? previous, global::app.actor.context.@this context, bool own)
    {
        if (previous is null) return context.NotFound(Name);
        if (!IsBinding)
        {
            var read = await previous.Peek().Get(previous, Name);
            // a member a program added to a variable's own value is kept in its binding — read after the value's own
            if (own && (!read.Success || !read.IsInitialized) && previous.Property.Contains(Name))
                return new global::app.data.@this(Name, await previous.Property.Value(Name), parent: previous);
            return read;
        }

        var key = Name[1..];
        if (previous.Property.Contains(key))
            return new global::app.data.@this(key, await previous.Property.Value(key), parent: previous);

        // Data's own members (Name, Type, Error, Success, …), then a subclass's.
        var member = typeof(global::app.data.@this).GetProperty(key, Public)
                     ?? previous.GetType().GetProperty(key, Public);
        if (member != null)
            return new global::app.data.@this(key, member.GetValue(previous), parent: previous);

        // A fact about the thing the value is — a reference's own (a file's !path, !size, !kind; a url's !host), never
        // its content's. A plain value has none: its members are read with a dot.
        if (previous.Peek().Fact(key, previous) is { } fact) return fact;

        return previous.Context?.NotFound(key) ?? context.NotFound(key);
    }

    /// <summary>A member takes the value as the parent's child; a <c>!</c> name lands in the
    /// binding's Property.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Set(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context)
        => Set(parent, value, context, own: false);

    /// <summary>A member written on a variable's own binding (<paramref name="own"/>) that its value can't take is kept
    /// in the binding (<c>set %name.lang% = "is"</c>).</summary>
    internal override async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context, bool own)
    {
        if (parent is null) return context.NotFound(Name);
        if (!IsBinding) return own ? await parent.Keep(Name, value) : await parent.Set(Name, isIndex: false, value);
        if (!parent.IsInitialized)
            return context.Error(new global::app.error.Error($"Variable '{parent.Name}' is not set", "VariableNotFound", 400));

        try
        {
            parent.Property.Set(Name[1..], value is global::app.data.@this held ? await held.Value() : value);
        }
        catch (System.ArgumentException ex)
        {
            return context.Error(new global::app.error.Error(ex.Message, "InvalidPropertyValue", 400));
        }
        return parent;
    }

    private const System.Reflection.BindingFlags Public = System.Reflection.BindingFlags.Public
        | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase;
}
