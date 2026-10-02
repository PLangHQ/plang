namespace app.module.math.setting;

/// <summary>How numbers behave — <c>%!math.setting%</c>.</summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>When two numbers are the same — <c>%!math.setting.equal%</c>.</summary>
    [Out, Store] public equal.@this Equal { get; set; } = new();
}
