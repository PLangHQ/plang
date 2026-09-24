using System.Reflection;
using app.module;
using app.module.action.build.code;

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
        _app = TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-stage4hint-" + System.Guid.NewGuid().ToString("N")[..8]));
    }

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private PrAction Make(string module, string action, params (string name, object? value)[] parameters)
        => new PrAction
        {
            Module = _app.Module[module],
            Name = action,
            Property = global::PLang.Tests.Shared.Make.Properties(parameters.Select(p => new Data(p.name, p.value, context: _app.User.Context)).ToList())
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
        public Task<Data> SerializeAsync(System.IO.Stream s, Data d, global::app.View view = global::app.View.Out, System.Threading.CancellationToken ct = default) => Task.FromResult(Data.Ok());
        public Task<Data> DeserializeAsync(System.IO.Stream s, global::app.View view = global::app.View.Out, System.Threading.CancellationToken ct = default) => Task.FromResult(Data.Ok());
        public Task<global::app.data.@this<T>> DeserializeAsync<T>(System.IO.Stream s, global::app.View view = global::app.View.Out, System.Threading.CancellationToken ct = default) where T : global::app.type.item.@this, global::app.type.item.ICreate<T> => Task.FromResult(global::app.data.@this<T>.Ok(default!));
        public global::app.type.item.@this Read(global::app.type.item.source source, global::app.type.reader.ReadContext ctx) => global::app.type.item.@null.@this.Instance;
        public global::app.data.@this<global::app.type.item.text.@this> Serialize(Data d) => global::app.data.@this<global::app.type.item.text.@this>.Ok("");
        public Data Deserialize(string d) => Data.Ok();
        public global::app.data.@this<T> Deserialize<T>(string d) where T : global::app.type.item.@this, global::app.type.item.ICreate<T> => global::app.data.@this<T>.Ok(default!);
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

    // The chain finishes itself (action.list.Build); an empty list means nothing failed.
    private static async Task<List<string>> RunBuildPass(StepActions actions, global::app.@this app)
    {
        var chain = new global::app.goal.step.action.list.@this();
        foreach (var a in actions) chain.Add(a);
        return await chain.Build(app.User.Context) is { } failed ? new() { failed.Message } : new();
    }

    [Test]
    public async Task BuilderValidate_UserHintWinsOverBuildInference()
    {
        // file.read.Build() would infer "csv"; the LLM emitted Type="json" — keep json.
        var setAction = Make("variable", "set",
            ("Name", "x"), ("Value", "%!data%"), ("Type", "json"));
        var actions = ActionsOf(Make("file", "read", ("Path", "foo.csv")), setAction);

        var errors = await RunBuildPass(actions, _app);
        await Assert.That(errors).IsEmpty();

        var typeParam = setAction["Type"];
        await Assert.That(typeParam.Value?.ToString()).IsEqualTo("json");
    }

    [Test]
    public async Task BuilderValidate_BuildInferenceWinsOverDefaultObject()
    {
        // No Type parameter on variable.set → Build()'s "csv" stamps in.
        var setAction = Make("variable", "set", ("Name", "x"), ("Value", "%!data%"));
        var actions = ActionsOf(Make("file", "read", ("Path", "foo.csv")), setAction);

        var errors = await RunBuildPass(actions, _app);
        await Assert.That(errors).IsEmpty();

        var typeParam = setAction["Type"];
        await Assert.That(typeParam).IsNotNull();
        // Stage 3: foo.csv infers the file REFERENCE — {file, csv} — stamped on
        // the terminal variable.set; the content shape appears on narrow.
        await Assert.That(((global::app.type.@this)typeParam!.Value!).Name).IsEqualTo("file");
        await Assert.That(((global::app.type.@this)typeParam!.Value!).Kind?.Name).IsEqualTo("csv");
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

        var typeParam = setAction["Type"];
        await Assert.That(typeParam.Value?.ToString()).IsEqualTo("object");
    }

    [Test]
    public async Task OutputAsk_Build_ReturnsBareOk_DefersToHint()
    {
        var action = Make("output", "ask", ("Question", "?"));
        var (shell, _) = action.Instance(_app.User.Context);
        var (handler, _) = await shell!.Resolve(action, _app.User.Context);
        var result = await ((IClass)handler!).Build();

        await result.IsSuccess();
        await Assert.That(await (await result.Value())!.IsEmpty()).IsTrue()
            .Because("output.ask defers Type to the (type) hint on the write target.");
    }
}
