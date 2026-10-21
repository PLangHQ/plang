namespace app.channel.setting;

/// <summary>
/// What the console's channels write and read in — <c>%!app.channel.setting%</c>; <c>--channel.setting.format=…</c>
/// is this run's value for it. The setting holds until changed, for every writer to the console.
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The format, named as any format is (its name, MIME or extension): <c>txt</c> for a person at a
    /// terminal, <c>plang</c> for a program that runs this plang, <c>json</c>, <c>bin</c> for a value's own bytes.</summary>
    [Out, Store, global::app.module.Default("txt")] public global::app.channel.format.@this Format { get; set; } = null!;
}
