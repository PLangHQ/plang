namespace PLang.Tests.Builder.CompilePromptTests;

/// <summary>
/// Renderer behavior for the per-action teaching markdown.
/// Template lives at <c>os/system/builder/templates/v2/stepActionDetails.template</c>.
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
/// And the architect's verification check #1:
///   - System prompt for a `Tests/Simple` step compile drops below 16 KB
///     (was ~20.8 KB). The drop is structural, not cosmetic.
/// </summary>
public class StepActionDetailsRenderTests
{
    [Test]
    public async Task Render_NotesBlockAppearsForActionInPlannerSet()
    {
        // planStep.actions = ["variable.set"], catalog entry for variable.set has
        // Notes="AsDefault rule text". Rendered template contains a "Notes:" line
        // followed by "AsDefault rule text" inside the variable.set section.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Render_NotesBlockOmittedWhenTextEmpty()
    {
        // planStep.actions = ["variable.set"], catalog Notes/ModuleNotes both
        // null. Rendered template contains the variable.set section but NO
        // "Notes:" header (omit the block, don't render an empty one).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Render_NotesNotLeakedForActionsOutsidePlannerSet()
    {
        // planStep.actions = ["variable.set"]. error.handle has notes in its
        // markdown (large block). Rendered template MUST NOT contain any
        // error.handle section, header, or notes text. This is the core win
        // of the branch — notes only fire when relevant.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Render_BothLayersConcatModuleFirstActionSecond()
    {
        // ModuleNotes="Module rule." + Notes="Action rule." → rendered Notes
        // body shows module text first, blank line, then action text. Same
        // order rule under Description and Examples sections.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Render_ModifierActionInPlannerSet_GetsItsNotesRendered()
    {
        // planStep.actions = ["variable.set", "error.handle"]. error.handle's
        // notes (recovery-semantics text) render under its section. Modifiers
        // use the same code path as peer actions — no special casing.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CompileSystemPrompt_TestsSimpleStep_DropsBelow16Kb()
    {
        // Architect verification check #1. The compile-step system prompt
        // (rendered from os/system/builder/llm/Compile.llm, NOT the per-step
        // user message) for a representative `Tests/Simple` step now sits
        // below 16 KB. Was ~20.8 KB before this branch.
        //
        // Why the bound: ~5 KB of per-action teaching was deleted from the
        // system prompt and moved to per-action markdown rendered in the user
        // message instead. The system prompt should now be ~15 KB; 16 KB is
        // the conservative ceiling that proves the deletion landed.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
