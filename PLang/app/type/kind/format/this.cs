namespace app.type.kind.format;

/// <summary>
/// The kind a <c>[Format]</c> declaration mints — one concrete <c>kind&lt;T&gt;</c> for every format a type's
/// class declares (image's png/gif/…, text's md/xml, table's csv), T the declaring type. One class, many
/// instances (as a choice set is one class over many enums): a format differs only in data — its MIMEs, its
/// extension, whether it compresses — so it needs no class of its own. Whether T writes and reads itself is
/// the format's own question (decision 157): T's IEncode/IDecode, bound on the first write or read, once for
/// every format of T. The registry makes one per <c>[Format]</c> (decision 774), holding T's entry as its owner.
/// </summary>
public sealed class @this<T> : global::app.type.kind.@this<T> where T : global::app.type.item.@this
{
    // How T writes its formats — its IEncode, bound into a delegate when a format of T first writes (no reflection
    // per write); null when T writes none of them.
    private static readonly System.Func<System.IO.Stream, global::app.data.@this, global::app.actor.context.@this,
        global::app.View?, System.Text.Encoding?, System.Threading.CancellationToken,
        System.Threading.Tasks.Task<global::app.data.@this>>? _encode =
        typeof(T).GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.IEncode<>)) is { } encodes
            ? typeof(T).GetInterfaceMap(encodes).TargetMethods[0].CreateDelegate<System.Func<System.IO.Stream, global::app.data.@this,
                global::app.actor.context.@this, global::app.View?, System.Text.Encoding?, System.Threading.CancellationToken,
                System.Threading.Tasks.Task<global::app.data.@this>>>()
            : null;

    // How T reads its formats — its IDecode, bound when a format of T first reads; null when T's content is born
    // lazily, through its type's create.
    private static readonly System.Func<byte[], global::app.actor.context.@this, string, global::app.type.item.path.@this?,
        System.Threading.Tasks.Task<global::app.data.@this>>? _decode =
        typeof(T).GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.IDecode<>)) is { } decodes
            ? typeof(T).GetInterfaceMap(decodes).TargetMethods[0].CreateDelegate<System.Func<byte[], global::app.actor.context.@this,
                string, global::app.type.item.path.@this?, System.Threading.Tasks.Task<global::app.data.@this>>>()
            : null;

    public @this(global::app.Attributes.FormatAttribute format, global::app.type.@this owner) : base(format, owner) { }

    /// <summary>Born from its bytes unless T reads its formats itself.</summary>
    internal override bool IsBorn => _decode == null;

    /// <summary>T writes this format itself when it writes its formats; else as any kind writes (its family).</summary>
    public override System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View? view = null,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
        => _encode != null ? _encode(stream, data, context, view, encoding, ct)
            : base.Encode(stream, data, context, view, encoding, ct);

    /// <summary>T reads this format itself when it reads its formats (a .pr into a goal) — but never a template: a
    /// template's content is born through its type's create, as any kind's.</summary>
    public override System.Threading.Tasks.Task<global::app.data.@this> Decode(byte[] raw,
        global::app.actor.context.@this context, string name = "", global::app.View view = global::app.View.Out,
        System.Threading.CancellationToken ct = default, global::app.type.item.template.kind.@this? template = null,
        global::app.type.item.path.@this? origin = null)
        => _decode != null && template == null ? _decode(raw, context, name, origin)
            : base.Decode(raw, context, name, view, ct, template, origin);
}
