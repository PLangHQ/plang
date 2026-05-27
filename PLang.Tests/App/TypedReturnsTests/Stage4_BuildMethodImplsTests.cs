using app.modules;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: file.read, llm.query, http.request and http.upload implement
// Build() to surface the inferred PLang type from a literal Path/Url or a
// Schema/Format param. Variable references and unknown extensions yield
// bare Data.Ok() so the runtime materializer can still fill in.

public class Stage4_BuildMethodImplsTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-stage4-" + System.Guid.NewGuid().ToString("N")[..8]));
    }

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private static PrAction Make(string module, string action, params (string name, object? value)[] parameters)
        => new PrAction
        {
            Module = module,
            ActionName = action,
            Parameters = parameters.Select(p => new Data(p.name, p.value)).ToList()
        };

    private async Task<Data> Build(string module, string action, params (string name, object? value)[] parameters)
    {
        var a = Make(module, action, parameters);
        var (handler, err) = _app.Modules.GetCodeGenerated(a);
        await Assert.That(err).IsNull();
        var classified = (IClass)handler!;
        classified.SetAction(a, _app.User.Context);
        return await classified.Build();
    }

    // --- file.read.Build() ---

    [Test]
    public async Task FileRead_Build_LiteralCsvPath_ReturnsOkWithCsv()
    {
        var result = await Build("file", "read", ("Path", "foo.csv"));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("csv");
    }

    [Test]
    public async Task FileRead_Build_LiteralJsonPath_ReturnsOkWithJson()
    {
        var result = await Build("file", "read", ("Path", "data.json"));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("json");
    }

    [Test]
    public async Task FileRead_Build_LiteralUnknownExtension_FallsBackToOk()
    {
        var result = await Build("file", "read", ("Path", "foo.zzz"));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }

    [Test]
    public async Task FileRead_Build_NonLiteralPath_ReturnsBareOk()
    {
        var result = await Build("file", "read", ("Path", "%p%"));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }

    [Test]
    public async Task FileRead_Build_LiteralMissingFile_WritesBuildWarning()
    {
        var channel = (global::app.channels.channel.stream.@this)
            _app.User.Channels.CreateMemoryChannel("builder");

        const string missing = "definitely-missing-stage4.csv";
        var result = await Build("file", "read", ("Path", missing));
        await Assert.That(result.Success).IsTrue();

        channel.Stream.Position = 0;
        var written = await channel.ReadAllTextAsync();
        await Assert.That(written).Contains(missing)
            .Because("Build() must write a missing-file warning whose message names the offending path.");
    }

    [Test]
    public async Task FileRead_Build_LiteralMissingFile_StillReturnsOkWithInferredType()
    {
        var result = await Build("file", "read", ("Path", "definitely-missing-stage4b.csv"));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("csv")
            .Because("Missing file is non-fatal at build time — the inferred type still surfaces.");
    }

    // --- llm.query.Build() ---

    [Test]
    public async Task LlmQuery_Build_WithSchema_ReturnsOkWithJson()
    {
        var result = await Build("llm", "query",
            ("System", "you are a bot"), ("User", "hi"), ("Schema", "{\"type\":\"object\"}"));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("json");
    }

    [Test]
    public async Task LlmQuery_Build_WithFormatNoSchema_ReturnsOkWithFormatValue()
    {
        var result = await Build("llm", "query",
            ("System", "you are a bot"), ("User", "hi"), ("Format", "md"));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("md");
    }

    [Test]
    public async Task LlmQuery_Build_NeitherSchemaNorFormat_ReturnsBareOk()
    {
        var result = await Build("llm", "query",
            ("System", "you are a bot"), ("User", "hi"));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }

    // --- http.request / http.upload .Build() ---

    [Test]
    public async Task HttpRequest_Build_LiteralUrlWithExtension_InfersTypeFromExtension()
    {
        var result = await Build("http", "request", ("Url", "https://api/x.json"));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("json");
    }

    [Test]
    public async Task HttpUpload_Build_NonLiteralUrl_ReturnsBareOk()
    {
        var result = await Build("http", "upload",
            ("Url", "%endpoint%"),
            ("FilePath", "/tmp/dummy.txt"));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }
}
