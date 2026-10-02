namespace app.module.http.type.progress;

/// <summary>
/// PLang <c>progress</c> value — how far a body has moved: <c>received</c> (a download's) or <c>sent</c> (an upload's),
/// a size; <c>total</c>, the size the server said, null when it said none; <c>percent</c>, null with no total. A last
/// report always comes when the body is done; whether it went well is the report's Data, as every Data says.
/// </summary>
[global::app.Attributes.PlangType("progress")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "{received: \"40 MiB\", total: \"100 MiB\", percent: 40}";
    public static string Description => "How far a download or an upload has come: received (or sent), total and percent.";
    public static string Shape => "object";

    /// <summary>How much of a download has come — null on an upload.</summary>
    [Out] public global::app.type.item.size.@this? Received { get; }

    /// <summary>How much of an upload has gone — null on a download.</summary>
    [Out] public global::app.type.item.size.@this? Sent { get; }

    /// <summary>The size the server said the body is — null when it said none.</summary>
    [Out] public global::app.type.item.size.@this? Total { get; }

    /// <summary>How much of <see cref="Total"/> has moved, 0 to 100 — null with no total.</summary>
    [Out] public global::app.type.item.number.@this? Percent { get; }

    /// <summary><paramref name="moved"/> bytes of <paramref name="total"/>, received or <paramref name="sent"/>, written in
    /// the asker's size standard.</summary>
    public @this(long moved, long? total, bool sent, global::app.actor.context.@this context)
    {
        var size = new global::app.type.item.size.@this(moved, context);
        if (sent) Sent = size;
        else Received = size;
        Total = total is { } all ? new global::app.type.item.size.@this(all, context) : null;
        Percent = total is > 0 ? (global::app.type.item.number.@this)decimal.Round(moved * 100m / total.Value, 1) : null;
    }

    public override bool IsLeaf => false;

    /// <summary>A progress is reported by a transfer, never made from a value.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this progress) return progress;
        data.Fail(new global::app.error.Error("a progress is reported by a download or an upload, never made from a value",
            "CreateItemDeclined", 400));
        return null;
    }
}
