namespace app.module.screen.type.screen.kind;

/// <summary>
/// A kind of screen — <c>window</c> (a window on the host that shows frames) or <c>display</c> (PlangOS's display that
/// programs draw onto). A goal reads it as <c>%screen!type.kind%</c>, or asks <c>if %screen% is display</c>.
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    protected @this(string name) : base(name) { }

    protected internal override string Owner => "screen";
}
