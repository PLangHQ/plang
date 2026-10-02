namespace PLang.Tests.App.Types.PathTests;

// A /system/ path is the app's own when the app has it, else the os's — and it names both, the app's first, whichever
// it resolved to (the one overlay, on the file path).
public class SystemPlaceTests : System.IAsyncDisposable
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sysplace-" + System.Guid.NewGuid().ToString("N")[..8]);
    private readonly global::app.@this _app;

    public SystemPlaceTests()
    {
        System.IO.Directory.CreateDirectory(_root);
        _app = new global::app.@this(_root).Testing();
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        System.IO.Directory.Delete(_root, recursive: true);
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;
    private string Os(string relative) => System.IO.Path.GetFullPath(System.IO.Path.Combine(_app.OsAbsolutePath, relative));
    private string Own(string relative) => System.IO.Path.GetFullPath(System.IO.Path.Combine(_root, relative));

    [Test] public async Task WithoutTheAppsCopy_ItIsTheOss_AndNamesBoth()
    {
        var at = global::app.type.item.path.@this.Resolve("/system/error/400.txt", Ctx);

        await Assert.That(at.Absolute).IsEqualTo(Os("system/error/400.txt"));
        await Assert.That(at.Place(Ctx).Select(p => p.Absolute)).IsEquivalentTo(new[] { Own("system/error/400.txt"), Os("system/error/400.txt") });
    }

    [Test] public async Task WithTheAppsCopy_ItIsTheApps_AndNamesBoth()
    {
        System.IO.Directory.CreateDirectory(Own("system/error"));
        System.IO.File.WriteAllText(Own("system/error/400.txt"), "mine");

        var at = global::app.type.item.path.@this.Resolve("/system/error/400.txt", Ctx);

        await Assert.That(at.Absolute).IsEqualTo(Own("system/error/400.txt"));
        await Assert.That(at.Place(Ctx).First().Absolute).IsEqualTo(Own("system/error/400.txt"));
    }

    [Test] public async Task AnyOtherPath_IsTheOnePlaceItIs()
    {
        var at = global::app.type.item.path.@this.Resolve("/data/x.txt", Ctx);

        await Assert.That(at.Place(Ctx).Select(p => p.Absolute)).IsEquivalentTo(new[] { Own("data/x.txt") });
    }
}
