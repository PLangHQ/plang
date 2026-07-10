namespace app.type.item.choice.serializer;

using System.Reflection;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.item.choice.@this{T}"/> — the closed named-set value. Its wire form
/// is the chosen option's NAME (a scalar, e.g. <c>"=="</c>); the KIND names the option
/// set (<c>"operator"</c>, <c>"httpmethod"</c>), which resolves through the type registry
/// to the closed <c>choice&lt;T&gt;</c> wrapper. The choice builds ITSELF from the name
/// (<c>choice&lt;T&gt;.FromName</c>) — the reader only resolves the closed type and hands
/// it the scalar. Format-agnostic: the same impl reads the name off any <c>IReader</c>.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    // The closed choice<T> wrapper's own FromName(name, context) — cached per wrapper.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, MethodInfo> _fromName = new();

    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("choice", kind);
        var name = reader.String();
        if (string.IsNullOrEmpty(kind))
            throw new System.NotSupportedException(
                "choice reader: a choice value needs its kind (the option-set name, e.g. 'operator').");

        // The kind names the closed set; the registry maps it to the choice<T> wrapper
        // (RegisterModuleChoiceTypes did the reverse "operator" → choice<Operator> mapping).
        var wrapper = ctx.Context.App.Type[kind!].ClrType
            ?? throw new System.NotSupportedException($"choice reader: no closed type for kind '{kind}'.");

        var fromName = _fromName.GetOrAdd(wrapper, static w =>
            w.GetMethod("FromName", BindingFlags.Public | BindingFlags.Static)
            ?? throw new System.NotSupportedException($"choice<{w.Name}>: has no static FromName(name, context)."));

        return (global::app.type.item.@this)fromName.Invoke(null, new object?[] { name, ctx.Context })!;
    }
}
