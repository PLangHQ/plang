namespace app.call.setting.diff;

/// <summary>
/// Whether a call keeps the variable changes it makes — <c>%!app.call.setting.diff%</c>, off by default — and
/// how: scalars only, or a deep copy of a non-scalar's value before it changed (<see cref="Deep"/>).
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>A settings class is a type but never LLM vocabulary (774) — kept out of the catalog.</summary>
    public static bool Internal => true;

    public @this(global::app.type.@this type) : base(type) => Enabled = false;

    /// <summary>A deep copy of a non-scalar's value before it changed — <c>%!app.call.setting.diff.deep%</c>.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Deep { get; set; } = global::app.type.item.@bool.@this.False;
}
