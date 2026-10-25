namespace PLang.Tests.App.FileSystem.PermissionTests;

/// A sub app is made in its maker's folder or under it, never in its maker's stores folder or under it: there the
/// maker's stores would be plain in-root for it. Checking the immediate maker's stores is enough down a chain of makers.
public class SubAppRootTests : System.IAsyncDisposable
{
    private readonly string _folder = System.IO.Directory.CreateDirectory(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-subroot-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
    private readonly global::app.@this _top;

    public SubAppRootTests() => _top = new global::app.@this(_folder, global::PLang.Tests.Parent.Actor).Testing();

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await _top.DisposeAsync();
        try { System.IO.Directory.Delete(_folder, recursive: true); } catch (System.IO.IOException) { }
    }

    private static async Task Refused(string root, global::app.actor.@this maker)
    {
        var refused = await Assert.ThrowsAsync<global::app.error.AppException>(() =>
        {
            _ = new global::app.@this(root, maker);
            return Task.CompletedTask;
        });
        await Assert.That(refused!.Error.Key).IsEqualTo("OutsideItsMaker");
        await Assert.That(refused.Error.Message).Contains(".data");
    }

    [Test]
    [Arguments(".data")]
    [Arguments(".data/setting")]
    public async Task ASubApp_IsNeverMadeInItsMakersStores(string tail)
        => await Refused(System.IO.Path.Combine(_folder, tail), _top.actor.list.User);

    [Test]
    public async Task ASubAppOfASubApp_AtASharedRoot_IsNeverMadeInTheTopsStores()
    {
        // the middle app shares the top's root, so its stores folder is the top's
        await using var middle = new global::app.@this(_folder, _top.actor.list.User);

        await Refused(System.IO.Path.Combine(_folder, ".data"), middle.actor.list.User);
    }

    [Test]
    public async Task TheMakersOwnRoot_AndAFolderBesideItsStores_AreTaken()
    {
        await using var same = new global::app.@this(_folder, _top.actor.list.User);
        await using var beside = new global::app.@this(System.IO.Path.Combine(_folder, "data"), _top.actor.list.User);
    }
}
