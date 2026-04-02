using PLang.Runtime2.Engine.Goals.Goal;

namespace PLang.Tests.Runtime2.Modules.builder;

/// <summary>
/// Tests for GoalFile — the .goal file format parser.
/// GoalFile parses .goal text into Runtime2 Goal and Step objects directly.
/// This is the one place PLang actually parses text (everything else is LLM-mapped).
/// </summary>
public class GoalFileTests
{
    [Test]
    public async Task Parse_SingleGoalWithSteps_ReturnsOneGoal()
    {
        var goals = Goal.Parse("MyGoal\n- step one\n- step two", "/MyGoal.goal");

        await Assert.That(goals.Count).IsEqualTo(1);
        await Assert.That(goals[0].Name).IsEqualTo("MyGoal");
        await Assert.That(goals[0].Steps.Count).IsEqualTo(2);
        await Assert.That(goals[0].Steps[0].Text).IsEqualTo("step one");
        await Assert.That(goals[0].Steps[1].Text).IsEqualTo("step two");
    }

    [Test]
    public async Task Parse_MultipleGoals_FirstPublicRestPrivate()
    {
        var goals = Goal.Parse("First\n- step a\n\nSecond\n- step b\n\nThird\n- step c", "/Multi.goal");

        await Assert.That(goals.Count).IsEqualTo(3);
        await Assert.That(goals[0].Visibility).IsEqualTo(Visibility.Public);
        await Assert.That(goals[1].Visibility).IsEqualTo(Visibility.Private);
        await Assert.That(goals[2].Visibility).IsEqualTo(Visibility.Private);
    }

    [Test]
    public async Task Parse_IndentedSteps_SetsIndentLevel()
    {
        var goals = Goal.Parse("MyGoal\n- top level\n    - indent 1\n        - indent 2", "/Indent.goal");

        await Assert.That(goals[0].Steps[0].Indent).IsEqualTo(0);
        await Assert.That(goals[0].Steps[1].Indent).IsEqualTo(1);
        await Assert.That(goals[0].Steps[2].Indent).IsEqualTo(2);
    }

    [Test]
    public async Task Parse_ContinuationLines_AppendsToStepText()
    {
        var goals = Goal.Parse("MyGoal\n- first line\n  continuation line", "/Cont.goal");

        await Assert.That(goals[0].Steps.Count).IsEqualTo(1);
        await Assert.That(goals[0].Steps[0].Text).IsEqualTo("first line\ncontinuation line");
    }

    [Test]
    public async Task Parse_GoalComments_SetsGoalComment()
    {
        var goals = Goal.Parse("/ This is a comment\nMyGoal\n- step", "/Comment.goal");

        await Assert.That(goals[0].Comment).IsEqualTo("This is a comment");
    }

    [Test]
    public async Task Parse_StepComments_SetsStepComment()
    {
        var goals = Goal.Parse("MyGoal\n- step one\n/ step comment\n- step two", "/StepComment.goal");

        await Assert.That(goals[0].Steps[0].Comment).IsNull();
        await Assert.That(goals[0].Steps[1].Comment).IsEqualTo("step comment");
    }

    [Test]
    public async Task Parse_MultiLineComments_HandledCorrectly()
    {
        var goals = Goal.Parse("/* multi\nline */\nMyGoal\n- step", "/BlockComment.goal");

        await Assert.That(goals[0].Comment).IsEqualTo("multi\nline");
    }

    [Test]
    public async Task Parse_PathComputation_AllGoalsSharePath()
    {
        var goals = Goal.Parse("First\n- step\n\nSecond\n- step", "/folder/MyGoal.goal");

        await Assert.That(goals[0].Path).IsEqualTo("/folder/MyGoal.goal");
        await Assert.That(goals[1].Path).IsEqualTo("/folder/MyGoal.goal");
    }

