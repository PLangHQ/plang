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

    // The inverse of Output: build a host CLR object by reflecting its [Store] props and
    // pulling each off the format-agnostic reader (wire order drives, unknown names skip,
    // missing names keep the property default). A collection host (StepActions : IList<action>)
    // reads as an array of its element; List<Data> props (action.Parameters) hand their bytes
    // to the @schema:data reader (%var%-born / template / signing byte-identical). The `*` kind
    // owns the SHAPE walk; a format kind (json) owns bridging its content to the reader.
    public object? Read<TReader>(ref TReader reader, global::System.Type target,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return null;

        // Collection host — an array of its element type (StepActions→action, GoalSteps→step).
        // These are IList<T> (generic only), so add through ICollection<T>.Add by interface.
        var element = ElementTypeOf(target);
        if (element != null)
        {
            var coll = global::System.Activator.CreateInstance(target)!;
            var add = typeof(global::System.Collections.Generic.ICollection<>).MakeGenericType(element).GetMethod("Add")!;
            reader.BeginArray();
            while (reader.NextElement()) add.Invoke(coll, new[] { ReadValue(ref reader, element, ctx) });
            reader.EndArray();
            return coll;
        }

        // Object host — its [Store] props, matched to wire names (the same selector Output writes).
        var host = global::System.Activator.CreateInstance(target)!;
        var byName = new global::System.Collections.Generic.Dictionary<string, global::System.Reflection.PropertyInfo>(
            global::System.StringComparer.OrdinalIgnoreCase);
        foreach (var entry in global::app.channel.serializer.filter.Tagged.PropertiesFor(target, global::app.View.Store))
            byName[entry.WireName] = entry.Property;

        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            if (byName.TryGetValue(name, out var prop) && prop.CanWrite)
                prop.SetValue(host, ReadValue(ref reader, prop.PropertyType, ctx));
            else
                reader.Skip();
        }
        reader.EndObject();
        return host;
    }

    // Read one value AS its declared CLR type off the reader.
    private object? ReadValue<TReader>(ref TReader reader, global::System.Type type,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        var t = global::System.Nullable.GetUnderlyingType(type) ?? type;
        if (reader.Null()) return null;

        if (t == typeof(string)) return reader.String();
        if (t == typeof(bool)) return reader.Bool();
        if (t == typeof(int)) return (int)reader.Long();
        if (t == typeof(long)) return reader.Long();
        if (t.IsEnum)
            return reader.Peek() == global::app.channel.serializer.TokenKind.Number
                ? global::System.Enum.ToObject(t, reader.Long())
                : global::System.Enum.Parse(t, reader.String(), ignoreCase: true);

        // A path rides as its string form — the perimeter crossing (path.Resolve once).
        // TEMP: generalizes to "any plang value type reads through its own reader" once the
        // pr-graph hosts drop item.@this and the item-subclass signal cleanly means "value".
        if (t == typeof(global::app.type.path.@this))
            return global::app.type.path.@this.Resolve(reader.String(), ctx.Context);

        // List<Data> (Parameters / Defaults) — each element a {name,type,value} through the
        // @schema:data reader over its own verbatim bytes (sign-identical to the byte path).
        if (IsListOfData(t)) return ReadDataList(ref reader, t, ctx);

        // A nested object/collection host → recurse the same walk.
        if (ElementTypeOf(t) != null || t.IsClass) return Read(ref reader, t, ctx);

        // Scalar fallback: raw slot lowered to the target.
        var raw = new global::app.type.item.serializer.json(ctx.Context).ReadSlot(ref reader, ctx);
        return raw is global::app.type.item.@this iv ? iv.Clr(t) : raw;
    }

    private global::System.Collections.IList ReadDataList<TReader>(ref TReader reader,
        global::System.Type listType, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        var list = (global::System.Collections.IList)global::System.Activator.CreateInstance(listType)!;
        var dataReader = new global::app.data.reader.@this();
        reader.BeginArray();
        // Each param's own verbatim bytes → the @schema:data reader (it owns its format). The
        // shape walk stays format-agnostic: RawValue is IReader surface, no json named here.
        while (reader.NextElement())
            list.Add(dataReader.Read(reader.RawValue(), ctx));
        reader.EndArray();
        return list;
    }

    // The element type of a collection host (IList<T> / IEnumerable<T>, not string, not List<Data>).
    private global::System.Type? ElementTypeOf(global::System.Type t)
    {
        if (t == typeof(string) || IsListOfData(t)) return null;
        foreach (var i in t.GetInterfaces())
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::System.Collections.Generic.IList<>))
                return i.GetGenericArguments()[0];
        return null;
    }

    private bool IsListOfData(global::System.Type t)
    {
        foreach (var i in t.GetInterfaces())
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::System.Collections.Generic.IEnumerable<>)
                && i.GetGenericArguments()[0] == typeof(global::app.data.@this))
                return true;
        return false;
    }

    // The inverse of Read: reflect the object's tagged props and write each under its WIRE
    // name (the [JsonPropertyName]/camelCase form Read matches on and STJ round-trips), nulls
    // omitted. Serves hosts (goal/step/action) and foreign POCOs alike — the one Output path.
    public override async global::System.Threading.Tasks.ValueTask Output(
        object obj, global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? ctx)
    {
        writer.BeginObject();
        foreach (var entry in global::app.channel.serializer.filter.Tagged.PropertiesFor(obj.GetType(), mode))
        {
            if (entry.Masked) { writer.Name(entry.WireName); writer.String("****"); continue; }
            object? value;
            try { value = entry.Property.GetValue(obj); }
            catch (System.Exception ex)
            {
                throw new global::app.data.OutputException(
                    $"Output failed reading {obj.GetType().Name}.{entry.Property.Name}: {ex.Message}",
                    "OutputGetterThrew", ex);
            }
            if (value == null) continue;   // nulls omitted (WhenWritingNull)
            writer.Name(entry.WireName);
            await WriteReflected(writer, value, mode, ctx);
        }
        writer.EndObject();
    }

    // A value reflected off a property writes itself: a plang value / a Data via its own
    // Output, a sequence as an array of self-writes, a raw C# scalar through the writer (C#
    // primitives can't write themselves). The reflection→wire boundary.
    private async global::System.Threading.Tasks.ValueTask WriteReflected(
        global::app.channel.serializer.IWriter writer, object value, global::app.View mode,
        global::app.actor.context.@this? ctx)
    {
        switch (value)
        {
            case global::app.type.item.@this item: await item.Output(writer, mode, ctx); break;
            case global::app.data.@this d: await d.Output(writer, mode, ctx); break;
            case System.Collections.IEnumerable seq when value is not (string or byte[]):
                writer.BeginArray(-1);
                foreach (var el in seq) if (el != null) await WriteReflected(writer, el, mode, ctx);
                writer.EndArray();
                break;
            default: writer.Value(value); break;
        }
    }
}
