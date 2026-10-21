namespace app.module.archive.setting;

/// <summary>How archives are packed and unpacked — <c>%!archive.setting%</c>: what every archive action takes when its
/// step says nothing.</summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The most an unpack writes — <c>%!archive.setting.max%</c>; an archive that unpacks to more is refused as
    /// it passes it (a zip bomb). 100 MiB.</summary>
    [Out, Store, global::app.module.Default("100 MiB")]
    public global::app.type.item.size.@this Max { get; set; } = null!;

    /// <summary>How hard a pack compresses — fastest, optimal, smallest or none.</summary>
    [Out, Store] public global::app.type.item.choice.@this<Level> Level { get; set; } = setting.Level.optimal;
}

/// <summary>How hard a pack compresses.</summary>
[global::app.Attributes.PlangType("compression"), global::app.Attributes.Choice]
public enum Level { fastest, optimal, smallest, none }
