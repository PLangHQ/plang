namespace app.type.item.setting.kind;

/// <summary>
/// One class of settings, as a kind of <c>setting</c> named by its path (<c>{setting, goal.list.setting}</c>):
/// what a value of it is, and a fresh one with its defaults.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly System.Type _class;

    /// <param name="sample">One of the class, which says its own path.</param>
    public @this(global::app.type.item.setting.@this sample) : base(sample.Path) => _class = sample.GetType();

    protected internal override string Owner => "setting";

    /// <summary>A value of this kind is one of the class.</summary>
    public override System.Type? ClrForm => _class;

    /// <summary>A new one of the class — its defaults.</summary>
    internal global::app.type.item.setting.@this Create()
        => (global::app.type.item.setting.@this)System.Activator.CreateInstance(_class)!;
}
