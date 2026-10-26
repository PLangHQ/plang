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
        // a goal is where its .pr is found: read as found in this app, at the .pr its path builds to
        var found = goal.Pr?.Path is { } pr ? global::app.type.item.path.@this.Resolve(pr.Raw, app.actor.list.User.Context) : null;
        return await Read(app, await app.actor.list.User.Context.Pr(goal), found);
    }

    /// <summary>A .pr's text read the way the runtime reads one off I/O. Found at <paramref name="origin"/>, it is read
    /// as a file's content is (the goal is there); with none, through a stream channel, mime
    /// <c>application/plang-goal</c> — found nowhere, it has no path.</summary>
    public static async Task<global::app.goal.@this> Read(global::app.@this app, string prJson,
        global::app.type.item.path.@this? origin = null)
    {
        if (origin != null)
        {
            var context = app.actor.list.User.Context;
            var found = await app.type.list.Mime("application/plang-goal").Decode(
                System.Text.Encoding.UTF8.GetBytes(prJson), context, origin: origin);
            if (!found.Success)
                throw new System.InvalidOperationException($"RealGoalLoad: read failed — {found.Error?.Message}");
            return ((await found.Value()) as global::app.goal.@this)!;
        }
        var ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(prJson));
        var channel = new global::app.channel.type.stream.@this(app.actor.list.User.Channel,
            "real-load", ms, global::app.channel.ChannelDirection.Input, ownsStream: true)
        {
            Mime = "application/plang-goal",
        };
        app.actor.list.User.Channel.Register(channel);

        var read = await channel.Read();
        if (!read.Success)
            throw new System.InvalidOperationException(
                $"RealGoalLoad: channel read failed — {read.Error?.Message}");

        // goal is a plang item now — the load answers the goal itself, not a clr<goal> carrier.
        return ((await read.Value()) as global::app.goal.@this)!;
    }
}
