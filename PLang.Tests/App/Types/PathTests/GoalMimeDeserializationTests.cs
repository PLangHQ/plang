using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.path.file.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Batch 3. <c>.goal</c> MIME behaviour.
///
/// <c>.goal</c> stays <c>text/plain</c> — generic file.read of a .goal returns
/// raw source text (the existing convention; PLang scripts grep through the
/// content). Callers that want a typed Goal call <c>Goal.Parse(text, path)</c>
/// explicitly (discover.cs's auto-flow does exactly this).
///
/// <c>.pr</c> deserializes to <c>Goal</c> via the existing MIME → CLR map.
/// </summary>
public class GoalMimeDeserializationTests
{
    private static (global::app.@this app, string root) MakeApp()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-mime-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        return (new global::app.@this(root), root);
    }

    private const string SimpleGoalText =
        "Start\n" +
        "- write to %x%, 'hello'\n";

    [Test] public async Task ReadText_OnDotGoalFile_ReturnsRawText()
    {
        var (app, root) = MakeApp();
        var abs = System.IO.Path.Combine(root, "Start.goal");
        await System.IO.File.WriteAllTextAsync(abs, SimpleGoalText);
        var p = new FilePath(abs, app.User.Context);
        var read = await p.ReadText();
        await read.IsSuccess();
        await Assert.That((await read.Value()) as string).IsEqualTo(SimpleGoalText);
    }

    [Test] public async Task ReadText_OnDotTestGoalFile_AlsoReturnsRawText()
    {
        var (app, root) = MakeApp();
        var abs = System.IO.Path.Combine(root, "Start.test.goal");
        await System.IO.File.WriteAllTextAsync(abs, SimpleGoalText);
        var p = new FilePath(abs, app.User.Context);
        var read = await p.ReadText();
        await read.IsSuccess();
        await Assert.That((await read.Value()) as string).IsEqualTo(SimpleGoalText);
    }

    [Test] public async Task ReadText_OnDotPrFile_StillReturnsGoal_RegressionGuard()
    {
        var (app, root) = MakeApp();
        var prAbs = System.IO.Path.Combine(root, "Start.pr");
        var json = "{\"path\":\"Start.goal\",\"name\":\"Start\"}";
        await System.IO.File.WriteAllTextAsync(prAbs, json);
        var p = new FilePath(prAbs, app.User.Context);
        var read = await p.ReadText();
        await read.IsSuccess();
        await Assert.That((await read.Value()) is Goal).IsTrue();
    }

    [Test] public async Task GoalParse_ProducesGoalFromText()
    {
        // The typed deserialization lives on Goal.Parse — discover.cs uses it.
        var (app, root) = MakeApp();
        var abs = System.IO.Path.Combine(root, "Start.goal");
        await System.IO.File.WriteAllTextAsync(abs, SimpleGoalText);
        var p = new FilePath(abs, app.User.Context);
        var read = await p.ReadText();
        var text = (await read.Value()) as string ?? "";
        var goal = Goal.Parse(text, p);
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Name).IsEqualTo("Start");
    }

    [Test] public async Task GoalParse_OnEmptyText_ReturnsNull()
    {
        var (app, root) = MakeApp();
        var abs = System.IO.Path.Combine(root, "Bad.goal");
        await System.IO.File.WriteAllTextAsync(abs, "");
        var p = new FilePath(abs, app.User.Context);
        var read = await p.ReadText();
        var text = (await read.Value()) as string ?? "";
        await Assert.That(Goal.Parse(text, p)).IsNull();
    }

    [Test] public async Task GoalMimeRegistration_DotGoalIsTextPlain()
    {
        // .goal MIME is text/plain — confirms the convention reverted in
        // Stage 2's mop-up after PLang scripts that read .goal contents as
        // text needed to keep working.
        var (app, _) = MakeApp();
        var mime = app.Format.Mime(".goal");
        await Assert.That(mime).IsEqualTo("text/plain");
    }
}
