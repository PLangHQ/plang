using app.module.ui;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// A module's learner page (docs/modules/&lt;name&gt;.md) is its catalog rendered through docs/templates/module.template
/// (decision 411). The file module's page is the spec's golden — Documentation/v0.2/module-reference-generation.md,
/// "Golden output", its annotation lines left out: the docs' own check, run without the goal's .pr.
/// </summary>
public class ModulePageTests
{
    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    // The spec's golden page: the markdown block under "Golden output", without its annotation lines.
    private static string[] Golden()
    {
        var spec = System.IO.File.ReadAllLines(System.IO.Path.Combine(RepoRoot(), "Documentation", "v0.2", "module-reference-generation.md"));
        var from = System.Array.FindIndex(spec, line => line.StartsWith("## Golden output"));
        var open = System.Array.FindIndex(spec, from, line => line == "```markdown");
        var close = System.Array.FindIndex(spec, open + 1, line => line == "```");
        return spec[(open + 1)..close].Where(line => !line.StartsWith("<!--")).ToArray();
    }

    // The page's intro is the module's description and its guide (module.guide.md, the learner's prose); the module's
    // notes are the builder's and never on the page.
    [Test]
    public async Task ThePagesIntro_IsTheDescriptionAndTheGuide_NeverTheNotes()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-page-" + System.Guid.NewGuid().ToString("N")[..8]);
        var folder = System.IO.Path.Combine(root, "system", "modules", "file");
        System.IO.Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "module.description.md"), "Files.\n");
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "module.guide.md"), "Paths can be URLs.\n");
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "module.notes.md"), "BUILDER-ONLY\n");
        try
        {
            await using var app = new global::app.@this(root).Testing();
            var context = app.actor.list.User.Context;
            context.Variable.Set(new global::app.data.@this("module", app.Module("file")!, context: context));
            var render = new Render(context)
            {
                Template = (global::app.type.item.text.@this)System.IO.File.ReadAllText(
                    System.IO.Path.Combine(RepoRoot(), "docs", "templates", "module.template")),
                IsFile = (global::app.type.item.@bool.@this)false,
            };

            var result = await new global::app.module.ui.code.Fluid().Render(render);

            await result.IsSuccess();
            var page = (await result.Value())!.ToString()!;
            await Assert.That(page).StartsWith("# File Module\nFiles.\n\nPaths can be URLs.\n\n## ");
            await Assert.That(page).DoesNotContain("BUILDER-ONLY");
        }
        finally
        {
            if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task TheFilePage_IsTheSpecsGolden()
    {
        await using var os = new global::app.@this(System.IO.Path.Combine(RepoRoot(), "os")).Testing();
        var context = os.actor.list.User.Context;
        context.Variable.Set(new global::app.data.@this("module", os.Module("file")!, context: context));
        var render = new Render(context)
        {
            Template = (global::app.type.item.text.@this)System.IO.File.ReadAllText(
                System.IO.Path.Combine(RepoRoot(), "docs", "templates", "module.template")),
            IsFile = (global::app.type.item.@bool.@this)false,
        };

        var result = await new global::app.module.ui.code.Fluid().Render(render);

        await result.IsSuccess();
        var page = (await result.Value())!.ToString()!.TrimEnd('\n').Split('\n');
        var golden = Golden();
        var at = Enumerable.Range(0, System.Math.Max(page.Length, golden.Length))
            .FirstOrDefault(i => i >= page.Length || i >= golden.Length || page[i] != golden[i], -1);
        await Assert.That(at < 0 ? "" : $"line {at + 1}\n  page:   {(at < page.Length ? page[at] : "<end>")}\n  golden: {(at < golden.Length ? golden[at] : "<end>")}")
            .IsEqualTo("");
    }
}
