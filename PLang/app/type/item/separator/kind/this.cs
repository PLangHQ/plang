namespace app.type.item.separator.kind;

/// <summary>
/// A separator's kind — a named separator (line, comma, tab, space, semicolon) knowing the characters it stands for.
/// This one, with no name, is the type's own: what a separator can be in a step, as the decider is offered it.
/// </summary>
public class @this : global::app.type.kind.@this
{
    public @this() : this("") { }

    protected @this(string name) : base(name) { }

    protected internal override string Owner => "separator";

    /// <summary>The characters it stands for; the type's own kind stands for none.</summary>
    public virtual string Characters => "";

    /// <summary>The named separators, each by its name, then the step's variables.</summary>
    public override async System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => [.. Named.Select(named => new global::app.type.item.separator.@this(named)), .. await base.Offers(step)];

    // the named separators, found as the registry finds the kinds it holds
    private System.Collections.Generic.IEnumerable<@this> Named
        => Every(typeof(@this).Assembly).OfType<@this>().Where(kind => !kind.IsEmpty);
}
