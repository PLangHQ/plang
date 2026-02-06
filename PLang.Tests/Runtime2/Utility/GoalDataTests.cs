using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Utility;

namespace PLang.Tests.Runtime2.Utility;

public class GoalDataTests
{
    [Test]
    public async Task Properties_CanBeSet()
    {
        var data = new GoalData
        {
            Name = "TestGoal",
            Description = "A test goal",
            Comment = "This is a comment",
            Visibility = "public",
            IsSetup = true,
            IsEvent = false,
            Hash = "abc123",
            InputParameters = new Dictionary<string, string> { { "param1", "string" } },
            SubGoals = new List<string> { "SubGoal1" },
            Steps = new List<StepDataDto>
            {
                new StepDataDto { Index = 0, Text = "step 1" }
            }
        };

        await Assert.That(data.Name).IsEqualTo("TestGoal");
        await Assert.That(data.Description).IsEqualTo("A test goal");
        await Assert.That(data.Comment).IsEqualTo("This is a comment");
        await Assert.That(data.Visibility).IsEqualTo("public");
        await Assert.That(data.IsSetup).IsTrue();
        await Assert.That(data.IsEvent).IsFalse();
        await Assert.That(data.Hash).IsEqualTo("abc123");
        await Assert.That(data.InputParameters!.Count).IsEqualTo(1);
        await Assert.That(data.SubGoals!.Count).IsEqualTo(1);
        await Assert.That(data.Steps.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Name_DefaultsToEmptyString()
    {
        var data = new GoalData();

        await Assert.That(data.Name).IsEqualTo("");
    }

    [Test]
    public async Task Steps_DefaultsToEmptyList()
    {
        var data = new GoalData();

        await Assert.That(data.Steps).IsNotNull();
        await Assert.That(data.Steps.Count).IsEqualTo(0);
    }
}

public class StepDataDtoTests
{
    [Test]
    public async Task Properties_CanBeSet()
    {
        var data = new StepDataDto
        {
            Index = 5,
            Text = "test step",
            LineNumber = 10,
            Indent = 2,
            Comment = "step comment",
            Actions = new List<PLang.Runtime2.Core.Action>
            {
                new PLang.Runtime2.Core.Action
                {
                    Class = "http",
                    Method = "get",
                    Parameters = new List<Data> { new Data("url", "https://api.example.com") },
                    Return = new Return { Variables = new List<Data> { new Data("response") } }
                }
            },
            OnErrorGoal = "HandleError",
            WaitForExecution = false,
            Timeout = 30
        };

        await Assert.That(data.Index).IsEqualTo(5);
        await Assert.That(data.Text).IsEqualTo("test step");
        await Assert.That(data.LineNumber).IsEqualTo(10);
        await Assert.That(data.Indent).IsEqualTo(2);
        await Assert.That(data.Comment).IsEqualTo("step comment");
        await Assert.That(data.Actions.Count).IsEqualTo(1);
        await Assert.That(data.Actions[0].Class).IsEqualTo("http");
        await Assert.That(data.Actions[0].Method).IsEqualTo("get");
        await Assert.That(data.OnErrorGoal).IsEqualTo("HandleError");
        await Assert.That(data.WaitForExecution).IsFalse();
        await Assert.That(data.Timeout).IsEqualTo(30);
    }

    [Test]
    public async Task Text_DefaultsToEmptyString()
    {
        var data = new StepDataDto();

        await Assert.That(data.Text).IsEqualTo("");
    }

    [Test]
    public async Task Actions_DefaultsToEmptyList()
    {
        var data = new StepDataDto();

        await Assert.That(data.Actions).IsNotNull();
        await Assert.That(data.Actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task WaitForExecution_DefaultsToTrue()
    {
        var data = new StepDataDto();

        await Assert.That(data.WaitForExecution).IsTrue();
    }
}

public class GoalDataConverterTests
{
    [Test]
    public async Task ToGoal_ConvertsBasicProperties()
    {
        var data = new GoalData
        {
            Name = "TestGoal",
            Description = "A test goal",
            Comment = "This is a comment",
            Hash = "abc123",
            IsSetup = true,
            IsEvent = false
        };

        var goal = GoalDataConverter.ToGoal(data);

        await Assert.That(goal.Name).IsEqualTo("TestGoal");
        await Assert.That(goal.Description).IsEqualTo("A test goal");
        await Assert.That(goal.Comment).IsEqualTo("This is a comment");
        await Assert.That(goal.Hash).IsEqualTo("abc123");
        await Assert.That(goal.IsSetup).IsTrue();
        await Assert.That(goal.IsEvent).IsFalse();
    }

    [Test]
    public async Task ToGoal_ConvertsVisibility_Public()
    {
        var data = new GoalData { Name = "Test", Visibility = "public" };

        var goal = GoalDataConverter.ToGoal(data);

        await Assert.That(goal.Visibility).IsEqualTo(Visibility.Public);
    }

    [Test]
    public async Task ToGoal_ConvertsVisibility_Private()
    {
        var data = new GoalData { Name = "Test", Visibility = "private" };

        var goal = GoalDataConverter.ToGoal(data);

        await Assert.That(goal.Visibility).IsEqualTo(Visibility.Private);
    }

    [Test]
    public async Task ToGoal_ConvertsVisibility_NullDefaultsToPrivate()
    {
        var data = new GoalData { Name = "Test", Visibility = null };

        var goal = GoalDataConverter.ToGoal(data);

        await Assert.That(goal.Visibility).IsEqualTo(Visibility.Private);
    }

    [Test]
    public async Task ToGoal_ConvertsVisibility_CaseInsensitive()
    {
        var data = new GoalData { Name = "Test", Visibility = "PUBLIC" };

        var goal = GoalDataConverter.ToGoal(data);

        await Assert.That(goal.Visibility).IsEqualTo(Visibility.Public);
    }

    [Test]
    public async Task ToGoal_ConvertsSteps()
    {
        var data = new GoalData
        {
            Name = "TestGoal",
            Steps = new List<StepDataDto>
            {
                new StepDataDto
                {
                    Index = 0, Text = "step 1",
                    Actions = new List<PLang.Runtime2.Core.Action>
                    {
                        new PLang.Runtime2.Core.Action { Class = "var", Method = "set" }
                    }
                },
                new StepDataDto
                {
                    Index = 1, Text = "step 2",
                    Actions = new List<PLang.Runtime2.Core.Action>
                    {
                        new PLang.Runtime2.Core.Action { Class = "http", Method = "get" }
                    }
                }
            }
        };

        var goal = GoalDataConverter.ToGoal(data);

        await Assert.That(goal.Steps.Count).IsEqualTo(2);
        await Assert.That(goal.Steps[0].Text).IsEqualTo("step 1");
        await Assert.That(goal.Steps[1].Text).IsEqualTo("step 2");
    }

    [Test]
    public async Task ToGoal_SetsStepGoalReference()
    {
        var data = new GoalData
        {
            Name = "TestGoal",
            Steps = new List<StepDataDto> { new StepDataDto { Index = 0, Text = "step" } }
        };

        var goal = GoalDataConverter.ToGoal(data);

        await Assert.That(goal.Steps[0].Goal).IsEqualTo(goal);
    }

    [Test]
    public async Task ToGoal_SetsFilePaths()
    {
        var data = new GoalData { Name = "TestGoal" };

        var goal = GoalDataConverter.ToGoal(data, "/path/goal.goal", "/path/goal.pr.json");

        await Assert.That(goal.Path).IsEqualTo("/path/goal.goal");
        await Assert.That(goal.PrPath).IsEqualTo("/path/goal.pr.json");
    }

    [Test]
    public async Task ToGoal_ConvertsInputParameters()
    {
        var data = new GoalData
        {
            Name = "TestGoal",
            InputParameters = new Dictionary<string, string> { { "name", "string" }, { "age", "int" } }
        };

        var goal = GoalDataConverter.ToGoal(data);

        await Assert.That(goal.InputParameters!.Count).IsEqualTo(2);
        await Assert.That(goal.InputParameters["name"]).IsEqualTo("string");
    }

    [Test]
    public async Task ToGoal_ConvertsSubGoals()
    {
        var data = new GoalData
        {
            Name = "TestGoal",
            SubGoals = new List<string> { "SubGoal1", "SubGoal2" }
        };

        var goal = GoalDataConverter.ToGoal(data);

        await Assert.That(goal.SubGoals.Count).IsEqualTo(2);
        await Assert.That(goal.SubGoals[0]).IsEqualTo("SubGoal1");
    }

    [Test]
    public async Task ToStep_ConvertsAllProperties()
    {
        var data = new StepDataDto
        {
            Index = 5,
            Text = "test step",
            LineNumber = 10,
            Indent = 2,
            Comment = "comment",
            Actions = new List<PLang.Runtime2.Core.Action>
            {
                new PLang.Runtime2.Core.Action { Class = "http", Method = "get" }
            },
            OnErrorGoal = "ErrorHandler",
            WaitForExecution = false,
            Timeout = 30
        };

        var step = GoalDataConverter.ToStep(data);

        await Assert.That(step.Index).IsEqualTo(5);
        await Assert.That(step.Text).IsEqualTo("test step");
        await Assert.That(step.LineNumber).IsEqualTo(10);
        await Assert.That(step.Indent).IsEqualTo(2);
        await Assert.That(step.Comment).IsEqualTo("comment");
        await Assert.That(step.Actions.Count).IsEqualTo(1);
        await Assert.That(step.Actions[0].Class).IsEqualTo("http");
        await Assert.That(step.Actions[0].Method).IsEqualTo("get");
        await Assert.That(step.OnErrorGoal).IsEqualTo("ErrorHandler");
        await Assert.That(step.WaitForExecution).IsFalse();
        await Assert.That(step.Timeout).IsEqualTo(30);
    }

    [Test]
    public async Task ToData_Goal_ConvertsAllProperties()
    {
        var goal = new Goal
        {
            Name = "TestGoal",
            Description = "A test goal",
            Comment = "comment",
            Visibility = Visibility.Public,
            IsSetup = true,
            IsEvent = false,
            Hash = "abc123",
            InputParameters = new Dictionary<string, string> { { "param", "string" } },
            SubGoals = new List<string> { "SubGoal" },
            Steps = new List<Step> { new Step { Index = 0, Text = "step" } }
        };

        var data = GoalDataConverter.ToData(goal);

        await Assert.That(data.Name).IsEqualTo("TestGoal");
        await Assert.That(data.Description).IsEqualTo("A test goal");
        await Assert.That(data.Comment).IsEqualTo("comment");
        await Assert.That(data.Visibility).IsEqualTo("public");
        await Assert.That(data.IsSetup).IsTrue();
        await Assert.That(data.IsEvent).IsFalse();
        await Assert.That(data.Hash).IsEqualTo("abc123");
        await Assert.That(data.InputParameters!["param"]).IsEqualTo("string");
        await Assert.That(data.SubGoals![0]).IsEqualTo("SubGoal");
        await Assert.That(data.Steps.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ToData_Step_ConvertsAllProperties()
    {
        var step = new Step
        {
            Index = 5,
            Text = "test step",
            LineNumber = 10,
            Indent = 2,
            Comment = "comment",
            Actions = new List<IAction>
            {
                new PLang.Runtime2.Core.Action { Class = "http", Method = "get" }
            },
            OnErrorGoal = "ErrorHandler",
            WaitForExecution = false,
            Timeout = 30
        };

        var data = GoalDataConverter.ToData(step);

        await Assert.That(data.Index).IsEqualTo(5);
        await Assert.That(data.Text).IsEqualTo("test step");
        await Assert.That(data.LineNumber).IsEqualTo(10);
        await Assert.That(data.Indent).IsEqualTo(2);
        await Assert.That(data.Comment).IsEqualTo("comment");
        await Assert.That(data.Actions.Count).IsEqualTo(1);
        await Assert.That(data.Actions[0].Class).IsEqualTo("http");
        await Assert.That(data.Actions[0].Method).IsEqualTo("get");
        await Assert.That(data.OnErrorGoal).IsEqualTo("ErrorHandler");
        await Assert.That(data.WaitForExecution).IsFalse();
        await Assert.That(data.Timeout).IsEqualTo(30);
    }

    [Test]
    public async Task Roundtrip_GoalData_PreservesData()
    {
        var original = new GoalData
        {
            Name = "TestGoal",
            Description = "A test",
            Visibility = "public",
            Steps = new List<StepDataDto>
            {
                new StepDataDto
                {
                    Index = 0, Text = "step 1",
                    Actions = new List<PLang.Runtime2.Core.Action>
                    {
                        new PLang.Runtime2.Core.Action { Class = "var", Method = "set" }
                    }
                }
            }
        };

        var goal = GoalDataConverter.ToGoal(original);
        var roundtrip = GoalDataConverter.ToData(goal);

        await Assert.That(roundtrip.Name).IsEqualTo(original.Name);
        await Assert.That(roundtrip.Description).IsEqualTo(original.Description);
        await Assert.That(roundtrip.Visibility).IsEqualTo(original.Visibility);
        await Assert.That(roundtrip.Steps.Count).IsEqualTo(original.Steps.Count);
    }
}
