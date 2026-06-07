namespace app.goal.steps.step.actions.action;

public sealed partial class @this
{
    /// <summary>
    /// OBP: Action owns its reconstruction from a wire record, the read-side mirror of
    /// <see cref="AsData"/>. An on-error recovery chain rides the born-native wire as
    /// <c>{module, action, parameters:[{name,value,type}]}</c> records; rebuild from those
    /// slots via <see cref="data.@this.FromWireShape"/> (which reads value/type directly, so
    /// no <c>@schema:data</c> marker is required — a ToRaw→JSON round-trip strips it and loses
    /// the params). A named factory, not a catalog Convert hook — a structural action chain
    /// rebuilds through here explicitly; the generic <c>list&lt;action&gt;</c> conversion keeps
    /// its own element behavior. Returns <c>null</c> for a shape that names no module/action.
    /// </summary>
    public static @this? FromWire(object? value, global::app.actor.context.@this context)
    {
        if (value is @this already) return already;

        string? module = global::app.data.@this.WireSlot(value, "module")?.ToString();
        string? actionName = global::app.data.@this.WireSlot(value, "action")?.ToString();
        if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(actionName)) return null;

        var act = new @this { Module = module!, ActionName = actionName! };
        if (global::app.data.@this.WireSlot(value, "parameters") is { } paramsRaw
            && Sequence(paramsRaw) is { } paramSeq)
        {
            foreach (var p in paramSeq)
            {
                var pRaw = p is global::app.data.@this pd ? pd.Value : p;
                var name = global::app.data.@this.WireSlot(pRaw, "name")?.ToString() ?? "";
                act.Parameters.Add(global::app.data.@this.FromWireShape(pRaw, name, context));
            }
        }
        return act;
    }

    // A native list / CLR sequence of records, or null when the value isn't iterable.
    internal static System.Collections.IEnumerable? Sequence(object? value) => value switch
    {
        string => null,
        app.type.list.@this nl => nl.Items.Select(i => i.Value),
        System.Collections.IEnumerable e => e,
        _ => null,
    };
}
