namespace PLang.Tests.App.Decider;

// A goal's steps are built inside the goal's frame, as they run: what a step names relative to its goal is
// found where the goal is — not where the builder's own goal is.
public class BuildInGoalFrameTests
{
    [Test]
    public async Task ARelativeFile_BesideTheGoal_IsFoundAtBuild()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_frame_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "sub"));
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, "sub", "data.json"), "{\"a\":1}");
        await using var app = new global::app.@this(root, autoWireConsoleChannels: false).Testing();
        var context = app.actor.list.User.Context;
        var warned = new MemoryStream();
        app.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this("builder", warned,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "application/json" });

        var goal = global::app.goal.@this.Parse("Start\n- file.read(Path=\"data.json\")\n",
            global::app.type.item.path.@this.Resolve("/sub/Start.goal", context), context)!;
        var refused = await goal.Step.Read("", context);

        await Assert.That(refused?.Message).IsNull();
        await Assert.That(System.Text.Encoding.UTF8.GetString(warned.ToArray())).DoesNotContain("does not exist");
    }
}
