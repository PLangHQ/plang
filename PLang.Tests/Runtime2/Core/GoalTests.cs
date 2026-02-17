using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Tests.Runtime2.Core;

public class GoalTests
{
    [Test]
    public async Task Properties_CanBeInitialized()
    {
        var goal = new Goal
        {
            Name = "TestGoal",
            Description = "A test goal",
            Comment = "This is a comment",
            Visibility = Visibility.Public,
            Path = "/path/to/goal.goal",
            PrPath = "/path/to/goal.pr.json",
            Hash = "abc123",
            IsSetup = true,
            IsEvent = false,
            InputParameters = new Dictionary<string, string> { { "param1", "string" } },
            SubGoals = new List<string> { "SubGoal1", "SubGoal2" },
            Steps = new GoalSteps
            {
                new Step { Index = 0, Text = "first step" },
                new Step { Index = 1, Text = "second step" }
            }
        };

        await Assert.That(goal.Name).IsEqualTo("TestGoal");
        await Assert.That(goal.Description).IsEqualTo("A test goal");
        await Assert.That(goal.Comment).IsEqualTo("This is a comment");
        await Assert.That(goal.Visibility).IsEqualTo(Visibility.Public);
        await Assert.That(goal.Path).IsEqualTo("/path/to/goal.goal");
        await Assert.That(goal.PrPath).IsEqualTo("/path/to/.build/goal.pr");
        await Assert.That(goal.Hash).IsEqualTo("abc123");
        await Assert.That(goal.IsSetup).IsTrue();
        await Assert.That(goal.IsEvent).IsFalse();
        await Assert.That(goal.InputParameters!.Count).IsEqualTo(1);
        await Assert.That(goal.SubGoals.Count).IsEqualTo(2);
        await Assert.That(goal.Steps.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Name_DefaultsToEmptyString()
    {
        var goal = new Goal();

        await Assert.That(goal.Name).IsEqualTo("");
    }

    [Test]
    public async Task Visibility_DefaultsToPrivate()
    {
        var goal = new Goal();

        await Assert.That(goal.Visibility).IsEqualTo(Visibility.Private);
    }

    [Test]
    public async Task IsSetup_DefaultsToFalse()
    {
        var goal = new Goal();

        await Assert.That(goal.IsSetup).IsFalse();
    }

    [Test]
    public async Task IsEvent_DefaultsToFalse()
    {
        var goal = new Goal();

        await Assert.That(goal.IsEvent).IsFalse();
    }

    [Test]
    public async Task Steps_DefaultsToEmptyList()
    {
        var goal = new Goal();

        await Assert.That(goal.Steps).IsNotNull();
        await Assert.That(goal.Steps.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SubGoals_DefaultsToEmptyList()
    {
        var goal = new Goal();

        await Assert.That(goal.SubGoals).IsNotNull();
        await Assert.That(goal.SubGoals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Parent_CanBeSet()
    {
        var parent = new Goal { Name = "ParentGoal" };
        var child = new Goal { Name = "ChildGoal" };

        child.Parent = parent;

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task FullPath_NoParent_ReturnsName()
    {
        var goal = new Goal { Name = "TestGoal" };

        await Assert.That(goal.FullPath).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task FullPath_WithParent_IncludesParentPath()
    {
        var parent = new Goal { Name = "ParentGoal" };
        var child = new Goal { Name = "ChildGoal", Parent = parent };

        await Assert.That(child.FullPath).IsEqualTo("ParentGoal/ChildGoal");
    }

    [Test]
    public async Task FullPath_MultipleParents_IncludesFullHierarchy()
    {
        var grandparent = new Goal { Name = "Grandparent" };
        var parent = new Goal { Name = "Parent", Parent = grandparent };
        var child = new Goal { Name = "Child", Parent = parent };

        await Assert.That(child.FullPath).IsEqualTo("Grandparent/Parent/Child");
    }

    [Test]
    public async Task ToText_ReturnsFormattedGoal()
    {
        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps
            {
                new Step { Index = 0, Text = "first step" },
                new Step { Index = 1, Text = "second step" }
            }
        };

        var text = goal.ToText();

        await Assert.That(text).Contains("TestGoal");
        await Assert.That(text).Contains("first step");
        await Assert.That(text).Contains("second step");
    }

    [Test]
    public async Task ToText_IncludesComment()
    {
        var goal = new Goal
        {
            Name = "TestGoal",
            Comment = "This is a goal comment"
        };

        var text = goal.ToText();

        await Assert.That(text).Contains("/ This is a goal comment");
    }

    [Test]
    public async Task ToText_IncludesStepComments()
    {
        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps
            {
                new Step { Index = 0, Text = "step", Comment = "step comment" }
            }
        };

        var text = goal.ToText();

        await Assert.That(text).Contains("/ step comment");
    }

    [Test]
    public async Task ToText_RespectsStepIndent()
    {
        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps
            {
                new Step { Index = 0, Text = "no indent", Indent = 0 },
                new Step { Index = 1, Text = "one indent", Indent = 1 },
                new Step { Index = 2, Text = "two indent", Indent = 2 }
            }
        };

        var text = goal.ToText();

        await Assert.That(text).Contains("- no indent");
        await Assert.That(text).Contains(" - one indent");
        await Assert.That(text).Contains("  - two indent");
    }

    [Test]
    public async Task NotFound_CreatesPlaceholderGoal()
    {
        var goal = Goal.NotFound("MissingGoal");

        await Assert.That(goal.Name).IsEqualTo("MissingGoal");
        await Assert.That(goal.Description).IsEqualTo("Goal not found");
    }

    [Test]
    public async Task ToString_ReturnsTypeName()
    {
        var goal = new Goal { Name = "TestGoal" };

        var str = goal.ToString();

        // Goal does not override ToString; returns default type name
        await Assert.That(str).IsEqualTo("PLang.Runtime2.Engine.Goal");
    }

    [Test]
    public async Task FormatForLlm_BasicGoal_ContainsNameAndSteps()
    {
        var goal = new Goal
        {
            Name = "Start",
            Steps = new GoalSteps
            {
                new Step { Index = 0, Text = "write out \"hello\"" },
                new Step { Index = 1, Text = "set %name% = \"world\"" }
            }
        };

        var result = await goal.FormatForLlm();

        await Assert.That(result).Contains("Start");
        await Assert.That(result).Contains("- write out \"hello\"  <= pr: null");
        await Assert.That(result).Contains("- set %name% = \"world\"  <= pr: null");
    }

    [Test]
    public async Task FormatForLlm_WithActions_ShowsActionsJson()
    {
        var goal = new Goal
        {
            Name = "Start",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "write out \"hello\"",
                    Actions = new StepActions(new[]
                    {
                        new PLang.Runtime2.Engine.Action
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new List<Data> { new("content", "hello") }
                        }
                    })
                }
            }
        };

        var result = await goal.FormatForLlm();

        await Assert.That(result).Contains("Start");
        await Assert.That(result).Contains("\"module\":\"output\"");
        await Assert.That(result).Contains("\"action\":\"write\"");
        await Assert.That(result).Contains("\"name\":\"content\"");
    }

    [Test]
    public async Task FormatForLlm_WithComment_IncludesComment()
    {
        var goal = new Goal
        {
            Name = "Start",
            Comment = "This is the main goal",
            Steps = new GoalSteps()
        };

        var result = await goal.FormatForLlm();

        await Assert.That(result).Contains("/ This is the main goal");
        await Assert.That(result).Contains("Start");
    }

    [Test]
    public async Task FormatForLlm_WithErrors_IncludesErrors()
    {
        var goal = new Goal
        {
            Name = "Start",
            Steps = new GoalSteps(),
            Errors = new List<Info>
            {
                new() { Key = "step1", Message = "Invalid JSON response" }
            }
        };

        var result = await goal.FormatForLlm();

        await Assert.That(result).Contains("errors:");
        await Assert.That(result).Contains("step1");
        await Assert.That(result).Contains("Invalid JSON response");
    }

    [Test]
    public async Task FormatForLlm_WithReturn_IncludesReturn()
    {
        var goal = new Goal
        {
            Name = "Start",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "select * from users, write to %users%",
                    Actions = new StepActions(new[]
                    {
                        new PLang.Runtime2.Engine.Action
                        {
                            Module = "db",
                            ActionName = "select",
                            Parameters = new List<Data> { new("sql", "select * from users") },
                            Return = new List<Data> { new("users") }
                        }
                    })
                }
            }
        };

        var result = await goal.FormatForLlm();

        await Assert.That(result).Contains("\"return\":[{\"name\":\"users\"}]");
    }

    [Test]
    public async Task FormatForLlm_MixedSteps_BuiltAndUnbuilt()
    {
        var goal = new Goal
        {
            Name = "Start",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "write out \"hello\"",
                    Actions = new StepActions(new[]
                    {
                        new PLang.Runtime2.Engine.Action
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new List<Data> { new("content", "hello") }
                        }
                    })
                },
                new Step { Index = 1, Text = "set %name% = \"world\"" }
            }
        };

        var result = await goal.FormatForLlm();

        await Assert.That(result).Contains("\"module\":\"output\"");
        await Assert.That(result).Contains("set %name% = \"world\"  <= pr: null");
    }
}

public class VisibilityTests
{
    [Test]
    public async Task Private_HasValueZero()
    {
        await Assert.That((int)Visibility.Private).IsEqualTo(0);
    }

    [Test]
    public async Task Public_HasValueOne()
    {
        await Assert.That((int)Visibility.Public).IsEqualTo(1);
    }
}
