namespace app.callstack.setting.diff;

/// <summary>
/// Whether a call keeps the variable changes it makes — <c>%!app.callstack.setting.diff%</c>, off by default — and
/// how: scalars only, or a deep copy of a non-scalar's value before it changed (<see cref="Deep"/>).
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    public @this() => Enabled = false;

    /// <summary>A deep copy of a non-scalar's value before it changed — <c>%!app.callstack.setting.diff.deep%</c>.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Deep { get; set; } = global::app.type.item.@bool.@this.False;
}
