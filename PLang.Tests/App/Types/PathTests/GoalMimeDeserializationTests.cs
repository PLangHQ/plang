using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.types.path.file.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 2 — Batch 3. <c>.goal</c> MIME → Goal deserialization (D2).
///
/// Mirrors the existing <c>.pr</c> pattern. <c>FilePath.ReadText</c> converts
/// via the MIME map, then stamps the Goal's Path back-reference. Stage 2 sets
/// <c>Goal.Path</c> to the FilePath's <c>Relative</c> form (string-typed until
/// Stage 3 flips Goal.Path to a Path object).
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

    [Test] public async Task ReadText_OnDotGoalFile_ReturnsParsedGoal()
    {
        var (app, root) = MakeApp();
        var rel = "Start.goal";
        var abs = System.IO.Path.Combine(root, rel);
        await System.IO.File.WriteAllTextAsync(abs, SimpleGoalText);

        var p = new FilePath(abs, app.User.Context);
        var read = await p.ReadText();
        await Assert.That(read.Success).IsTrue();
        await Assert.That(read.Value is Goal).IsTrue();
    }

    [Test] public async Task ReadText_OnDotGoalFile_StampsGoalPathToSelf()
    {
        // Stage 2: Goal.Path holds the Relative string of the FilePath that
        // read it. Stage 3 will flip both Goal.Path and the stamp to Path
        // objects and tighten this to "goal.Path == this filepath".
        var (app, root) = MakeApp();
        var abs = System.IO.Path.Combine(root, "Start.goal");
        await System.IO.File.WriteAllTextAsync(abs, SimpleGoalText);

        var p = new FilePath(abs, app.User.Context);
        var read = await p.ReadText();
        var goal = (Goal)read.Value!;
        await Assert.That(goal.Path).IsEqualTo(p.Relative);
    }

    [Test] public async Task ReadText_OnDotTestGoalFile_FlowsSameWay()
    {
        var (app, root) = MakeApp();
        var abs = System.IO.Path.Combine(root, "Start.test.goal");
        await System.IO.File.WriteAllTextAsync(abs, SimpleGoalText);

        var p = new FilePath(abs, app.User.Context);
        var read = await p.ReadText();
        await Assert.That(read.Success).IsTrue();
        await Assert.That(read.Value is Goal).IsTrue();
    }

    [Test] public async Task ReadText_OnDotPrFile_StillReturnsGoal_RegressionGuard()
    {
        // Build a .pr file by hand-serializing a Goal (the .pr path uses JSON,
        // not Goal.Parse). The regression check is that the existing JSON branch
        // still hits typeof(Goal) — independent of the new source-parse branch.
        var (app, root) = MakeApp();
        var prAbs = System.IO.Path.Combine(root, "Start.pr");
        var json = "{\"path\":\"Start.goal\",\"name\":\"Start\"}";
        await System.IO.File.WriteAllTextAsync(prAbs, json);

        var p = new FilePath(prAbs, app.User.Context);
        var read = await p.ReadText();
        await Assert.That(read.Success).IsTrue();
        await Assert.That(read.Value is Goal).IsTrue();
    }

    [Test] public async Task ReadText_OnMalformedDotGoalFile_ReturnsFailureWithError()
    {
        var (app, root) = MakeApp();
        var abs = System.IO.Path.Combine(root, "Bad.goal");
        // Empty .goal file → Parse returns null → Fail with ParseError.
        await System.IO.File.WriteAllTextAsync(abs, "");

        var p = new FilePath(abs, app.User.Context);
        var read = await p.ReadText();
        await Assert.That(read.Success).IsFalse();
        await Assert.That(read.Error!.Key).IsEqualTo("ParseError");
    }

    [Test] public async Task GoalMimeRegistration_AppearsInTypeMapping()
    {
        // TypeMapping resolves the source-Goal MIME to the Goal CLR type.
        var clr = global::app.types.@this.ClrFromMime("application/plang-goal-source");
        await Assert.That(clr).IsEqualTo(typeof(Goal));
    }
}
