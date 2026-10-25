using app.module.file;
using PLangPath = global::app.type.item.path.@this;
using PLangFilePath = global::app.type.item.path.file.@this;

namespace PLang.Tests.App.CompareRedesign;

// Stage 3 — `read X` yields a reference (`file`/`directory`/`url`); content
// is lazy. Examining the content narrows the value to its content type — same
// `Data` instance, `.Type` mutated in place, prior type retained in the
// `.Is()` chain. `!` resolves chain-wide, never headline-only. Single-storage:
// the parsed item replaces the raw, there is no `_raw` alongside.
public class Stage3_ReferenceNarrowTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;

    public Stage3_ReferenceNarrowTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_stage3_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir, global::PLang.Tests.Parent.Actor).Testing();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private string TempPath(string rel) => System.IO.Path.Combine(_tempDir, rel);

    private global::app.data.@this<PLangPath> MakePath(string rel) =>
        new("", _app.actor.list.User.Context.File(TempPath(rel)), context: _app.actor.list.User.Context);

    private async Task<Data> Read(string rel)
    {
        var action = new Read(_app.actor.list.User.Context) { Path = MakePath(rel) };
        var result = await action.Start();
        await result.IsSuccess();
        return result;
    }

    private Data JsonFile(string name = "config.json", string content = "{\"database\":\"plang\",\"port\":8080}")
    {
        System.IO.File.WriteAllText(TempPath(name), content);
        return Read(name).GetAwaiter().GetResult();
    }

    [Test]
    public async Task ReadLocalFile_ReturnsFileType_ChainIsFilePathItem()
    {
        System.IO.File.WriteAllText(TempPath("file.txt"), "hello");
        var result = await Read("file.txt");
        await Assert.That(result.Type!.Name).IsEqualTo("file");
        await Assert.That(result.Peek()).IsTypeOf<global::app.type.item.file.@this>();
        // is-a chain answered by the VALUE from its type history (file born from path): file is-a
        // path is-a item; content untouched.
        await Assert.That(result.Is("file")).IsTrue();
        await Assert.That(result.Is("path")).IsTrue();
        await Assert.That(result.Is("item")).IsTrue();
    }

    [Test]
    public async Task ReadHttpUrl_ReturnsUrlType_NotFile()
    {
        // remote scheme routes to `url` with NO fetch — pure construction
        var http = _app.actor.list.User.Context.Url("http://example.com/data.json");
        var action = new Read(_app.actor.list.User.Context) { Path = new global::app.data.@this<PLangPath>("", http, context: _app.actor.list.User.Context) };
        var result = await action.Start();
        await result.IsSuccess();
        await Assert.That(result.Type!.Name).IsEqualTo("url");
        var reference = (global::app.type.item.url.@this)result.Peek()!;
        await Assert.That(reference.IsLoaded).IsFalse();
        await Assert.That(reference.Host).IsEqualTo("example.com");
    }

    [Test]
    public async Task ContentKindInference_CsvExtension_NarrowsToTableOrList()
    {
        System.IO.File.WriteAllText(TempPath("report.csv"), "name,age\nAda,42\n");
        var data = await Read("report.csv");
        await Assert.That(data.Type!.kind.Name).IsEqualTo("csv");
        var op = new global::app.data.Operator("is");
        var right = new Data("", "table", context: _app.actor.list.User.Context);
        var isTable = (await op.Evaluate(data, right, _app.actor.list.User.Context)).ToBoolean();
        var rightList = new Data("", "list", context: _app.actor.list.User.Context);
        var isList = (await op.Evaluate(data, rightList, _app.actor.list.User.Context)).ToBoolean();
        await Assert.That(isTable || isList).IsTrue()
            .Because("csv content narrows to table (or list)");
    }

    [Test]
    public async Task ReadUnknownLocalExtension_StaysGenericFile()
    {
        System.IO.File.WriteAllBytes(TempPath("blob.zzz"), new byte[] { 1, 2, 3 });
        var result = await Read("blob.zzz");
        await Assert.That(result.Type!.Name).IsEqualTo("file");
        await Assert.That(result.Peek()).IsTypeOf<global::app.type.item.file.@this>();
        // unknown extension: content is a binary value (raw bytes, no text decode)
        var content = await result.Value();
        await Assert.That(content).IsTypeOf<global::app.type.item.binary.@this>();
        await Assert.That(((global::app.type.item.binary.@this)content!).Value).IsEquivalentTo(new byte[] { 1, 2, 3 });
    }

    // (ContentKindInference_JsonExtension_ConvertsToDictOnAsk retired — it tested the deleted clr(json)
    //  navigator's explicit json→dict Convert door. A json value now narrows to dict by opening it on first
    //  navigation (787); that is covered by JsonLazyNavigationTests and JsonPropertyAccessMaterializesTests.)

    // (BangFileBangPath_ResolvesWithoutReading_MaterializeCountZero deleted — the %x!file% value-nav
    //  it tested was retired with Facet; the metadata surface returns as the standardized !info,
    //  where its tests will be added.)

    // (BangFileBangPath_ResolvesOnUnNarrowed_AND_Narrowed_Branches deleted — same retirement as above:
    //  the %x!file% value-nav returns as the standardized !info.)

    [Test]
    public async Task TerminalTypes_ImageAndDirectory_DoNotNarrow()
    {
        // directory: content type known up-front (list<path>), stays the headline
        System.IO.Directory.CreateDirectory(TempPath("sub"));
        System.IO.File.WriteAllText(TempPath("sub/a.txt"), "a");
        var dir = await Read("sub");
        await Assert.That(dir.Type!.Name).IsEqualTo("directory");
        await Assert.That(dir.Peek()).IsTypeOf<global::app.type.item.directory.@this>();
        // image: eager specialisation, headline image (1x1 px PNG)
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        System.IO.File.WriteAllBytes(TempPath("dot.png"), png);
        var img = await Read("dot.png");
        // Content off I/O is binary/lazy — the value parses (narrows) only on access.
        await img.Value();
        await Assert.That(img.Type!.Name).IsEqualTo("image");
        await Assert.That(img.Peek()).IsTypeOf<global::app.type.item.image.@this>();
    }
}
