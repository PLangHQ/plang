namespace app.type.item.kind.json.setting;

/// <summary>How json is written — <c>%!app.type.kind.json.setting%</c>. Read once where json's encode starts; plang's own
/// format, a digest and a .pr have writers of their own and never read it.</summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The layout json is written in — <c>formatted</c> (indented) or <c>compact</c> (one line).</summary>
    [Out, Store, global::app.module.Default("formatted")]
    public global::app.type.item.choice.@this<layout> Layout { get; set; } = null!;
}
