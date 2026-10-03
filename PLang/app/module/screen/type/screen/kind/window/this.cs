namespace app.module.screen.type.screen.kind.window;

/// <summary>A window on the host (Windows) that shows the frames drawn into it. (<c>window</c> is also a type, the
/// window module's: <c>is window</c> asks that type, so a goal reads this kind as <c>%screen!type.kind%</c>.)</summary>
public sealed class @this : kind.@this
{
    public @this() : base("window") { }
}
