namespace app.callstack.setting.frame;

/// <summary>The frames a call stack keeps — <c>%!app.callstack.setting.frame%</c>.</summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>How many returned calls the history keeps — <c>%!app.callstack.setting.frame.max%</c>.</summary>
    [Out, Store] public global::app.type.item.number.@this Max { get; set; } = 1000;
}
