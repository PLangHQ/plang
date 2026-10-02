namespace app.module.llm.setting;

/// <summary>
/// The llm module's own settings — <c>%!llm.setting.cache%</c>: what every llm action takes when neither the step
/// nor the action's own setting says.
/// </summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>Whether an answer kept from before is used — used, unless a step or this setting skips it.</summary>
    [Out, Store] public global::app.type.item.choice.@this<global::app.module.cache.type.cache> Cache { get; set; }
        = new(global::app.module.cache.type.cache.use);
}
