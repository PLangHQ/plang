namespace app.callstack.setting;

/// <summary>
/// What a call stack captures — <c>%!app.callstack.setting%</c>; <c>--callstack={…}</c> is this run's values for
/// it. Each option turns on one tier of a call's data:
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>StartedAt, CompletedAt, Duration on every call.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Timing { get; set; } = global::app.type.item.@bool.@this.False;

    /// <summary>The variable changes a call makes (scalars only unless <see cref="DeepDiff"/>).</summary>
    [Out, Store] public global::app.type.item.@bool.@this Diff { get; set; } = global::app.type.item.@bool.@this.False;

    /// <summary>A deep copy of a non-scalar's value before it changed (only with <see cref="Diff"/>).</summary>
    [Out, Store] public global::app.type.item.@bool.@this DeepDiff { get; set; } = global::app.type.item.@bool.@this.False;

    /// <summary>A hint for exporters; a call's own tags are written either way.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Tags { get; set; } = global::app.type.item.@bool.@this.False;

    /// <summary>Keep the calls that returned, under their caller (at most <see cref="MaxFrames"/>).</summary>
    [Out, Store] public global::app.type.item.@bool.@this History { get; set; } = global::app.type.item.@bool.@this.False;

    /// <summary>How many returned calls <see cref="History"/> keeps.</summary>
    [Out, Store] public global::app.type.item.number.@this MaxFrames { get; set; } = 1000;
}
