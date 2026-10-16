namespace app.type.item.template.kind;

/// <summary>
/// A kind of template — how a value's <c>%variables%</c> are filled when it is read (<c>plang</c>: from memory). A
/// read asked for one (<c>file.read(Path, Template=plang)</c>) lands content marked with the kind's name, the template
/// mark every value carries (<c>type.Template</c>); a read asked for none lands it as written.
///
/// <para>Not a <c>kind&lt;T&gt;</c> (decision 774): template is a facet of a type — the mark <c>type.Template</c> —
/// not a value type of its own, so there is no <c>template.@this</c> to be the parent. <c>plang</c> is a named value
/// of that facet. Left on the non-generic base with its string <c>Owner</c>; the facet itself is revisited in stage 6.</para>
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    protected @this(string name) : base(name) { }

    protected internal override string Owner => "template";
}
