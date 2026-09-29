namespace PLang.Tests.App.Modules.file;

// A build warns through its action's one door: a file.read of a missing file of a known format writes
// {action, message} on the builder channel, naming file.read; an unknown format expects nothing.
public class ReadBuildWarningTests
{
    private static (global::app.@this app, System.IO.MemoryStream warnings) App()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "readwarn-" + System.Guid.NewGuid().ToString("N")[..6]);
        System.IO.Directory.CreateDirectory(root);
        var app = TestApp.Create(root);
        var warnings = new System.IO.MemoryStream();
        app.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this(
            "builder", warnings, global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain" });
        return (app, warnings);
    }

    [Test] public async Task AMissingFileOfAKnownFormat_WarnsNamingTheAction()
    {
        var (app, warnings) = App();
        await using var _ = app;
        var ctx = app.actor.list.User.Context;
        var read = Make.Action("file", "read", ("Path", "missing.json"));

        var failed = await read.Build(ctx);

        await Assert.That(failed).IsNull();
        var written = System.Text.Encoding.UTF8.GetString(warnings.ToArray());
        await Assert.That(written).Contains("file.read");
        await Assert.That(written).Contains("NotFound");
        await Assert.That(written).Contains("does not exist on disk");
    }

    [Test] public async Task AnUnknownFormat_ExpectsNothing()
    {
        var (app, warnings) = App();
        await using var _ = app;
        var ctx = app.actor.list.User.Context;
        var read = Make.Action("file", "read", ("Path", "missing.xyz"));

        await Assert.That(await read.Build(ctx)).IsNull();
        await Assert.That(warnings.Length).IsEqualTo(0);
    }
}
