namespace app.call.setting.frame;

/// <summary>The frames a call stack keeps — <c>%!app.call.setting.frame%</c>.</summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>A settings class is a type but never LLM vocabulary (774) — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>How many returned calls the history keeps — <c>%!app.call.setting.frame.max%</c>.</summary>
    [Out, Store] public global::app.type.item.number.@this Max { get; set; } = 1000;
}
