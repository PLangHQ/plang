using app.module.ui;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// A module's learner page (system/modules/&lt;name&gt;/start.md) is its catalog rendered through docs/templates/module.template
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

    private static string SpecPath() =>
        System.IO.Path.Combine(RepoRoot(), "Documentation", "v0.2", "module-reference-generation.md");

    // A module's golden page: the ```markdown block under its "## Golden output — <module> module"
    // heading, without annotation lines.
    private static string[] Golden(string module)
    {
        var spec = System.IO.File.ReadAllLines(SpecPath());
        var from = System.Array.FindIndex(spec, line =>
            line.StartsWith("## Golden output") &&
            line.Contains(module, System.StringComparison.OrdinalIgnoreCase));
        return GoldenFrom(spec, from);
    }

    // The golden ends at the bare fence that closes the outer ```markdown. A fence with an info string
    // (```plang, ```markdown) opens a block one level deeper and a bare ``` closes one level, so a code block
    // inside the golden (a module's ## Examples) does not cut it short.
    private static string[] GoldenFrom(string[] spec, int from)
    {
        var (open, close) = Block(spec, from);
        return spec[(open + 1)..close].Where(line => !line.StartsWith("<!--")).ToArray();
    }

    // The outer ```markdown fence at or after `from`, and the bare fence that closes it.
    private static (int Open, int Close) Block(string[] spec, int from)
    {
        var open = System.Array.FindIndex(spec, from, line => line == "```markdown");
        var depth = 1;
        var close = open + 1;
        for (; close < spec.Length; close++)
        {
            if (spec[close] == "```") { if (--depth == 0) break; }
            else if (spec[close].StartsWith("```")) depth++;
        }
        return (open, close);
    }

    [Test]
    public async Task TheGolden_RunsToTheFenceThatClosesTheOuterMarkdown_PastANestedCodeBlock()
    {
        var spec = new[]
        {
            "## Golden output",
            "```markdown",
            "# File Module",
            "<!-- annotation -->",
            "## Examples",
            "```plang",
            "Start",
            "- read file.txt, write to %text%",
            "```",
            "## After the example",
            "```",
            "outside the golden",
        };

        var golden = GoldenFrom(spec, System.Array.FindIndex(spec, l => l.StartsWith("## Golden output")));

        await Assert.That(golden).IsEquivalentTo(new[]
        {
            "# File Module", "## Examples", "```plang", "Start", "- read file.txt, write to %text%", "```", "## After the example",
        });
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

    // A teaching file that is not there is nil to a template; one that is there and can't be read fails the render with
    // its own error — never read as empty (a page or a decider state pinned under load without its examples)
    [Test]
    public async Task ATeachingFileThatCantBeRead_FailsTheRender_AnAbsentOneIsNil()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-page-" + System.Guid.NewGuid().ToString("N")[..8]);
        var folder = System.IO.Path.Combine(root, "system", "modules", "file");
        System.IO.Directory.CreateDirectory(folder);
        var examples = System.IO.Path.Combine(folder, "read.examples.md");
        System.IO.File.WriteAllText(examples, "Step text: `read x.txt`\n");
        System.IO.File.SetUnixFileMode(examples, System.IO.UnixFileMode.None);
        try
        {
            await using var app = new global::app.@this(root).Testing();
            var context = app.actor.list.User.Context;
            context.Variable.Set(new global::app.data.@this("action", app.Module("file")!["read"]!, context: context));
            async Task<global::app.data.@this> Render(string template) => await new global::app.module.ui.code.Fluid().Render(
                new Render(context) { Template = (global::app.type.item.text.@this)template, IsFile = (global::app.type.item.@bool.@this)false });

            var unreadable = await Render("{% if action.Examples %}examples: {{ action.Examples }}{% endif %}");
            var absent = await Render("{% if action.Guide %}guide{% endif %}none");

            await unreadable.IsFailure();
            await Assert.That(unreadable.Error!.Status.IsNotFound).IsFalse();
            await absent.IsSuccess();
            await Assert.That((await absent.Value())!.ToString()).IsEqualTo("none");
        }
        finally
        {
            System.IO.File.SetUnixFileMode(examples, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite);
            if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
        }
    }

    // An action's guide (<action>.guide.md, the learner's prose) is on its module's page, right after the action's
    // Returns line and before the next action, and shown once.
    [Test]
    public async Task AnActionsGuide_IsOnThePageAfterItsReturns()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-page-" + System.Guid.NewGuid().ToString("N")[..8]);
        var folder = System.IO.Path.Combine(root, "system", "modules", "file");
        System.IO.Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "module.description.md"), "Files.\n");
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "read.description.md"), "Reads a file.\n");
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "read.guide.md"), "A path can be a URL too.\n");
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
            var start = page.IndexOf("\n## read\n", System.StringComparison.Ordinal);
            await Assert.That(start).IsGreaterThan(-1);
            var next = page.IndexOf("\n## ", start + 1, System.StringComparison.Ordinal);
            var section = next < 0 ? page[start..] : page[start..next];
            await Assert.That(System.Text.RegularExpressions.Regex.IsMatch(section, @"\*\*Returns:\*\* [^\n]*\n\nA path can be a URL too\.\n?\z")).IsTrue();
            await Assert.That(System.Text.RegularExpressions.Regex.Matches(page, "A path can be a URL too").Count).IsEqualTo(1);
        }
        finally
        {
            if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
        }
    }

    // A module's learner page rendered through the template, line by line.
    private static async Task<string[]> Page(string module)
    {
        await using var os = new global::app.@this(System.IO.Path.Combine(RepoRoot(), "os")).Testing();
        var context = os.actor.list.User.Context;
        context.Variable.Set(new global::app.data.@this("module", os.Module(module)!, context: context));
        var render = new Render(context)
        {
            Template = (global::app.type.item.text.@this)System.IO.File.ReadAllText(
                System.IO.Path.Combine(RepoRoot(), "docs", "templates", "module.template")),
            IsFile = (global::app.type.item.@bool.@this)false,
        };

        var result = await new global::app.module.ui.code.Fluid().Render(render);

        await result.IsSuccess();
        return (await result.Value())!.ToString()!.TrimEnd('\n').Split('\n');
    }

    // Compare a module's rendered page, line by line, to its golden.
    private static async Task ComparePageToGolden(string module)
    {
        var page = await Page(module);
        var golden = Golden(module);
        var at = Enumerable.Range(0, System.Math.Max(page.Length, golden.Length))
            .FirstOrDefault(i => i >= page.Length || i >= golden.Length || page[i] != golden[i], -1);
        await Assert.That(at < 0 ? "" : $"{module} line {at + 1}\n  page:   {(at < page.Length ? page[at] : "<end>")}\n  golden: {(at < golden.Length ? golden[at] : "<end>")}")
            .IsEqualTo("");
    }

    [Test]
    public Task TheFilePage_IsTheSpecsGolden() => ComparePageToGolden("file");

    [Test]
    public Task TheConditionPage_IsTheSpecsGolden() => ComparePageToGolden("condition");

    [Test]
    public Task TheLoopPage_IsTheSpecsGolden() => ComparePageToGolden("loop");

    [Test]
    public Task TheScreenPage_IsTheSpecsGolden() => ComparePageToGolden("screen");

    // Re-pins a module's golden from its rendered page, when the module's teaching changed on purpose: run it
    // explicitly, then read the spec's diff — only the teaching's lines may move. A golden holding annotation lines is
    // re-pinned by hand, so none is lost.
    [Test, Explicit, NotInParallel("golden")]
    [Arguments("file")]
    [Arguments("condition")]
    [Arguments("loop")]
    public async Task AcceptTheGolden(string module)
    {
        var spec = System.IO.File.ReadAllLines(SpecPath());
        var from = System.Array.FindIndex(spec, line =>
            line.StartsWith("## Golden output") && line.Contains(module, System.StringComparison.OrdinalIgnoreCase));
        await Assert.That(from).IsGreaterThan(-1);
        var (open, close) = Block(spec, from);
        await Assert.That(spec[(open + 1)..close].Any(line => line.StartsWith("<!--"))).IsFalse();

        var page = await Page(module);
        System.IO.File.WriteAllText(SpecPath(), string.Join('\n', [.. spec[..(open + 1)], .. page, .. spec[close..]]) + "\n");
    }
}
