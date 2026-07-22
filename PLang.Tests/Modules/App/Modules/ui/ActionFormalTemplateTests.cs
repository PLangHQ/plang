using app;
using app.actor.context;
using app.variable;
using app.module.action.ui;
using app.module.action.ui.code;

namespace PLang.Tests.App.Modules.ui;

/// <summary>
/// The actionFormal template renders the catalog's "module.action Name([type] value)" formal
/// syntax from self-describing plang values (a list of action dicts), with each param value
/// rendered by the value's own writer via | formal. Validates the template + binding + filter
/// end-to-end, which the Default.RenderFormal / spec-render migrations drive through app.Run.
/// </summary>
public class ActionFormalTemplateTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;
    private readonly global::app.module.action.ui.code.Fluid _provider;

    public ActionFormalTemplateTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_actionformal_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = global::PLang.Tests.TestApp.Plain(_tempDir);
        _provider = new global::app.module.action.ui.code.Fluid();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true);
    }

    private const string Template =
        "{% for a in actions %}{{ a.Module }}.{{ a.ActionName }}{% if a.Parameter.size > 0 %} {% for p in a.Parameter %}{{ p.Name }}({% if p.Type %}[{{ p.Type }}] {% endif %}{{ p.Value | formal }}){% unless forloop.last %}, {% endunless %}{% endfor %}{% endif %}{% for m in a.Modifier %} | {{ m.Module }}.{{ m.ActionName }}{% if m.Parameter.size > 0 %} {% for p in m.Parameter %}{{ p.Name }}({% if p.Type %}[{{ p.Type }}] {% endif %}{{ p.Value | formal }}){% unless forloop.last %}, {% endunless %}{% endfor %}{% endif %}{% endfor %}{% unless forloop.last %} | {% endunless %}{% endfor %}";

    private global::app.type.item.dict.@this Param(string name, string? type, object? value)
    {
        var d = new global::app.type.item.dict.@this(_app.User.Context);
        d.Set("Name", name);
        if (type != null) d.Set("Type", type);
        d.Set("Value", value);
        return d;
    }

    private global::app.type.item.dict.@this Action(string module, string actionName, params global::app.type.item.dict.@this[] parameters)
    {
        var a = new global::app.type.item.dict.@this(_app.User.Context);
        a.Set("Module", module);
        a.Set("ActionName", actionName);
        var ps = new global::app.type.item.list.@this(_app.User.Context);
        foreach (var p in parameters) ps.Add(p);
        a.Set("Parameter", ps);
        a.Set("Modifier", new global::app.type.item.list.@this(_app.User.Context));
        return a;
    }

    private async Task<string> Render(global::app.type.item.list.@this actions)
    {
        var context = _app.User.Context;
        context.Variable.Set(new Data("actions", actions, context: context));
        var action = new Render(context)
        {
            Template = (global::app.type.item.text.@this)Template,
            IsFile = (global::app.type.item.@bool.@this)false
        };
        var result = await _provider.Render(action);
        await result.IsSuccess();
        return (await result.Value())?.ToString() ?? "";
    }

    [Test]
    public async Task ScalarParam_RendersFormal()
    {
        var actions = new global::app.type.item.list.@this(_app.User.Context);
        actions.Add(Action("output", "write", Param("Data", null, "hello")));
        // Quote rule is gone: a bare value renders bare (hello, not "hello").
        await Assert.That(await Render(actions)).IsEqualTo("output.write Data(hello)");
    }

    [Test]
    public async Task TypedParam_RendersTypeTag()
    {
        var actions = new global::app.type.item.list.@this(_app.User.Context);
        actions.Add(Action("file", "read", Param("Path", "path", "file.txt")));
        await Assert.That(await Render(actions)).IsEqualTo("file.read Path([path] file.txt)");
    }

    [Test]
    public async Task StructuredParam_RendersJsonViaFormal()
    {
        var msg = new global::app.type.item.dict.@this(_app.User.Context);
        msg.Set("role", "user");
        var list = new global::app.type.item.list.@this(_app.User.Context);
        list.Add(msg);

        var actions = new global::app.type.item.list.@this(_app.User.Context);
        actions.Add(Action("llm", "query", Param("Messages", "list", list)));
        await Assert.That(await Render(actions)).IsEqualTo("llm.query Messages([list] [{\"role\":\"user\"}])");
    }
}
