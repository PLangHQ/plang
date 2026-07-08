namespace app.type.kind.behavior;

/// <summary>
/// The <c>*</c> kind — the catch-all: navigate ANY object by reflecting its public
/// properties. The behavior a <c>clr</c> falls back to when its object's kind names no
/// registered format (a third-party POCO, an infra collection). Holds the reflection that
/// used to live inline on <c>clr</c>.
/// </summary>
public sealed class reflection : @this
{
    public override global::app.type.kind.@this Kind => "*";

    // Bottom-up + DeclaredOnly + IgnoreCase so a shadowing derived property wins and
    // GetProperty never throws Ambiguous.
    protected override (bool, object?) Step(object obj, string key, global::app.actor.context.@this ctx)
    {
        System.Reflection.PropertyInfo? prop = null;
        for (var t = obj.GetType(); t != null && prop == null; t = t.BaseType)
            prop = t.GetProperty(key, System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.IgnoreCase
                | System.Reflection.BindingFlags.DeclaredOnly);
        return prop == null ? (false, null) : (true, prop.GetValue(obj));
    }

    // A reflected property that already IS a Data rides through as itself; otherwise the
    // raw value becomes a child Data (the Data ctor lifts it to its plang type).
    protected override global::app.data.@this Data(string name, object? node,
        global::app.data.@this? parent, global::app.actor.context.@this ctx)
        => node is global::app.data.@this d ? d
           : new global::app.data.@this(name, node, parent: parent, context: ctx);

    public override System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(
        object obj, global::app.actor.context.@this ctx)
    {
        foreach (var p in obj.GetType().GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            yield return new global::app.data.@this(p.Name, p.GetValue(obj), context: ctx);
    }
}
