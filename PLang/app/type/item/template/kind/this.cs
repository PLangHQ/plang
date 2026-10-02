namespace app.type.item.template.kind;

/// <summary>
/// A kind of template — how a value's <c>%variables%</c> are filled when it is read (<c>plang</c>: from memory). A
/// read asked for one (<c>file.read(Path, Template=plang)</c>) lands content marked with the kind's name, the template
/// mark every value carries (<c>type.Template</c>); a read asked for none lands it as written.
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    protected @this(string name) : base(name) { }

    protected internal override string Owner => "template";
}