    [Test]
    public async Task Parse_EmptyFile_ReturnsEmptyList()
    {

        var goals1 = Goal.Parse("", "/Empty.goal");
        await Assert.That(goals1.Count).IsEqualTo(0);

        var goals2 = Goal.Parse("   \n  \n  ", "/Whitespace.goal");
        await Assert.That(goals2.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Parse_TabsConvertedToSpaces()
    {
        // Tab before dash = 4 spaces = indent 1
        var goals = Goal.Parse("MyGoal\n- top\n\t- indented", "/Tabs.goal");

        await Assert.That(goals[0].Steps[1].Indent).IsEqualTo(1);
    }

    [Test]
    public async Task Parse_SubGoalNames_PopulatedOnPublicGoal()
    {
        var goals = Goal.Parse("Public\n- step\n\nPrivateA\n- step\n\nPrivateB\n- step", "/Sub.goal");

        await Assert.That(goals[0].Goals.Count).IsEqualTo(2);
        await Assert.That(goals[0].Goals[0].Name).IsEqualTo("PrivateA");
        await Assert.That(goals[0].Goals[1].Name).IsEqualTo("PrivateB");
    }

    [Test]
    public async Task Parse_BlankLinesBetweenGoals_HandledCorrectly()
    {
        var goals = Goal.Parse("First\n- step a\n\n\n\nSecond\n- step b", "/Blank.goal");

        await Assert.That(goals.Count).IsEqualTo(2);
        await Assert.That(goals[0].Steps.Count).IsEqualTo(1);
        await Assert.That(goals[1].Steps.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Parse_StepLineNumbers_MatchSourceLines()
    {
        // Line 1: MyGoal, Line 2: - step one, Line 3: - step two
        var goals = Goal.Parse("MyGoal\n- step one\n- step two", "/Lines.goal");

        await Assert.That(goals[0].Steps[0].LineNumber).IsEqualTo(2);
        await Assert.That(goals[0].Steps[1].LineNumber).IsEqualTo(3);
    }

    [Test]
    public async Task Parse_PrPath_DerivedFromPath()
    {
        var goals = Goal.Parse("MyGoal\n- step", "/folder/MyGoal.goal");

        // PrPath is derived from Path — /folder/MyGoal.goal → /folder/.build/mygoal.pr
        await Assert.That(goals[0].PrPath).IsEqualTo("/folder/.build/mygoal.pr");
    }

    [Test]
    public async Task Parse_StepBeforeHeader_CreatesImplicitStartGoal()
    {
        // Steps before any goal header → implicit "Start" goal
        var goals = Goal.Parse("- step one\n- step two", "/NoHeader.goal");

        await Assert.That(goals.Count).IsEqualTo(1);
        await Assert.That(goals[0].Name).IsEqualTo("Start");
        await Assert.That(goals[0].Visibility).IsEqualTo(Visibility.Public);
        await Assert.That(goals[0].Steps.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Parse_BareDash_CreatesStepWithEmptyText()
    {
        // A bare "-" with no text creates a step with empty Text
        var goals = Goal.Parse("MyGoal\n-", "/BareDash.goal");

        await Assert.That(goals[0].Steps.Count).IsEqualTo(1);
        await Assert.That(goals[0].Steps[0].Text).IsEqualTo("");
    }

    [Test]
    public async Task Parse_DoubleSlash_IsComment()
    {
        // // is also a comment, not a goal header
        var goals = Goal.Parse("MyGoal\n// this is a comment\n- step", "/DoubleSlash.goal");

        await Assert.That(goals.Count).IsEqualTo(1);
        await Assert.That(goals[0].Steps.Count).IsEqualTo(1);
        await Assert.That(goals[0].Steps[0].Comment).IsEqualTo("/ this is a comment");
    }

    [Test]
    public async Task Parse_BackslashEscape_ContinuesStepText()
    {
        // \ at column 0 continues the previous step's text
        var goals = Goal.Parse("MyGoal\n- write out 'select from list'\n\\Select option", "/Escape.goal");

        await Assert.That(goals.Count).IsEqualTo(1);
        await Assert.That(goals[0].Steps.Count).IsEqualTo(1);
        await Assert.That(goals[0].Steps[0].Text).IsEqualTo("write out 'select from list'\nSelect option");
    }
}
