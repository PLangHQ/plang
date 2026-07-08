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

    // A foreign POCO has no plang shape of its own, so it renders as an object of its
    // [Out] fields — each field VALUE lifts to its item via type.Create and writes itself.
    // The `*` kind owns only the reflection; every field's serialization is its own item's.
    public override async global::System.Threading.Tasks.ValueTask Output(
        object obj, global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? ctx)
    {
        writer.BeginObject();
        foreach (var entry in global::app.channel.serializer.filter.Tagged.PropertiesFor(obj.GetType(), mode))
        {
            writer.Name(entry.Property.Name.ToLowerInvariant());
            if (entry.Masked) { writer.String("****"); continue; }
            object? raw;
            try { raw = entry.Property.GetValue(obj); }
            catch (System.Exception ex)
            {
                throw new global::app.data.OutputException(
                    $"Output failed reading {obj.GetType().Name}.{entry.Property.Name}: {ex.Message}",
                    "OutputGetterThrew", ex);
            }
            if (raw is global::app.data.@this nested)
                await nested.Output(writer, mode, ctx);
            else
                await global::app.type.@this.Create(raw, ctx).Output(writer, mode, ctx);
        }
        writer.EndObject();
    }
}
