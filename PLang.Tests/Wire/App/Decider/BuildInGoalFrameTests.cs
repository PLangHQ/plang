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
        await using var app = new global::app.@this(root, autoWireConsoleChannels: false).Testing().Building();
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

    // A goal that saves a file, then reads it: the read finds it at run, so the build says nothing — while a path
    // nothing writes (a typo) still warns.
    [Test]
    [Arguments("note.txt", false)]
    [Arguments("nots.txt", true)]
    public async Task AFileTheGoalWritesFirst_IsNoWarning_ATypoIs(string read, bool warns)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_written_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        await using var app = new global::app.@this(root, autoWireConsoleChannels: false).Testing().Building();
        var context = app.actor.list.User.Context;
        var warned = new MemoryStream();
        app.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this("builder", warned,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "application/json" });

        var goal = global::app.goal.@this.Parse(
            $"Start\n- file.save(Path=\"note.txt\", Value=\"Buy milk\")\n- file.read(Path=\"{read}\")\n",
            global::app.type.item.path.@this.Resolve("/Start.goal", context), context)!;
        var refused = await goal.Step.Read("", context);

        await Assert.That(refused?.Message).IsNull();
        await Assert.That(System.Text.Encoding.UTF8.GetString(warned.ToArray()).Contains("does not exist")).IsEqualTo(warns);
    }

    // The build checks its goals over its own files: the file a step saves is there, and the disk is untouched.
    [Test]
    public async Task AFileTheGoalSaves_IsInTheBuildsFiles_NotOnDisk()
    {
        var (app, root, _) = Building("plang_overlay_");
        await using var _app = app;
        var context = app.actor.list.User.Context;

        var goal = global::app.goal.@this.Parse("Start\n- file.save(Path=\"note.txt\", Value=\"Buy milk\")\n",
            global::app.type.item.path.@this.Resolve("/Start.goal", context), context)!;
        await Assert.That((await goal.Step.Read("", context))?.Message).IsNull();

        var note = new global::app.type.item.path.file.@this(System.IO.Path.Combine(root, "note.txt"));
        await Assert.That(app.Build!.Files.IsFile(note)).IsTrue();
        await Assert.That(System.IO.File.Exists(note.Absolute)).IsFalse();
    }

    // A file a test adds to the build's files is found at build, as one on disk is.
    [Test]
    public async Task AMockFile_IsFoundAtBuild()
    {
        var (app, root, warned) = Building("plang_mock_");
        await using var _app = app;
        var context = app.actor.list.User.Context;
        app.Build!.Files.Add(new global::app.type.item.path.file.@this(System.IO.Path.Combine(root, "mock.txt")));

        var goal = global::app.goal.@this.Parse("Start\n- file.read(Path=\"mock.txt\")\n",
            global::app.type.item.path.@this.Resolve("/Start.goal", context), context)!;
        await Assert.That((await goal.Step.Read("", context))?.Message).IsNull();
        await Assert.That(System.Text.Encoding.UTF8.GetString(warned.ToArray())).DoesNotContain("does not exist");
    }

    // The build's files last the whole build: a file one goal deletes is gone for a goal checked after it — and
    // still on disk.
    [Test]
    public async Task AFileAGoalDeletes_IsGoneForALaterGoal_AndStillOnDisk()
    {
        var (app, root, warned) = Building("plang_gone_");
        await using var _app = app;
        var context = app.actor.list.User.Context;
        var old = System.IO.Path.Combine(root, "old.txt");
        await System.IO.File.WriteAllTextAsync(old, "old");

        var deletes = global::app.goal.@this.Parse("Clean\n- file.delete(Path=\"old.txt\")\n",
            global::app.type.item.path.@this.Resolve("/Clean.goal", context), context)!;
        var reads = global::app.goal.@this.Parse("Start\n- file.read(Path=\"old.txt\")\n",
            global::app.type.item.path.@this.Resolve("/Start.goal", context), context)!;
        await Assert.That((await deletes.Step.Read("", context))?.Message).IsNull();
        await Assert.That((await reads.Step.Read("", context))?.Message).IsNull();

        await Assert.That(System.Text.Encoding.UTF8.GetString(warned.ToArray())).Contains("does not exist");
        await Assert.That(System.IO.File.Exists(old)).IsTrue();
    }

    // A building app at a fresh root, its builder channel captured.
    private static (global::app.@this app, string root, MemoryStream warned) Building(string prefix)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        var app = new global::app.@this(root, autoWireConsoleChannels: false).Testing().Building();
        var warned = new MemoryStream();
        app.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this("builder", warned,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "application/json" });
        return (app, root, warned);
    }
}
