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

    internal Property(string text, string name) : base(text) => Name = name;

    public override string Kind => "property";

    protected override void Piece(global::app.type.format.IWriter writer) => writer.String(Name);

    internal bool IsBinding => Name.StartsWith('!');

    /// <summary>A member is the value's own content; a binding (<c>%x!context%</c>) reaches past the value into
    /// what holds it.</summary>
    internal override bool IsOwn => !IsBinding;

    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Start(
        global::app.data.@this? previous, global::app.actor.context.@this context)
    {
        if (previous is null) return context.NotFound(Name);
        if (!IsBinding) return await previous.Peek().Get(previous, Name);

        var key = Name[1..];
        if (previous.Properties.ContainsKey(key))
            return new global::app.data.@this(key, await previous.Properties.Value(key), parent: previous);

        // Data's own members (Name, Type, Error, Success, …), then a subclass's.
        var member = typeof(global::app.data.@this).GetProperty(key, Public)
                     ?? previous.GetType().GetProperty(key, Public);
        if (member != null)
            return new global::app.data.@this(key, member.GetValue(previous), parent: previous);

        // The value's own typed metadata (!path, !host, !size, !length) without materialising content.
        // NonPublic included: the raw derivations (path.Relative/.Extension/.Absolute) are internal C#
        // but ARE the !relative/!extension/!absolute projections.
        var peeked = previous.Peek();
        var own = peeked.GetType().GetProperty(key, Public | System.Reflection.BindingFlags.NonPublic);
        if (own != null)
            return new global::app.data.@this(key, own.GetValue(peeked), parent: previous);

        // A member that needs the asker's context is a method taking one context — !relative,
        // !mimetype, !kind answer with the binding's own context.
        var asks = peeked.GetType().GetMethod(key, Public | System.Reflection.BindingFlags.NonPublic,
            binder: null, types: [typeof(global::app.actor.context.@this)], modifiers: null);
        if (asks != null)
            return new global::app.data.@this(key, asks.Invoke(peeked, [previous.Context]), parent: previous);

        return previous.Context?.NotFound(key) ?? context.NotFound(key);
    }

    /// <summary>A member takes the value as the parent's child; a <c>!</c> name lands in the
    /// binding's Properties.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context)
    {
        if (parent is null) return context.NotFound(Name);
        if (!IsBinding) return await parent.Set(Name, isIndex: false, value);
        if (!parent.IsInitialized)
            return context.Error(new global::app.error.Error($"Variable '{parent.Name}' is not set", "VariableNotFound", 400));

        try
        {
            parent.Properties[Name[1..]] = value is global::app.data.@this held ? await held.Value() : value;
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
