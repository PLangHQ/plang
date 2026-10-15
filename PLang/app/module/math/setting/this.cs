namespace app.module.math.setting;

/// <summary>How numbers behave — <c>%!math.setting%</c>.</summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>A settings class is a type but never LLM vocabulary (774) — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>When two numbers are the same — <c>%!math.setting.equal%</c>.</summary>
    [Out, Store] public equal.@this Equal { get; set; } = new();
}
