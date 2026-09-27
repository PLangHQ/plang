namespace app.module.action.llm.setting;

/// <summary>
/// The llm module's own settings — <c>%!llm.cache%</c>: what every llm action takes when neither the step
/// nor the action's own setting says.
/// </summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>Whether an answer is cached. A build with <c>--build={"cache":false}</c> turns it off.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Cache { get; set; } = true;
}
