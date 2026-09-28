namespace PLang.Tests;

/// <summary>
/// A path read the way a program reads it: <c>path.Read</c> lands the reference, and touching it reads its
/// content through the path's gate. The answer is the landed Data — its value the decoded content, or its
/// error the gate's / the read's.
/// </summary>
public static class PathTouch
{
    public static async Task<global::app.data.@this> Touch(this global::app.type.item.path.@this path,
        global::app.actor.context.@this context)
    {
        var landed = await path.Read(context);
        if (landed.Success) await landed.Value();
        return landed;
    }

    /// <summary>The path's bytes decoded by the format its extension names — lazy Data, raw-backed and
    /// unparsed until touched, as the file's value door decodes it.</summary>
    public static async Task<global::app.data.@this> Decoded(this global::app.type.item.path.@this path,
        global::app.actor.context.@this context)
    {
        var bytes = await path.Bytes(context);
        if (!bytes.Success) return bytes;
        return await context.App.type.list.Mime(path.MimeType(context)).Decode((await bytes.Value())!.Value, context, path.Raw);
    }
}
