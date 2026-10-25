namespace app.module.screen.setting;

/// <summary>How screens take their frames — <c>%!screen.setting%</c>.</summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The most a frame's pixels take — <c>%!screen.setting.max%</c>; a frame whose header claims more
    /// (width × height × 4 bytes) is refused before it is decoded. 128 MiB, an 8K frame.</summary>
    [Out, Store, global::app.module.Default("128 MiB")]
    public global::app.type.item.size.@this Max { get; set; } = null!;
}
