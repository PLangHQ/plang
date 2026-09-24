namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// A <c>file://</c> URL names an OS location: it resolves to its local path and shows in plang
/// form — "/x" under the app root, "//x" anywhere else — never as a root-relative "file:" folder.
/// </summary>
public class FileUrlTests : System.IAsyncDisposable
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "plang-fileurl-" + System.Guid.NewGuid().ToString("N")[..8]);
    private readonly global::app.@this _app;

    public FileUrlTests() => _app = TestApp.Create(_root);

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task FileUrl_OutOfRoot_IsItsLocalPath_ShownInPlangForm()
    {
        var p = global::app.type.item.path.@this.Resolve("file:///var/plang-elsewhere/x.txt", _app.User.Context);

        await Assert.That(p).IsTypeOf<global::app.type.item.path.file.@this>();
        await Assert.That(p.Absolute).IsEqualTo("/var/plang-elsewhere/x.txt");
        await Assert.That(p.Raw).IsEqualTo("//var/plang-elsewhere/x.txt");
    }

    [Test]
    public async Task FileUrl_UnderRoot_ShowsRootRelative()
    {
        var p = global::app.type.item.path.@this.Resolve("file://" + _root + "/data/a.txt", _app.User.Context);

        await Assert.That(p.Absolute).IsEqualTo(_root + "/data/a.txt");
        await Assert.That(p.Raw).IsEqualTo("/data/a.txt");
    }

    [Test]
    public async Task PlangRootedPath_IsNotAFileUrl()
    {
        var p = global::app.type.item.path.@this.Resolve("/data/a.txt", _app.User.Context);

        await Assert.That(p.Absolute).IsEqualTo(_root + "/data/a.txt");
    }
}
