using app.module.ui;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// A type's learner page (os/system/type/&lt;name&gt;/start.md) is its catalog rendered through docs/templates/type.template.
/// The text type's page is the spec's golden — Documentation/v0.2/type-reference-generation.md, "Golden output", its
/// annotation lines left out: the docs' own check, run without the goal's .pr.
/// </summary>
public class TypePageTests
{
    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    private static string SpecPath() =>
        System.IO.Path.Combine(RepoRoot(), "Documentation", "v0.2", "type-reference-generation.md");

    // A type's golden page: the ```markdown block under its "## Golden output — <type>" heading, without annotation lines.
    private static string[] Golden(string type)
    {
        var spec = System.IO.File.ReadAllLines(SpecPath());
        var from = System.Array.FindIndex(spec, line =>
            line.StartsWith("## Golden output") &&
            line.Contains(type, System.StringComparison.OrdinalIgnoreCase));
        return GoldenFrom(spec, from);
    }

    // The golden ends at the bare fence that closes the outer ```markdown; a fenced code block inside (a ```plang
    // example in the guide) is one level deeper and does not cut it short.
    private static string[] GoldenFrom(string[] spec, int from)
    {
        var open = System.Array.FindIndex(spec, from, line => line == "```markdown");
        var depth = 1;
        var close = open + 1;
        for (; close < spec.Length; close++)
        {
            if (spec[close] == "```") { if (--depth == 0) break; }
            else if (spec[close].StartsWith("```")) depth++;
        }
        return spec[(open + 1)..close].Where(line => !line.StartsWith("<!--")).ToArray();
    }

    // Render a type's learner page through the template over the os catalog.
    private static async Task<string> RenderType(string type)
    {
        await using var os = new global::app.@this(System.IO.Path.Combine(RepoRoot(), "os")).Testing();
        var context = os.actor.list.User.Context;
        context.Variable.Set(new global::app.data.@this("type", os.type.list[type], context: context));
        var render = new Render(context)
        {
            Template = (global::app.type.item.text.@this)System.IO.File.ReadAllText(
                System.IO.Path.Combine(RepoRoot(), "docs", "templates", "type.template")),
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.ui.code.Fluid().Render(render);
        await result.IsSuccess();
        return (await result.Value())!.ToString()!;
    }

    // Render a type's page and compare it, line by line, to its golden.
    private static async Task ComparePageToGolden(string type)
    {
        var page = (await RenderType(type)).TrimEnd('\n').Split('\n');
        var golden = Golden(type);
        var at = Enumerable.Range(0, System.Math.Max(page.Length, golden.Length))
            .FirstOrDefault(i => i >= page.Length || i >= golden.Length || page[i] != golden[i], -1);
        await Assert.That(at < 0 ? "" : $"{type} line {at + 1}\n  page:   {(at < page.Length ? page[at] : "<end>")}\n  golden: {(at < golden.Length ? golden[at] : "<end>")}")
            .IsEqualTo("");
    }

    [Test]
    public Task TheTextPage_IsTheSpecsGolden() => ComparePageToGolden("text");

    [Test]
    public Task TheSizePage_IsTheSpecsGolden() => ComparePageToGolden("size");

    [Test]
    public Task TheDictPage_IsTheSpecsGolden() => ComparePageToGolden("dict");
}
