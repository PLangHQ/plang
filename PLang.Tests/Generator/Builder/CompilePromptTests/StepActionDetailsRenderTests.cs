using Fluid;
using PrAction2 = global::app.goal.steps.step.actions.action.@this;

namespace PLang.Tests.Build.CompilePromptTests;

/// <summary>
/// Renderer behavior for the per-action teaching markdown.
/// Template lives at <c>os/system/builder/llm/templates/stepActionDetails.template</c>.
///
/// Architect-spec'd rules (plan.md "Renderer" section):
///   - Per-action block is rendered ONLY for actions in the planner's set
///     (planStep.actions). Today's template already loops over that set;
///     these tests pin Notes/Description/Examples following the same gate.
///   - Each rendered action gains Description / Notes / Examples blocks
///     when the corresponding concatenated text is non-empty. Empty → omit.
///   - Modifier actions (error.handle, cache.wrap, timeout.after) render via
///     the same path — no special-case.
///
/// Architect verification check #1:
///   - System prompt for a `Tests/Simple` step compile drops below 16 KB
///     (was ~20.8 KB). Pinned here as a static-file size check on Compile.llm
///     (the system prompt body); the per-step user message is unaffected.
/// </summary>
public class StepActionDetailsRenderTests
{
    private static readonly string RepoRoot = LocateRepoRoot();
    private static readonly string TemplatePath =
        Path.Combine(RepoRoot, "os", "system", "builder", "llm", "templates", "stepActionDetails.template");
    private static readonly string CompileLlmPath =
        Path.Combine(RepoRoot, "os", "system", "builder", "llm", "Compile.llm");

    private static string LocateRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "os", "system", "builder")))
            dir = Path.GetDirectoryName(dir);
        if (dir == null) throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
        return dir;
    }

    private static async Task<string> RenderAsync(List<PrAction2> actions, List<string> plannerSet)
    {
        var parser = new FluidParser();
        var template = await File.ReadAllTextAsync(TemplatePath);
        if (!parser.TryParse(template, out var fluidTemplate, out var err))
            throw new InvalidOperationException("template parse error: " + err);

        var options = new TemplateOptions { MemberAccessStrategy = new UnsafeMemberAccessStrategy() };
        options.MemberAccessStrategy.IgnoreCasing = true;
        var context = new TemplateContext(options);
        context.SetValue("actions", actions);
        context.SetValue("planStep", new { actions = plannerSet });

        var writer = new StringWriter();
        await fluidTemplate.RenderAsync(writer, Fluid.NullEncoder.Default, context);
        return writer.ToString();
    }

    private static PrAction2 Action(
        string module, string action,
        string? notes = null, string? moduleNotes = null,
        string? description = null, string? moduleDescription = null,
        List<string>? examplesMd = null)
    {
        return new PrAction2
        {
            Module = module,
            ActionName = action,
            Cacheable = true,
            Notes = notes,
            ModuleNotes = moduleNotes,
            Description = description,
            ModuleDescription = moduleDescription,
            ExamplesMd = examplesMd ?? new List<string>(),
        };
    }

    [Test]
    public async Task Render_NotesBlockAppearsForActionInPlannerSet()
    {
        var rendered = await RenderAsync(
            new() { Action("variable", "set", notes: "AsDefault rule text") },
            new() { "variable.set" });

        await Assert.That(rendered).Contains("## variable.set");
        await Assert.That(rendered).Contains("Notes:");
        await Assert.That(rendered).Contains("AsDefault rule text");
    }

    [Test]
    public async Task Render_NotesBlockOmittedWhenTextEmpty()
    {
        var rendered = await RenderAsync(
            new() { Action("variable", "set") },
            new() { "variable.set" });

        await Assert.That(rendered).Contains("## variable.set");
        await Assert.That(rendered).DoesNotContain("Notes:");
    }

    [Test]
    public async Task Render_NotesNotLeakedForActionsOutsidePlannerSet()
    {
        var rendered = await RenderAsync(
            new()
            {
                Action("variable", "set"),
                Action("error", "handle", notes: "huge recovery semantics block"),
            },
            new() { "variable.set" });  // error.handle NOT in planner's set

        await Assert.That(rendered).Contains("## variable.set");
        await Assert.That(rendered).DoesNotContain("error.handle");
        await Assert.That(rendered).DoesNotContain("huge recovery semantics block");
    }

    [Test]
    public async Task Render_BothLayersConcatModuleFirstActionSecond()
    {
        var rendered = await RenderAsync(
            new() { Action("variable", "set", notes: "Action rule.", moduleNotes: "Module rule.") },
            new() { "variable.set" });

        var notesIdx = rendered.IndexOf("Notes:", StringComparison.Ordinal);
        await Assert.That(notesIdx).IsGreaterThan(-1);
        var modIdx = rendered.IndexOf("Module rule.", notesIdx, StringComparison.Ordinal);
        var actIdx = rendered.IndexOf("Action rule.", notesIdx, StringComparison.Ordinal);
        await Assert.That(modIdx).IsGreaterThan(-1);
        await Assert.That(actIdx).IsGreaterThan(modIdx);
    }

    [Test]
    public async Task Render_ModifierActionInPlannerSet_GetsItsNotesRendered()
    {
        var rendered = await RenderAsync(
            new()
            {
                Action("variable", "set"),
                Action("error", "handle", notes: "recovery-semantics text"),
            },
            new() { "variable.set", "error.handle" });

        await Assert.That(rendered).Contains("## error.handle");
        await Assert.That(rendered).Contains("recovery-semantics text");
    }

    [Test]
    public async Task CompileSystemPrompt_TestsSimpleStep_DropsBelow16Kb()
    {
        // Static-file check: the system prompt body is the Compile.llm file
        // (rendered as-is — its size IS the system-prompt size for a step compile
        // since no per-step substitution happens in this file). Per-action
        // teaching now lives in markdown rendered into the user message.
        var size = new FileInfo(CompileLlmPath).Length;
        await Assert.That(size).IsLessThan(16 * 1024);
    }
}
