namespace PLang.Tests;

/// <summary>
/// A snapshot to its text and back, through the doors production takes: the text a file save writes of it, and the
/// snapshot a read of that text is asked as.
/// </summary>
public static class SnapshotTextExtensions
{
    /// <summary>What <paramref name="snapshot"/> writes in text — what a file save of it holds.</summary>
    public static async Task<string> Text(this global::app.snapshot.@this snapshot, global::app.actor.context.@this context)
    {
        using var ms = new System.IO.MemoryStream();
        var written = await context.App.type.list["text"].kind.Encode(ms, new global::app.data.@this("", snapshot, context: context), context);
        if (!written.Success) throw new global::app.error.AppException(written.Error!);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>What the snapshot <paramref name="snapshot"/> answers writes in text.</summary>
    public static async Task<string> Text(this Task<global::app.snapshot.@this> snapshot, global::app.actor.context.@this context)
        => await (await snapshot).Text(context);

    /// <summary>The snapshot <paramref name="text"/> is, asked as one — the door <c>resume %snapshot%</c> takes.</summary>
    public static async Task<global::app.snapshot.@this> Snapshot(this global::app.actor.context.@this context, string text)
    {
        var data = new global::app.data.@this("", text, context: context);
        return await data.Value<global::app.snapshot.@this>() ?? throw new global::app.error.AppException(data.Error!);
    }
}
