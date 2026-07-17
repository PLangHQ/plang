namespace PLang.Tests.Shared;

/// <summary>
/// Loads a hand-built goal the way the runtime loads a <c>.pr</c> off I/O — instead
/// of running the in-C# <c>PrAction</c> shape that bypasses the read (the recurring
/// "tests load actions wrong" trap). A <c>.pr</c> is just bytes from a stream; this:
///
/// <list type="number">
///   <item>serializes the goal to the <c>.pr</c> wire shape (the builder's
///     <c>PrWrite</c>),</item>
///   <item>puts the bytes in a <see cref="System.IO.MemoryStream"/>,</item>
///   <item>feeds that stream through a <b>stream channel</b> (mime
///     <c>application/plang-goal</c>) — the exact read boundary a file/http channel
///     uses,</item>
///   <item>lets the channel stamp <c>{goal}</c> and materialize it through
///     <c>source.Value</c> — the same path <c>GoalCall</c> takes for a sub-goal.</item>
/// </list>
///
/// The loaded goal has its actions assembled and its params typed exactly like
/// production. (The %ref% template stamp rides this path only once the goal reader
/// routes through the authored read — see the branch summary.)
/// </summary>
public static class RealGoalLoad
{
    public static async Task<global::app.goal.@this> ViaChannel(
        global::app.@this app, global::app.goal.@this goal)
    {
        // Write the .pr the way the real builder now does — through goal.Output (Store), not STJ —
        // so the test exercises the actual write path, not the soon-to-be-deleted PrWrite.
        var serializer = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetOrDefault("application/plang");
        using var outMs = new System.IO.MemoryStream();
        await serializer.SerializeItemAsync(outMs, new global::app.type.clr.@this<global::app.goal.@this>(goal, app.User.Context), global::app.View.Store);
        var prJson = System.Text.Encoding.UTF8.GetString(outMs.ToArray());

        var ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(prJson));
        var channel = new global::app.channel.type.stream.@this(
            "real-load", ms, global::app.channel.ChannelDirection.Input, ownsStream: true)
        {
            Mime = "application/plang-goal",
        };
        app.User.Channel.Register(channel);

        var read = await channel.Read();
        if (!read.Success)
            throw new System.InvalidOperationException(
                $"RealGoalLoad: channel read failed — {read.Error?.Message}");

        // goal is a plang item now — the load answers the goal itself, not a clr<goal> carrier.
        return ((await read.Value()) as global::app.goal.@this)!;
    }
}
