namespace app.module.debug.setting.length;

/// <summary>How long a debug line runs — <c>%!debug.setting.length%</c>.</summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>Characters per line before it is cut — <c>%!debug.setting.length.max%</c>. Default 500.</summary>
    [Out, Store] public global::app.type.item.number.@this Max { get; set; } = 500;
}
