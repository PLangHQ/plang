using System.Reflection;
using app.module;
using app.module.builder.code;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: user-supplied (type) hints win over Build() inference, and the
// serializer registry walks multi-segment extensions (.junit.xml) before
// falling back to single-segment.

public class Stage4_TypeHintPrecedenceTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-stage4hint-" + System.Guid.NewGuid().ToString("N")[..8]));
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

    private static StepActions ActionsOf(params PrAction[] actions)
    {
        var s = new StepActions();
        foreach (var a in actions) s.Add(a);
        return s;
    }

    [Test]
    public async Task SerializersGetByExtension_SingleSegment_Resolves()
    {
        var json = _app.User.Channel.Serializers.GetByExtension(".json");
        await Assert.That(json).IsNotNull();
    }

    private sealed class StubSerializer : global::app.channel.serializer.ISerializer
    {
        public string Type { get; init; } = "";
        public string Extension { get; init; } = "";
        public Task<Data> SerializeAsync(System.IO.Stream s, Data d, System.Threading.CancellationToken ct = default) => Task.FromResult(Data.Ok());
        public Task<Data> DeserializeAsync(System.IO.Stream s, System.Threading.CancellationToken ct = default) => Task.FromResult(Data.Ok());
        public Task<global::app.data.@this<T>> DeserializeAsync<T>(System.IO.Stream s, System.Threading.CancellationToken ct = default) => Task.FromResult(global::app.data.@this<T>.Ok(default!));
        public global::app.data.@this<string> Serialize(Data d) => global::app.data.@this<string>.Ok("");
        public Data Deserialize(string d) => Data.Ok();
        public global::app.data.@this<T> Deserialize<T>(string d) => global::app.data.@this<T>.Ok(default!);
    }

    [Test]
    public async Task SerializersGetByExtension_MultiSegment_Resolves()
    {
        var stub = new StubSerializer { Extension = ".junit.xml", Type = "application/junit+xml" };
        _app.User.Channel.Serializers.Register(stub);

        var resolved = _app.User.Channel.Serializers.GetByExtension(".junit.xml");
        await Assert.That(resolved).IsEqualTo((global::app.channel.serializer.ISerializer)stub);
    }

    [Test]
    public async Task SerializersGetByExtension_MultiSegment_FallsBackToSingleSegment()
    {
        // Register only the single-segment ".xml" — a multi-segment lookup that
        // doesn't have its own registration must walk down to the trailing
        // segment and resolve there.
        var xml = new StubSerializer { Extension = ".xml", Type = "application/xml" };
        _app.User.Channel.Serializers.Register(xml);

        var resolved = _app.User.Channel.Serializers.GetByExtension(".unknown.xml");
        await Assert.That(resolved).IsEqualTo((global::app.channel.serializer.ISerializer)xml);
    }

    [Test]
    public async Task CompileLlm_Kernel_ContainsTypeHintRule()
    {
        var path = System.IO.Path.Combine(_app.AbsolutePath, "..", "..", "..", "..", "..", "os", "system", "builder", "llm", "Compile.llm");
        // Walk up from the test temp dir to the repo root.
        var asmDir = System.IO.Path.GetDirectoryName(typeof(global::app.@this).Assembly.Location)!;
        var repo = asmDir;
        while (repo != null && !System.IO.Directory.Exists(System.IO.Path.Combine(repo, "os")))
            repo = System.IO.Directory.GetParent(repo)?.FullName;
        await Assert.That(repo).IsNotNull();
        var compileLlm = System.IO.File.ReadAllText(System.IO.Path.Combine(repo!, "os", "system", "builder", "llm", "Compile.llm"));
        await Assert.That(compileLlm).Contains("(type)")
            .Because("Cross-cutting kernel must teach the (type) hint rule.");
        await Assert.That(compileLlm).Contains("write to %answer%(json)")
            .Because("Worked example anchors the rule.");
    }

    private static async Task<List<string>> RunBuildPass(StepActions actions, global::app.@this app)
        => await Default.RunBuildPass(actions, app.Module, app.User.Context);

    [Test]
    public async Task BuilderValidate_UserHintWinsOverBuildInference()
    {
        // file.read.Build() would infer "csv"; the LLM emitted Type="json" — keep json.
        var setAction = Make("variable", "set",
            ("Name", "x"), ("Value", "%!data%"), ("Type", "json"));
        var actions = ActionsOf(Make("file", "read", ("Path", "foo.csv")), setAction);

        var errors = await RunBuildPass(actions, _app);
        await Assert.That(errors).IsEmpty();

        var typeParam = setAction.Parameters.First(p =>
            string.Equals(p.Name, "Type", System.StringComparison.OrdinalIgnoreCase));
        await Assert.That(typeParam.Value).IsEqualTo("json");
    }

    [Test]
    public async Task BuilderValidate_BuildInferenceWinsOverDefaultObject()
    {
        // No Type parameter on variable.set → Build()'s "csv" stamps in.
        var setAction = Make("variable", "set", ("Name", "x"), ("Value", "%!data%"));
        var actions = ActionsOf(Make("file", "read", ("Path", "foo.csv")), setAction);

        var errors = await RunBuildPass(actions, _app);
        await Assert.That(errors).IsEmpty();

        var typeParam = setAction.Parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Type", System.StringComparison.OrdinalIgnoreCase));
        await Assert.That(typeParam).IsNotNull();
        // foo.csv infers the structured {text, csv} entity (name = materialized
        // family, kind = extension), stamped on the terminal variable.set.
        await Assert.That(((global::app.type.@this)typeParam!.Value!).Name).IsEqualTo("text");
        await Assert.That(((global::app.type.@this)typeParam!.Value!).Kind).IsEqualTo("csv");
    }

    [Test]
    public async Task BuilderValidate_DistinguishesExplicitObject_FromDefaultObject()
    {
        // Developer explicitly hinted (object) → variable.set has Type="object"
        // already. Build()'s "csv" must NOT overwrite — the explicit hint wins.
        var setAction = Make("variable", "set",
            ("Name", "x"), ("Value", "%!data%"), ("Type", "object"));
        var actions = ActionsOf(Make("file", "read", ("Path", "foo.csv")), setAction);

        var errors = await RunBuildPass(actions, _app);
        await Assert.That(errors).IsEmpty();

        var typeParam = setAction.Parameters.First(p =>
            string.Equals(p.Name, "Type", System.StringComparison.OrdinalIgnoreCase));
        await Assert.That(typeParam.Value).IsEqualTo("object");
    }

    [Test]
    public async Task OutputAsk_Build_ReturnsBareOk_DefersToHint()
    {
        var action = Make("output", "ask", ("Question", "?"));
        var (handler, _) = _app.Module.GetCodeGenerated(action);
        var classified = (IClass)handler!;
        classified.SetAction(action, _app.User.Context);
        var result = await classified.Build();

        await result.IsSuccess();
        await Assert.That(result.Value).IsNull()
            .Because("output.ask defers Type to the (type) hint on the write target.");
    }
}
