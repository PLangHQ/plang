using app.goal;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for GoalFile — the .goal file format parser.
/// GoalFile parses .goal text into App Goal and Step objects directly.
/// This is the one place PLang actually parses text (everything else is LLM-mapped).
/// </summary>
public class GoalFileTests
{
    [Test]
    public async Task Parse_SingleGoalWithSteps_ReturnsOneGoal()
    {
        var goal = Goal.Parse("MyGoal\n- step one\n- step two", global::app.type.item.path.@this.Resolve("/MyGoal.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Name).IsEqualTo("MyGoal");
        await Assert.That(goal.Steps.Count).IsEqualTo(2);
        await Assert.That(goal.Steps[0].Text).IsEqualTo("step one");
        await Assert.That(goal.Steps[1].Text).IsEqualTo("step two");
    }

    [Test]
    public async Task Parse_MultipleGoals_FirstPublicRestPrivate()
    {
        var goal = Goal.Parse("First\n- step a\n\nSecond\n- step b\n\nThird\n- step c", global::app.type.item.path.@this.Resolve("/Multi.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Visibility).IsEqualTo(Visibility.Public);
        await Assert.That(goal.Goals[0].Visibility).IsEqualTo(Visibility.Private);
        await Assert.That(goal.Goals[1].Visibility).IsEqualTo(Visibility.Private);
    }

    [Test]
    public async Task Parse_IndentedSteps_SetsIndentLevel()
    {
        var goal = Goal.Parse("MyGoal\n- top level\n    - indent 1\n        - indent 2", global::app.type.item.path.@this.Resolve("/Indent.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Steps[0].Indent).IsEqualTo(0);
        await Assert.That(goal.Steps[1].Indent).IsEqualTo(1);
        await Assert.That(goal.Steps[2].Indent).IsEqualTo(2);
    }

    [Test]
    public async Task Parse_ContinuationLines_AppendsToStepText()
    {
        var goal = Goal.Parse("MyGoal\n- first line\n  continuation line", global::app.type.item.path.@this.Resolve("/Cont.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Steps.Count).IsEqualTo(1);
        await Assert.That(goal.Steps[0].Text).IsEqualTo("first line\ncontinuation line");
    }

    [Test]
    public async Task Parse_GoalComments_SetsGoalComment()
    {
        var goal = Goal.Parse("/ This is a comment\nMyGoal\n- step", global::app.type.item.path.@this.Resolve("/Comment.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Comment).IsEqualTo("This is a comment");
    }

    [Test]
    public async Task Parse_StepComments_SetsStepComment()
    {
        var goal = Goal.Parse("MyGoal\n- step one\n/ step comment\n- step two", global::app.type.item.path.@this.Resolve("/StepComment.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Steps[0].Comment).IsNull();
        await Assert.That(goal.Steps[1].Comment).IsEqualTo("step comment");
    }

    [Test]
    public async Task Parse_MultiLineComments_HandledCorrectly()
    {
        var goal = Goal.Parse("/* multi\nline */\nMyGoal\n- step", global::app.type.item.path.@this.Resolve("/BlockComment.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Comment).IsEqualTo("multi\nline");
    }

    [Test]
    public async Task Parse_PathComputation_AllGoalsSharePath()
    {
        var goal = Goal.Parse("First\n- step\n\nSecond\n- step", global::app.type.item.path.@this.Resolve("/folder/MyGoal.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Path).IsEqualTo("/folder/MyGoal.goal");
        await Assert.That(goal.Goals[0].Path).IsEqualTo("/folder/MyGoal.goal");
    }

    [Test]
    public async Task Parse_EmptyFile_ReturnsNull()
    {
        var goal1 = Goal.Parse("", global::app.type.item.path.@this.Resolve("/Empty.goal", global::PLang.Tests.TestApp.SharedContext));
        await Assert.That(goal1).IsNull();

        var goal2 = Goal.Parse("   \n  \n  ", global::app.type.item.path.@this.Resolve("/Whitespace.goal", global::PLang.Tests.TestApp.SharedContext));
        await Assert.That(goal2).IsNull();
    }

    [Test]
    public async Task Parse_TabsConvertedToSpaces()
    {
        var goal = Goal.Parse("MyGoal\n- top\n\t- indented", global::app.type.item.path.@this.Resolve("/Tabs.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Steps[1].Indent).IsEqualTo(1);
    }

    [Test]
    public async Task Parse_SubGoalNames_PopulatedOnPublicGoal()
    {
        var goal = Goal.Parse("Public\n- step\n\nPrivateA\n- step\n\nPrivateB\n- step", global::app.type.item.path.@this.Resolve("/Sub.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Goals.Count).IsEqualTo(2);
        await Assert.That(goal.Goals[0].Name).IsEqualTo("PrivateA");
        await Assert.That(goal.Goals[1].Name).IsEqualTo("PrivateB");
    }

    [Test]
    public async Task Parse_BlankLinesBetweenGoals_HandledCorrectly()
    {
        var goal = Goal.Parse("First\n- step a\n\n\n\nSecond\n- step b", global::app.type.item.path.@this.Resolve("/Blank.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Steps.Count).IsEqualTo(1);
        await Assert.That(goal.Goals[0].Steps.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Parse_StepLineNumbers_MatchSourceLines()
    {
        var goal = Goal.Parse("MyGoal\n- step one\n- step two", global::app.type.item.path.@this.Resolve("/Lines.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Steps[0].LineNumber).IsEqualTo(2);
        await Assert.That(goal.Steps[1].LineNumber).IsEqualTo(3);
    }

    [Test]
    public async Task Parse_PrPath_DerivedFromPath()
    {
        var goal = Goal.Parse("MyGoal\n- step", global::app.type.item.path.@this.Resolve("/folder/MyGoal.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.PrPath).IsEqualTo("/folder/.build/mygoal.pr");
    }

    [Test]
    public async Task Parse_StepBeforeHeader_CreatesImplicitStartGoal()
    {
        var goal = Goal.Parse("- step one\n- step two", global::app.type.item.path.@this.Resolve("/NoHeader.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Name).IsEqualTo("Start");
        await Assert.That(goal.Visibility).IsEqualTo(Visibility.Public);
        await Assert.That(goal.Steps.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Parse_BareDash_CreatesStepWithEmptyText()
    {
        var goal = Goal.Parse("MyGoal\n-", global::app.type.item.path.@this.Resolve("/BareDash.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Steps.Count).IsEqualTo(1);
        await Assert.That(goal.Steps[0].Text).IsEqualTo("");
    }

    [Test]
    public async Task Parse_DoubleSlash_IsComment()
    {
        var goal = Goal.Parse("MyGoal\n// this is a comment\n- step", global::app.type.item.path.@this.Resolve("/DoubleSlash.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Steps.Count).IsEqualTo(1);
        await Assert.That(goal.Steps[0].Comment).IsEqualTo("/ this is a comment");
    }

    [Test]
    public async Task Parse_BackslashEscape_ContinuesStepText()
    {
        var goal = Goal.Parse("MyGoal\n- write out 'select from list'\n\\Select option", global::app.type.item.path.@this.Resolve("/Escape.goal", global::PLang.Tests.TestApp.SharedContext));

        await Assert.That(goal!.Steps.Count).IsEqualTo(1);
        await Assert.That(goal.Steps[0].Text).IsEqualTo("write out 'select from list'\nSelect option");
    }
}
