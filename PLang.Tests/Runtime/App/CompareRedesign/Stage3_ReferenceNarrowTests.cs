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
        _app = TestApp.Create(_tempDir);
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private string TempPath(string rel) => System.IO.Path.Combine(_tempDir, rel);

    private global::app.data.@this<PLangPath> MakePath(string rel) =>
        new("", new PLangFilePath(TempPath(rel), _app.User.Context), context: _app.User.Context);

    private async Task<Data> Read(string rel)
    {
        var action = new Read(_app.User.Context) { Path = MakePath(rel) };
        var result = await action.Run();
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
        // .Is() chain — file is-a path is-a item; content untouched
        await Assert.That(result.Type.Is("file")).IsTrue();
        await Assert.That(result.Type.Is("path")).IsTrue();
        await Assert.That(result.Type.Is("item")).IsTrue();
    }

    [Test]
    public async Task ReadHttpUrl_ReturnsUrlType_NotFile()
    {
        // remote scheme routes to `url` with NO fetch — pure construction
        var http = new global::app.type.item.path.http.@this("http://example.com/data.json", _app.User.Context) {};
        var action = new Read(_app.User.Context) { Path = new global::app.data.@this<PLangPath>("", http) };
        var result = await action.Run();
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
        await Assert.That(data.Type!.Kind?.Name).IsEqualTo("csv");
        var op = new global::app.module.condition.Operator("is");
        var right = new Data("", "table", context: _app.User.Context);
        var isTable = await op.Evaluate(data, right);
        var rightList = new Data("", "list", context: _app.User.Context);
        var isList = await op.Evaluate(data, rightList);
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

    [Test]
    public async Task ContentKindInference_JsonExtension_NarrowsToDict()
    {
        var data = JsonFile();
        await Assert.That(data.Type!.Kind?.Name).IsEqualTo("json");
        var child = await data.Get("database");
        await Assert.That((await child.Value())?.ToString()).IsEqualTo("plang");
        await Assert.That(data.Type!.Name).IsEqualTo("dict");
    }

    [Test]
    public async Task BangFileBangPath_ResolvesWithoutReading_MaterializeCountZero()
    {
        var data = JsonFile("untouched.json");
        var facet = await data.Get("!file");
        await Assert.That(facet.IsInitialized).IsTrue();
        var pathChild = await facet.Get("!path");
        await Assert.That(pathChild.Peek()).IsNotNull();
        // the property plane never read content: the reference is still unloaded
        // and the Data never narrowed
        var reference = (global::app.type.item.file.@this)data.Peek()!;
        await Assert.That(reference.IsLoaded).IsFalse();
        await Assert.That(data.Type!.Name).IsEqualTo("file");
    }

    [Test]
    public async Task DotField_OnFile_ReadsAndParsesAndNarrows_MaterializeCountOne()
    {
        var data = JsonFile("narrow1.json");
        var reference = (global::app.type.item.file.@this)data.Peek()!;
        await Assert.That(reference.IsLoaded).IsFalse();

        var child = await data.Get("port");
        await Assert.That((await child.Value())?.ToString()).IsEqualTo("8080");
        // narrowed: the parsed dict replaced the reference (single storage)
        await Assert.That(data.Type!.Name).IsEqualTo("dict");
        await Assert.That(data.Peek()).IsTypeOf<global::app.type.item.dict.@this>();
        // the sample stays on the reference — aliases and cached bindings that
        // did not rebind serve their next use from memory (one read per value
        // per program run)
        await Assert.That(reference.IsLoaded).IsTrue();
    }

    [Test]
    public async Task AfterNarrow_IsFile_AndIsDict_BothTrue_SameInstance()
    {
        var data = JsonFile("accum.json");
        await data.Get("database");
        // Provenance is the VALUE's — ask it, not a type entity that copied the chain.
        await Assert.That(data.Is("dict")).IsTrue();
        await Assert.That(data.Is("file")).IsTrue();
        await Assert.That(data.Is("item")).IsTrue();
    }

    [Test]
    public async Task NarrowMutatesSameDataInstance_NotReplaced()
    {
        var data = JsonFile("inplace.json");
        var before = data;
        await data.Get("database");
        await Assert.That(ReferenceEquals(before, data)).IsTrue();
        await Assert.That(before.Type!.Name).IsEqualTo("dict");
    }

    [Test]
    public async Task PostNarrow_ValueStillIsBothDictAndFile()
    {
        var data = JsonFile("chain.json");
        await data.Get("database");
        // Headline is the narrowed content type.
        await Assert.That(data.Type!.Name).IsEqualTo("dict");
        // Provenance lives on the VALUE (its Prior chain), not baked onto the type entity: the
        // narrowed value still answers `is` for both its content type and its origin.
        await Assert.That(data.Is("dict")).IsTrue();
        await Assert.That(data.Is("file")).IsTrue();
    }

    [Test]
    public async Task IsDict_ForcesNarrow_Deterministic()
    {
        var data = JsonFile("force.json");
        var op = new global::app.module.condition.Operator("is");
        var right = new Data("", "dict", context: _app.User.Context);
        await Assert.That(await op.Evaluate(data, right)).IsTrue();
        await Assert.That(data.Type!.Name).IsEqualTo("dict");
        // deterministic: asking again answers from the chain, no second parse
        await Assert.That(await op.Evaluate(data, right)).IsTrue();
    }

    [Test]
    public async Task BangFileBangPath_ResolvesOnUnNarrowed_AND_Narrowed_Branches()
    {
        // un-narrowed branch
        var a = JsonFile("brancha.json");
        var facetA = await a.Get("!file");
        await Assert.That(facetA.IsInitialized).IsTrue();
        // narrowed branch — the stashed reference serves the facet
        var b = JsonFile("branchb.json");
        await b.Get("database");
        var facetB = await b.Get("!file");
        await Assert.That(facetB.IsInitialized).IsTrue();
        await Assert.That(facetB.Peek()).IsTypeOf<global::app.type.item.file.@this>();
    }

    [Test]
    public async Task NarrowIsIdempotent_RacingNavigationsConverge_NoCorruption()
    {
        var data = JsonFile("race.json");
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () => await data.Get("port")))
            .ToArray();
        await Task.WhenAll(tasks);
        await Assert.That(data.Type!.Name).IsEqualTo("dict");
        foreach (var t in tasks)
            await Assert.That((await t.Result.Value())?.ToString()).IsEqualTo("8080");
    }

    [Test]
    public async Task DeepClonedReference_NarrowsItsOwnCopy_NoPropagationToOriginal()
    {
        var original = JsonFile("clone.json");
        var copy = original.Clone();
        await copy.Get("database");
        await Assert.That(copy.Type!.Name).IsEqualTo("dict");
        // the original never examined its content — still the reference
        await Assert.That(original.Type!.Name).IsEqualTo("file");
        await Assert.That(original.Peek()).IsTypeOf<global::app.type.item.file.@this>();
    }

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
