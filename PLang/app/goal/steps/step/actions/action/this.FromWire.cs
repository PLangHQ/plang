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
                var pRaw = p is global::app.data.@this pd ? pd.Peek() : p;
                var name = global::app.data.@this.WireSlot(pRaw, "name")?.ToString() ?? "";
                act.Parameters.Add(global::app.data.@this.FromWireShape(pRaw, name, context));
            }
            act.BornNativeParameters(context);
        }
        // A modifier (error.handle, cache.wrap, timeout.after) rides the wire nested under its
        // target as an action-shaped record — rebuild each recursively. Without this the modifier
        // (and the whole `on error …` clause) is silently dropped, both at build-time deserialize
        // of the compile response and at runtime recovery-chain reconstruction.
        if (global::app.data.@this.WireSlot(value, "modifiers") is { } modsRaw
            && Sequence(modsRaw) is { } modSeq)
        {
            foreach (var m in modSeq)
            {
                var mRaw = m is global::app.data.@this md ? md.Peek() : m;
                if (FromWire(mRaw, context) is { } mod) act.Modifiers.Add(mod);
            }
        }
        return act;
    }


    /// <summary>
    /// Born-native composites: a parameter whose tagged type maps to a CLR class exposing
    /// <c>static data.@this FromWire(object?, context)</c> (e.g. <c>goal.call</c> →
    /// <see cref="GoalCall.FromWire"/>) constructs its typed object HERE, at the wire
    /// boundary — the load is the deserialization boundary, so no dict shape flows
    /// downstream and the dispatch door only ever sees the real type. A dict reaching
    /// a typed slot at runtime is a conversion error, never silently converted.
    /// </summary>
    internal void BornNativeParameters(global::app.actor.context.@this context)
    {
        BornNative(Parameters, context);
        if (Defaults != null) BornNative(Defaults, context);
        foreach (var m in Modifiers) m.BornNativeParameters(context);
    }

    private static void BornNative(System.Collections.Generic.List<global::app.data.@this> slots, global::app.actor.context.@this context)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var p = slots[i];
            var typeName = p.Type?.Name;
            if (string.IsNullOrEmpty(typeName) || context.App == null) continue;
            var clr = context.App.Type.Clr(typeName!);
            if (clr == null || clr.IsPrimitive) continue;
            var hook = clr.GetMethod("FromWire",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(object), typeof(global::app.actor.context.@this) }, null);
            if (hook == null || hook.ReturnType != typeof(global::app.data.@this)) continue;
            var raw = p.Peek();
            if (raw == null || clr.IsInstanceOfType(raw)) continue;
            if (hook.Invoke(null, new object?[] { raw, context }) is global::app.data.@this r
                && r.Success && r.Peek() is { } typed && clr.IsInstanceOfType(typed))
                slots[i] = new global::app.data.@this(p.Name, typed, p.Type);
        }
    }

    // A native list / CLR sequence of records, or null when the value isn't iterable.
    internal static System.Collections.IEnumerable? Sequence(object? value) => value switch
    {
        string => null,
        app.type.list.@this nl => nl.Items.Select(i => i.Materialize()),
        System.Collections.IEnumerable e => e,
        _ => null,
    };
}
