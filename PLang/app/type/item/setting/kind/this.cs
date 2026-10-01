namespace app.type.item.setting.kind;

/// <summary>
/// One class of settings, as a kind of <c>setting</c> named by its path (<c>{setting, goal.list.setting}</c>):
/// what a value of it is, and a fresh one with its defaults.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly System.Type _class;
    private readonly System.Lazy<global::app.type.property.list.@this> _property;

    /// <param name="sample">One of the class, which says its own path.</param>
    /// <param name="types">The types it names its options' types through.</param>
    public @this(global::app.type.item.setting.@this sample, global::app.type.list.@this types) : base(sample.Path)
    {
        _class = sample.GetType();
        _property = new(() => Options(types));
    }

    /// <summary>The class's options as the builder teaches them — each by the name a <c>%!path%</c> writes
    /// (<c>cache</c>), its type, and its default — the value itself, which writes its own text (<c>true</c>,
    /// <c>30</c>, <c>[]</c>, a node's members).</summary>
    public global::app.type.property.list.@this Property => _property.Value;

    // The option rows, read off a fresh one of the class (its defaults).
    private global::app.type.property.list.@this Options(global::app.type.list.@this types)
    {
        var fresh = Create();
        var rows = new global::app.type.property.list.@this();
        foreach (var option in fresh.Options)
        {
            var reflected = new global::app.type.property.@this(option, types);
            rows.Add(new global::app.type.property.@this
            {
                Name = char.ToLowerInvariant(option.Name[0]) + option.Name[1..],
                Type = reflected.Type,
                Nullable = reflected.Nullable,
                Default = option.GetValue(fresh) is global::app.type.item.@this { IsNull: false } value ? value : null,
            });
        }
        return rows;
    }

    protected internal override string Owner => "setting";

    /// <summary>A value of this kind is one of the class.</summary>
    public override System.Type? ClrForm => _class;

    /// <summary>The class is an actor's own (<see cref="OwnAttribute"/>): read from the actor's row alone.</summary>
    internal bool Own => System.Attribute.IsDefined(_class, typeof(OwnAttribute));

    /// <summary>A new one of the class — its defaults.</summary>
    internal global::app.type.item.setting.@this Create()
        => (global::app.type.item.setting.@this)System.Activator.CreateInstance(_class)!;
}
