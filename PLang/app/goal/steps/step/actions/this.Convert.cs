namespace app.goal.steps.step.actions;

public sealed partial class @this
{
    /// <summary>
    /// OBP: a structural action chain owns its reconstruction. <c>error.handle.Actions</c>
    /// (an on-error recovery chain) rides the born-native wire as a list of action records;
    /// rebuild each into an <c>action.@this</c> directly from its <c>{module, action,
    /// parameters:[{name,value,type}]}</c> shape via <see cref="data.@this.FromWireShape"/>.
    /// A ToRaw→JSON round-trip loses the params (the marker the Data re-read needs is stripped
    /// by ToRaw), so reconstruct field-by-field here instead.
    /// </summary>
    public static global::app.data.@this? Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        if (value is @this already) return global::app.data.@this.Ok(already);
        System.Collections.IEnumerable? seq = value switch
        {
            string => null,
            app.type.list.@this nl => nl.Items.Select(i => i.Value),
            System.Collections.IEnumerable e => e,
            _ => null,
        };
        if (seq == null) return null;

        var actions = new @this();
        foreach (var element in seq)
        {
            var raw = element is global::app.data.@this d ? d.Value : element;
            // Already a built action (a C#-constructed chain, or a list that held actions) —
            // take it as-is; only a wire record needs rebuilding.
            if ((raw ?? element) is action.@this built) { actions.Add(built); continue; }
            var act = BuildAction(raw, context);
            if (act != null) actions.Add(act);
        }
        return global::app.data.@this.Ok(actions);
    }

    private static action.@this? BuildAction(object? raw, global::app.actor.context.@this context)
    {
        string? module = global::app.data.@this.WireSlot(raw, "module")?.ToString();
        string? actionName = global::app.data.@this.WireSlot(raw, "action")?.ToString();
        if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(actionName)) return null;

        var act = new action.@this { Module = module!, ActionName = actionName! };
        if (global::app.data.@this.WireSlot(raw, "parameters") is { } paramsRaw)
        {
            System.Collections.IEnumerable? paramSeq = paramsRaw switch
            {
                app.type.list.@this nl => nl.Items.Select(i => i.Value),
                System.Collections.IEnumerable e and not string => e,
                _ => null,
            };
            if (paramSeq != null)
                foreach (var p in paramSeq)
                {
                    var pRaw = p is global::app.data.@this pd ? pd.Value : p;
                    var name = global::app.data.@this.WireSlot(pRaw, "name")?.ToString() ?? "";
                    act.Parameters.Add(global::app.data.@this.FromWireShape(pRaw, name, context));
                }
        }
        return act;
    }
}
