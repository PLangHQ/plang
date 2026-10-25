namespace PLang.Tests.App.Modules.ui;

/// <summary>Security probe (bugs 44): what an html page writes is escaped through filters, containers and the value's own
/// members; the places escaping does not reach are pinned so a change shows. Asserts the SAFE behaviour: red is a hole.</summary>
public class FluidHtmlEscapeProbeTests
{
    private const string Evil = "<img src=x onerror=alert(1)>\"'&";

    private static async Task<string> Page(string file, string content, string var = "x", object? value = null)
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-he-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, file), content);
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        await user.Context.Variable.Set(new global::app.data.@this(var, value ?? Evil, context: user.Context));
        var goal = new Goal(app.type.list) { Name = "Host", Path = global::app.type.item.path.@this.Resolve("/host.goal", user.Context) };
        await using var inGoal = user.Context.call.Push(goal, await user.Context.call.Setting());
        var action = new global::app.module.ui.Render(user.Context)
        {
            Template = new global::app.data.@this<global::app.type.item.text.@this>("Template", "/" + file, context: user.Context),
            IsFile = new global::app.data.@this<global::app.type.item.@bool.@this>("IsFile", true, context: user.Context),
        };
        var r = await new global::app.module.ui.code.Fluid().Render(action);
        return r.Success ? (await r.Value())?.ToString() ?? "" : "ERR:" + r.Error?.Key;
    }

    private static void NoActiveMarkup(string o)
    {
        if (o.Contains('<') || o.Contains('>')) throw new System.Exception("raw angle bracket in: " + o);
    }

    [Test]
    [Arguments("{{ x | upcase }}")]
    [Arguments("{{ x | append: 'z' }}")]
    [Arguments("{{ x | prepend: 'z' }}")]
    [Arguments("{{ x | replace: 'a', 'b' }}")]
    [Arguments("{{ x | strip }}")]
    [Arguments("{{ x | truncate: 80 }}")]
    [Arguments("{{ x | default: 'd' }}")]
    [Arguments("{% assign y = x %}{{ y }}")]
    [Arguments("{% capture c %}{{ x }}{% endcapture %}{{ c }}")]
    [Arguments("<a href=\"{{ x }}\">l</a>")]
    [Arguments("<input value='{{ x }}'>")]
    public async Task AnHtmlPage_EscapesTheValue_ThroughEachFilter(string template)
    {
        var o = await Page("p.html", template.Replace("<a href", "<a href").Replace("<input", "<input"));
        // the template's own tags may contain angle brackets: strip the literal wrapper before judging the value
        var value = o.Replace("<a href=\"", "").Replace("\">l</a>", "").Replace("<input value='", "").Replace("'>", "");
        NoActiveMarkup(value);
        await Assert.That(value).DoesNotContain("\"'");
    }

    [Test]
    public async Task AListOfTexts_InAnHtmlPage_IsEscaped()
    {
        var list = new List<string> { Evil };
        var o = await Page("p.html", "{% for i in x %}{{ i }}{% endfor %}{{ x | join: ',' }}", value: list);
        NoActiveMarkup(o);
    }

    [Test]
    public async Task ADictMember_InAnHtmlPage_IsEscaped()
    {
        var o = await Page("p.html", "{{ x.name }}{{ x['name'] }}", value: new Dictionary<string, object?> { ["name"] = Evil });
        NoActiveMarkup(o);
    }

    // pinned, not a hole by the page's own rule: the json filter writes unencoded, so a quote stays a quote — inside an
    // attribute that breaks out. Documented for the page author: json is for a script.
    [Test]
    public async Task TheJsonFilter_InsideAnAttribute_IsNotAttributeSafe()
    {
        // json's own quotes close the attribute (data-x=""), and the value's words are then attributes of the tag
        var o = await Page("p.html", "<div data-x=\"{{ x | json }}\">", value: "x onmouseover=alert(1) ");
        await Assert.That(o).Contains("\"\"x onmouseover=alert(1) \"\"");
    }

    // an svg is a page: a browser runs script in it, so it escapes as html does
    [Test]
    public async Task AnSvgTemplate_EscapesAValue()
    {
        var o = await Page("p.svg", "{{ x }}");
        await Assert.That(o).DoesNotContain("<img src=x");
        await Assert.That(o).Contains("&lt;img");
    }
}
