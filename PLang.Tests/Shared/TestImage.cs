namespace PLang.Tests;

/// <summary>An image made the way a program's makers make one: born holding the app's image type of its kind, as
/// <paramref name="context"/>'s types hold it.</summary>
public static class TestImage
{
    /// <summary>Bytes in hand, of the kind named (<c>png</c>, or a MIME, <c>image/png</c>); the plain image type when
    /// none is named.</summary>
    public static global::app.type.item.image.@this Image(this global::app.actor.context.@this context, byte[] bytes, string? kind = null)
        => new(bytes, context.App.type.list[new global::app.type.@this("image", kind), context]);

    /// <summary>A path-backed image (its bytes, when given, already read), of the type its extension names.</summary>
    public static global::app.type.item.image.@this Image(this global::app.actor.context.@this context,
        global::app.type.item.path.@this path, byte[]? bytes = null)
    {
        var type = path.Kind(context) is { } named && named.Is("image")
            ? named : context.App.type.list[typeof(global::app.type.item.image.@this)];
        return bytes == null ? new(path, type) : new(bytes, path, type);
    }
}
