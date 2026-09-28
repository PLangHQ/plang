using System.Reflection;
using app.module;
using app.module.action.build.code;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: user-supplied (type) hints win over Build() inference, and a
// format is found by its extension on the type list.

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
            Module = _app.Module(module),
            Name = action,
            Property = global::PLang.Tests.Shared.Make.Properties(parameters.Select(p => new Data(p.name, p.value, context: _app.actor.list.User.Context)).ToList())
        };

    private static StepActions ActionsOf(params PrAction[] actions)
    {
        var s = new StepActions();
        foreach (var a in actions) s.Add(a);
        return s;
    }

    [Test]
    public async Task FormatByExtension_SingleSegment_Resolves()
    {
        var json = _app.type.list.Extension(".json", _app.actor.list.User.Context).kind;
        await Assert.That(json.Name).IsEqualTo("json");
    }

    // The chain finishes itself (action.list.Build); an empty list means nothing failed.
    private static async Task<List<string>> RunBuildPass(StepActions actions, global::app.@this app)
    {
        var chain = new global::app.goal.step.action.list.@this();
        foreach (var a in actions) chain.Add(a);
        return await chain.Build(app.actor.list.User.Context) is { } failed ? new() { failed.Message } : new();
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
        await Assert.That(((global::app.type.@this)typeParam!.Value!).kind.Name).IsEqualTo("csv");
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
        var (shell, _) = action.Instance(_app.actor.list.User.Context);
        var (handler, _) = await shell!.Resolve(action, _app.actor.list.User.Context);
        var result = await ((IClass)handler!).Build();

        await result.IsSuccess();
        await Assert.That(await (await result.Value())!.IsEmpty()).IsTrue()
            .Because("output.ask defers Type to the (type) hint on the write target.");
    }
}
