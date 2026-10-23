namespace app.call.setting;

/// <summary>
/// What a call stack captures — <c>%!app.call.setting%</c>; <c>--call.setting.…</c> is this run's values for
/// it. Each option turns on one tier of a call's data:
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>StartedAt, CompletedAt, Duration on every call.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Timing { get; set; } = global::app.type.item.@bool.@this.False;

    /// <summary>The variable changes a call makes — scalars only, unless <c>diff.deep</c>.</summary>
    [Out, Store] public diff.@this Diff { get; set; }

    /// <summary>A hint for exporters; a call's own tags are written either way.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Tags { get; set; } = global::app.type.item.@bool.@this.False;

    /// <summary>Keep the calls that returned, under their caller (at most <c>frame.max</c>).</summary>
    [Out, Store] public global::app.type.item.@bool.@this History { get; set; } = global::app.type.item.@bool.@this.False;

    /// <summary>The frames kept — how many returned calls <see cref="History"/> keeps (<c>frame.max</c>).</summary>
    [Out, Store] public frame.@this Frame { get; set; }
}
