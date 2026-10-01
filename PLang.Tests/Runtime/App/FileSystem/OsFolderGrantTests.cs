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

        await (await Authorize(app, app.actor.list.User, Verb.read)).IsSuccess();
        await (await Authorize(app, app.actor.list.User, Verb.execute)).IsSuccess();
    }

    [Test]
    public async Task AUserWritingOrDeletingInTheOsFolder_IsRefused()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-os-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();

        var write = await Authorize(app, app.actor.list.User, Verb.write);
        var delete = await Authorize(app, app.actor.list.User, Verb.delete);

        await write.IsFailure();
        await Assert.That(write.Error!.Key).IsEqualTo("PermissionDenied");
        await delete.IsFailure();
    }

    [Test]
    public async Task TheSystemWritesInTheOsFolder()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-os-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();

        await (await Authorize(app, app.actor.list.System, Verb.write)).IsSuccess();
        await (await Authorize(app, app.actor.list.System, Verb.delete)).IsSuccess();
    }

    // A new file named the plang way, /system/<folder>/x, where the os has that folder and the app doesn't: it
    // resolves into the os folder — and the os grants rule it there: a user writing it is refused (headless; asked
    // with a person), the system writes it (PlangOS's shell update runs as the system).
    [Test]
    public async Task ANewSystemFile_ResolvedIntoTheOsFolder_IsTheSystemsToWrite()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-os-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var folder = "grant-" + System.Guid.NewGuid().ToString("N")[..8];
        var system = System.IO.Path.Join(app.OsAbsolutePath, "system", folder);
        System.IO.Directory.CreateDirectory(system);
        try
        {
            var user = global::app.type.item.path.@this.Resolve($"/system/{folder}/new.md", app.actor.list.User.Context);
            var asSystem = global::app.type.item.path.@this.Resolve($"/system/{folder}/new.md", app.actor.list.System.Context);
            await Assert.That(user.Absolute.StartsWith(system)).IsTrue();

            var write = await user.Authorize(Verb.Write, app.actor.list.User.Context);
            await write.IsFailure();
            await Assert.That(write.Error!.Key).IsEqualTo("PermissionDenied");
            await (await asSystem.Authorize(Verb.Write, app.actor.list.System.Context)).IsSuccess();
        }
        finally { System.IO.Directory.Delete(system, true); }
    }

    // An app rooted at the os folder itself doesn't make the folder its user's: the os grants still rule there.
    [Test]
    public async Task AUserInsideTheOsApp_StillCantWriteTheOsFolder()
    {
        await using var probe = new global::app.@this(System.IO.Path.GetTempPath()).Testing();
        await using var app = new global::app.@this(probe.OsAbsolutePath).Testing();

        var write = await Authorize(app, app.actor.list.User, Verb.write);

        await write.IsFailure();
        await Assert.That(write.Error!.Key).IsEqualTo("PermissionDenied");
    }
}
