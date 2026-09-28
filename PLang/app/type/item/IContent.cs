namespace app.type.item;

/// <summary>
/// A reference to content somewhere else — a <c>file</c>, a <c>url</c>. Its value is the content decoded by
/// its format; <see cref="Content"/> is the content as it is, the bytes the reference samples once (through
/// its location's gate) and decodes from.
/// </summary>
public interface IContent
{
    /// <summary>The raw bytes, read once, as the asker whose <paramref name="context"/> it is.</summary>
    System.Threading.Tasks.Task<global::app.data.@this<global::app.type.item.binary.@this>> Content(global::app.actor.context.@this context);
}
