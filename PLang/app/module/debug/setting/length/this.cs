namespace app.module.debug.setting.length;

/// <summary>How long a debug line runs — <c>%!debug.setting.length%</c>.</summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Characters per line before it is cut — <c>%!debug.setting.length.max%</c>. Default 500.</summary>
    [Out, Store] public global::app.type.item.number.@this Max { get; set; } = 500;
}
