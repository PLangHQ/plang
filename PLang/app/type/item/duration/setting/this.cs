namespace app.type.item.duration.setting;

/// <summary>How spans are written — <c>%!app.type.duration.setting%</c>. A duration read from text keeps the standard its
/// form names; a span with no form of its own (how long a step ran) is made in <see cref="Standard"/>.</summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The standard a span with no form of its own is made in — <c>short</c> (1m23.456s), <c>iso</c>
    /// (PT1M23.456S) or <c>dotnet</c> (00:01:23.4560000).</summary>
    [Out, Store, global::app.module.Default("short")]
    public global::app.type.item.choice.@this<global::app.type.kind.@this<duration.@this>> Standard { get; set; } = null!;
}
