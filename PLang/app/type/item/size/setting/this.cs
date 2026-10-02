namespace app.type.item.size.setting;

/// <summary>How sizes are written — <c>%!app.type.size.setting%</c>. A size read from text keeps the standard its
/// suffix names; one made from a count (a download's bytes, a file's length) is written in <see cref="Standard"/>.</summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>The standard a counted size is written in — <c>iec</c> (1024: 95.4 MiB) or <c>si</c> (1000: 100 MB).</summary>
    [Out, Store] public global::app.type.item.choice.@this<kind.@this> Standard { get; set; } = new kind.iec.@this();
}
