namespace app.module.browser.type.browser.kind.headless;

/// <summary>One page rendered off-screen: its frames go to OnFrame, its input comes through browser.send.</summary>
public sealed class @this : kind.@this
{
    public @this() : base("headless") { }
}
