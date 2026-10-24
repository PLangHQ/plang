using Verb = global::app.type.item.permission.Verb;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// What each actor stands on its app's folders, as its role answers: the system reads, runs, writes and deletes in the
/// runtime's shared os folder and reads, writes and deletes in the stores' folder; a user reads and runs in the os folder
/// and nothing more. Each verb on each folder, on the folder itself and under it, is pinned both ways.
public class StandingTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/standing", global::PLang.Tests.Parent.Actor).Testing();

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    // Every verb the actor's view allows at place — on the folder, and under it.
    private static async Task<string> Stands(global::app.actor.@this actor, global::app.type.item.path.@this folder)
    {
        var allowed = new List<string>();
        foreach (var at in new[] { folder.Absolute, folder.Absolute + System.IO.Path.DirectorySeparatorChar + "x" })
            foreach (var verb in System.Enum.GetValues<Verb>())
                if (await actor.Permission.Find(at, verb) != null) allowed.Add((at == folder.Absolute ? "" : "under ") + verb);
        return string.Join(", ", allowed);
    }

    [Test]
    public async Task TheSystem_StandsOnTheOsFolderAndTheStoresFolder()
    {
        var system = _app.actor.list.System;

        await Assert.That(await Stands(system, _app.filesystem.os))
            .IsEqualTo("read, write, delete, execute, under read, under write, under delete, under execute");
        await Assert.That(await Stands(system, _app.filesystem.store))
            .IsEqualTo("read, write, delete, under read, under write, under delete");
    }

    [Test]
    public async Task AUser_ReadsAndRunsInTheOsFolder_AndNothingMore()
    {
        var user = _app.actor.list.User;

        await Assert.That(await Stands(user, _app.filesystem.os)).IsEqualTo("read, execute, under read, under execute");
        await Assert.That(await Stands(user, _app.filesystem.store)).IsEqualTo("");
    }
}
