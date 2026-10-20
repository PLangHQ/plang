namespace app.type.item.size.setting;

/// <summary>How sizes are written — <c>%!app.type.size.setting%</c>. A size read from text keeps the standard its
/// suffix names; one made from a count (a download's bytes, a file's length) is written in <see cref="Standard"/>.</summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary (774) — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The standard a counted size is written in — <c>iec</c> (1024: 95.4 MiB) or <c>si</c> (1000: 100 MB).</summary>
    [Out, Store, global::app.module.Default("iec")]
    public global::app.type.item.choice.@this<kind.@this> Standard { get; set; } = null!;
}
