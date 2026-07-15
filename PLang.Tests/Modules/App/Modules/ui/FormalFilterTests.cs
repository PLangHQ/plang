using app;
using app.actor.context;
using app.variable;
using app.module.action.ui;
using app.module.action.ui.code;

namespace PLang.Tests.App.Modules.ui;

/// <summary>
/// The `| formal` filter renders a value the way the action catalog writes it — through the
/// value's OWN text-channel writer: a scalar bare (no quotes, the space/comma quote rule is
/// gone), a dict/list as compact JSON. No C# type-switch, no STJ, no per-type converter.
/// </summary>
public class FormalFilterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;
    private readonly global::app.module.action.ui.code.Fluid _provider;

    public FormalFilterTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_formal_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = global::PLang.Tests.TestApp.Plain(_tempDir);
        _provider = new global::app.module.action.ui.code.Fluid();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private async Task<string> Formal(string varName, object? value)
    {
        var context = _app.User.Context;
        context.Variable.Set(new Data(varName, value, context: context));
        var action = new Render(context)
        {
            Template = (global::app.type.item.text.@this)("{{ " + varName + " | formal }}"),
            IsFile = (global::app.type.item.@bool.@this)false
        };
        var result = await _provider.Render(action);
        await result.IsSuccess();
        return (await result.Value())?.ToString() ?? "";
    }

    [Test]
    public async Task ScalarString_WithSpace_RendersBare_NoQuotes()
        => await Assert.That(await Formal("s", "hello world")).IsEqualTo("hello world");

    [Test]
    public async Task Number_RendersBareLiteral()
        => await Assert.That(await Formal("n", 42)).IsEqualTo("42");

    [Test]
    public async Task Bool_RendersBare()
        => await Assert.That(await Formal("b", true)).IsEqualTo("true");

    [Test]
    public async Task Dict_RendersCompactJson()
    {
        var d = new global::app.type.item.dict.@this(_app.User.Context);
        d.Set("name", "alice");
        d.Set("age", 30);
        await Assert.That(await Formal("d", d)).IsEqualTo("{\"name\":\"alice\",\"age\":30}");
    }

    [Test]
    public async Task List_RendersCompactJsonArray()
    {
        var l = new global::app.type.item.list.@this(_app.User.Context);
        l.Add(new global::app.type.item.text.@this("a"));
        l.Add(new global::app.type.item.text.@this("b"));
        await Assert.That(await Formal("l", l)).IsEqualTo("[\"a\",\"b\"]");
    }
}
