namespace app.type.item;

/// <summary>
/// A type that writes its formats: the value a Data holds, onto a stream, in the given view — or, given none,
/// the format's own face. A format kind
/// the type declares encodes through this — bound once, when the kind is held on its type. A type that
/// doesn't implement it writes none of its formats.
/// </summary>
public interface IEncode<TSelf> where TSelf : @this, IEncode<TSelf>
{
    static abstract System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream, global::app.data.@this data,
        global::app.actor.context.@this context, global::app.View? view, System.Text.Encoding? encoding,
        System.Threading.CancellationToken ct);
}
