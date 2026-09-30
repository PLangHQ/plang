using Verb = global::app.type.item.permission.Verb;

namespace PLang.Tests.App.FileSystem;

// The runtime's shared os folder is no actor's own: every actor holds a standing grant to read and run what is
// there, and only the system actor to write or delete. Anyone else writing there is asked — headless, refused.
public class OsFolderGrantTests
{
    private static global::app.type.item.path.@this Under(global::app.@this app, string relative)
        => global::app.type.item.path.@this.Resolve(
            app.OsAbsolutePath + global::System.IO.Path.DirectorySeparatorChar + relative, app.actor.list.User.Context);

    private static async Task<global::app.data.@this> Authorize(global::app.@this app, global::app.actor.@this actor, Verb verb)
        => await Under(app, "system/builder/Probe.goal").Authorize(verb, actor.Context);

    [Test]
    public async Task AUserReadsAndRunsTheOsFolder_Silently()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-os-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();

        await (await Authorize(app, app.actor.list.User, Verb.Read)).IsSuccess();
        await (await Authorize(app, app.actor.list.User, Verb.Execute)).IsSuccess();
    }

    [Test]
    public async Task AUserWritingOrDeletingInTheOsFolder_IsRefused()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-os-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();

        var write = await Authorize(app, app.actor.list.User, Verb.Write);
        var delete = await Authorize(app, app.actor.list.User, Verb.Delete);

        await write.IsFailure();
        await Assert.That(write.Error!.Key).IsEqualTo("PermissionDenied");
        await delete.IsFailure();
    }

    [Test]
    public async Task TheSystemWritesInTheOsFolder()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-os-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();

        await (await Authorize(app, app.actor.list.System, Verb.Write)).IsSuccess();
        await (await Authorize(app, app.actor.list.System, Verb.Delete)).IsSuccess();
    }

    // An app rooted at the os folder itself doesn't make the folder its user's: the os grants still rule there.
    [Test]
    public async Task AUserInsideTheOsApp_StillCantWriteTheOsFolder()
    {
        await using var probe = new global::app.@this(System.IO.Path.GetTempPath()).Testing();
        await using var app = new global::app.@this(probe.OsAbsolutePath).Testing();

        var write = await Authorize(app, app.actor.list.User, Verb.Write);

        await write.IsFailure();
        await Assert.That(write.Error!.Key).IsEqualTo("PermissionDenied");
    }
}
