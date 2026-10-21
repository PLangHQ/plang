namespace app.module.math.setting.equal;

/// <summary>When two numbers are the same — <c>%!math.setting.equal%</c>.</summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The significant digits two numbers are compared at, when a binary float is one of them —
    /// <c>%!math.setting.equal.digits%</c>. Default 15 (what a double shows: <c>0.1 + 0.2</c> is <c>0.3</c>); 0 is
    /// exact. Equality and order both read it.</summary>
    [Out, Store] public global::app.type.item.number.@this Digits { get; set; } = 15;
}
