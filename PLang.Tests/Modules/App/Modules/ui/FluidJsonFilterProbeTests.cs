namespace PLang.Tests.App.Modules.ui;

/// <summary>Security probe (bugs full pass): the <c>| json</c> filter's output stands inside a page's script. A value
/// cannot close the script or open a tag, and a text still reads back as it was.</summary>
public class FluidJsonFilterProbeTests
{
    private static async Task<string> Render(string template)
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-jf-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        var goal = new Goal(app.type.list) { Name = "Host", Path = global::app.type.item.path.@this.Resolve("/host.goal", user.Context) };
        await using var inGoal = user.Context.call.Push(goal, await user.Context.call.Setting());
        var action = new global::app.module.ui.Render(user.Context)
        {
            Template = new global::app.data.@this<global::app.type.item.text.@this>("Template", template, context: user.Context),
            IsFile = new global::app.data.@this<global::app.type.item.@bool.@this>("IsFile", false, context: user.Context),
        };
        var r = await new global::app.module.ui.code.Fluid().Render(action);
        return r.Success ? (await r.Value())?.ToString() ?? "" : "ERR:" + r.Error?.Key;
    }

    [Test]
    public async Task AScriptCloser_InAValue_NeverReachesThePageRaw()
    {
        var o = await Render("{% assign x = '</script><img src=x onerror=alert(1)> & <!--' %}{{ x | json }}");
        await Assert.That(o).DoesNotContain("<");
        await Assert.That(o).DoesNotContain(">");
        await Assert.That(o).DoesNotContain("&");
    }

    [Test]
    public async Task AListOfTexts_KeepsItsShape_AndNoAngleBracket()
    {
        var o = await Render("{% assign x = '<b>' %}{{ x | json }}");
        await Assert.That(o).IsEqualTo("\"\\u003Cb\\u003E\"");
    }
}
