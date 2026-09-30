namespace app.type.item;

/// <summary>
/// A type that reads its formats: content in one of them (a file's bytes), made into a value of the type — the
/// mirror of <see cref="IEncode{TSelf}"/>. A format kind the type declares decodes through this, bound once when
/// the kind is held on its type. A type that doesn't implement it has its content born lazily, parsed when used.
/// </summary>
public interface IDecode<TSelf> where TSelf : @this, IDecode<TSelf>
{
    static abstract System.Threading.Tasks.Task<global::app.data.@this> Decode(byte[] raw,
        global::app.actor.context.@this context, string name, global::app.type.item.path.@this? origin);
}
