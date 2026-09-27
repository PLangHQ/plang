namespace app.module.action.build.setting;

/// <summary>
/// The build module's own settings — <c>%!build.cache%</c>: what the builder's goals read.
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Whether the builder's LLM answers are cached. <c>--build={"cache":false}</c> turns it off.</summary>
    [Out] public global::app.type.item.@bool.@this Cache { get; set; } = true;
}
