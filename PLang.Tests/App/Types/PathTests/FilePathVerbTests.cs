using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.path.file.@this;
using StatInfo = global::app.type.path.@this.StatInfo;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// FilePath verb round-trips. In-root paths auto-grant Authorize.
/// </summary>
public class FilePathVerbTests
{
    private static (global::app.@this app, string root) MakeApp()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-fpv-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        return (new global::app.@this(root), root);
    }

    private static FilePath At(global::app.@this app, string root, string name) =>
        new(System.IO.Path.Combine(root, name), app.User.Context);

    [Test] public async Task WriteText_ThenReadText_RoundTrips()
    {
        var (app, root) = MakeApp();
        var p = At(app, root, "rt.txt");
        var w = await p.WriteText("round-trip");
        await Assert.That(w.Success).IsTrue();
        var r = await p.ReadText();
        await Assert.That(r.Success).IsTrue();
        await Assert.That(r.Value).IsEqualTo("round-trip");
    }

    [Test] public async Task WriteBytes_ThenReadBytes_RoundTrips()
    {
        var (app, root) = MakeApp();
        var p = At(app, root, "rt.bin");
        var bytes = new byte[] { 1, 2, 3, 9, 8, 7 };
        await p.WriteBytes(bytes);
        var r = await p.ReadBytes();
        await Assert.That(r.Success).IsTrue();
        await Assert.That((byte[])r.Value!).IsEquivalentTo(bytes);
    }

    [Test] public async Task Exists_FalseBeforeWrite_TrueAfterWrite()
    {
        var (app, root) = MakeApp();
        var p = At(app, root, "ex.txt");
        var before = await p.ExistsAsync();
        await Assert.That(before.Success).IsTrue();
        await Assert.That(before.Value).IsEqualTo(false);
        await p.WriteText("now exists");
        var after = await p.ExistsAsync();
        await Assert.That(after.Value).IsEqualTo(true);
    }

    [Test] public async Task AsBooleanAsync_FalseBeforeWrite_TrueAfterWrite()
    {
        // path truthiness is "does it exist" — the dispatch target for
        // `if %path% exists`.
        var (app, root) = MakeApp();
        var p = At(app, root, "asbool.txt");
        await Assert.That(await p.AsBooleanAsync()).IsFalse();
        await p.WriteText("now exists");
        await Assert.That(await p.AsBooleanAsync()).IsTrue();
    }

    [Test] public async Task Delete_RemovesFile_ExistsBecomesFalse()
    {
        var (app, root) = MakeApp();
        var p = At(app, root, "del.txt");
        await p.WriteText("to delete");
        var d = await p.Delete();
        await Assert.That(d.Success).IsTrue();
        var ex = await p.ExistsAsync();
        await Assert.That(ex.Value).IsEqualTo(false);
    }

    [Test] public async Task Append_AddsToExistingContent()
    {
        var (app, root) = MakeApp();
        var p = At(app, root, "ap.txt");
        await p.WriteText("abc");
        await p.Append("def");
        var r = await p.ReadText();
        await Assert.That(r.Value).IsEqualTo("abcdef");
    }

    [Test] public async Task Stat_ReportsLength_MatchingWrittenBytes()
    {
        var (app, root) = MakeApp();
        var p = At(app, root, "st.txt");
        await p.WriteText("12345");
        var s = await p.Stat();
        await Assert.That(s.Success).IsTrue();
        var info = (StatInfo)s.Value!;
        await Assert.That(info.Exists).IsTrue();
        await Assert.That(info.IsFile).IsEqualTo(true);
        await Assert.That(info.Length).IsEqualTo(5L);
    }

    [Test] public async Task Stat_NonexistentPath_ReportsExistsFalse_StillSuccess()
    {
        var (app, root) = MakeApp();
        var p = At(app, root, "nope.txt");
        var s = await p.Stat();
        await Assert.That(s.Success).IsTrue();
        var info = (StatInfo)s.Value!;
        await Assert.That(info.Exists).IsFalse();
    }

    [Test] public async Task List_ReturnsDirectoryEntries()
    {
        var (app, root) = MakeApp();
        await At(app, root, "a.txt").WriteText("a");
        await At(app, root, "b.txt").WriteText("b");
        var dir = new FilePath(root, app.User.Context);
        var list = await dir.List();
        await Assert.That(list.Success).IsTrue();
        var entries = list.Value!;
        await Assert.That(entries.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test] public async Task WriteText_CreatesMissingParentDirectory()
    {
        var (app, root) = MakeApp();
        var p = new FilePath(System.IO.Path.Combine(root, "newsub", "deep", "f.txt"), app.User.Context);
        var w = await p.WriteText("nested");
        await Assert.That(w.Success).IsTrue();
        var r = await p.ReadText();
        await Assert.That(r.Value).IsEqualTo("nested");
    }

    [Test] public async Task ReadText_NonexistentFile_ReturnsFail_DoesNotThrow()
    {
        var (app, root) = MakeApp();
        var p = At(app, root, "ghost.txt");
        var r = await p.ReadText();
        await Assert.That(r.Success).IsFalse();
    }
}
