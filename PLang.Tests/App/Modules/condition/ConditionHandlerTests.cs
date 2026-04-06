using App.Context;
using App;
using App.Variables;
using App.modules.condition;
using PLang.SafeFileSystem;
namespace PLang.Tests.App.actions.condition;

public class ConditionHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly App.@this _engine;

    public ConditionHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        _engine = new App.@this(_tempDir, fileSystem: _fs);
    }

    public void Dispose()
    {
        _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private PLangContext CreateContext()
    {
        return _engine.CreateContext();
    }

    // --- Unit tests: no goals ---

    [Test]
    public async Task IfTrue_NoGoals_ReturnsSuccessWithTrue()
    {
        var action = new If { Context = CreateContext(), Left = Data.Ok(true), Operator = "==", Right = Data.Ok(true) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task IfFalse_NoGoals_ReturnsSuccessWithFalse()
    {
        var action = new If { Context = CreateContext(), Left = Data.Ok(false), Operator = "==", Right = Data.Ok(true) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    // --- Unit tests: with goals ---

    [Test]
    public async Task IfTrue_CallsGoalIfTrue()
    {

        var captureStream = new System.IO.MemoryStream();
        _engine.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        // Register a goal that writes "true-branch"
        var trueGoal = new Goal
        {
            Name = "TrueBranch",
            Path = "/TrueBranch.goal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "write true branch",
                    Actions = new StepActions
                    {
                        new App.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new System.Collections.Generic.List<Data> { new Data("Data", "true-branch") }
                        }
                    }
                }
            }
        };
        _engine.Goals.Add(trueGoal);

        var action = new If
        {
            Context = CreateContext(),
            Left = Data.Ok(true),
            Operator = "==",
            Right = Data.Ok(true),
            GoalIfTrue = new GoalCall { Name = "TrueBranch" },
            GoalIfFalse = new GoalCall { Name = "FalseBranch" }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("true-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task IfFalse_CallsGoalIfFalse()
    {

        var captureStream = new System.IO.MemoryStream();
        _engine.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        var falseGoal = new Goal
        {
            Name = "FalseBranch",
            Path = "/FalseBranch.goal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "write false branch",
                    Actions = new StepActions
                    {
                        new App.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new System.Collections.Generic.List<Data> { new Data("Data", "false-branch") }
                        }
                    }
                }
            }
        };
        _engine.Goals.Add(falseGoal);

        var action = new If
        {
            Context = CreateContext(),
            Left = Data.Ok(false),
            Operator = "==",
            Right = Data.Ok(true),
            GoalIfTrue = new GoalCall { Name = "TrueBranch" },
            GoalIfFalse = new GoalCall { Name = "FalseBranch" }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("false-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task IfTrue_GoalNotFound_ReturnsError()
    {

        var action = new If
        {
            Context = CreateContext(),
            Left = Data.Ok(true),
            Operator = "==",
            Right = Data.Ok(true),
            GoalIfTrue = new GoalCall { Name = "NonExistentGoal" }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- Integration: file.exists → condition.if → output.write ---

    [Test]
    public async Task Integration_FileExists_ConditionTrue_CallsGoal()
    {
        // Arrange: create a real file
        System.IO.File.WriteAllText(System.IO.Path.Combine(_tempDir, "real.txt"), "I exist");


        var captureStream = new System.IO.MemoryStream();
        _engine.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        // Register the goal that condition.if will call
        var writeGoal = new Goal
        {
            Name = "WriteExists",
            Path = "/WriteExists.goal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "write file exists",
                    Actions = new StepActions
                    {
                        new App.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new System.Collections.Generic.List<Data> { new Data("Data", "file exists!") }
                        }
                    }
                }
            }
        };
        _engine.Goals.Add(writeGoal);

        // Build main goal: file.exists → condition.if (using %fileResult.Exists%)
        var mainGoal = new Goal
        {
            Name = "TestConditionFlow",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "check if file exists",
                    Actions = new StepActions
                    {
                        new App.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "file",
                            ActionName = "exists",
                            Parameters = new System.Collections.Generic.List<Data> { new Data("path", System.IO.Path.Combine(_tempDir, "real.txt")) },
                            Return = new System.Collections.Generic.List<Data> { new Data("fileResult") }
                        }
                    }
                },
                new Step
                {
                    Index = 1,
                    Text = "if file exists call WriteExists",
                    Actions = new StepActions
                    {
                        new App.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "condition",
                            ActionName = "if",
                            Parameters = new System.Collections.Generic.List<Data>
                            {
                                new Data("left", "%fileResult.Exists%"),
                                new Data("operator", "=="),
                                new Data("right", true),
                                new Data("goalIfTrue", "WriteExists")
                            }
                        }
                    }
                }
            }
        };

        var context = _engine.CreateContext();
        var goalResult = await _engine.RunGoalAsync(mainGoal, context);

        // Assert: goal succeeded
        await Assert.That(goalResult.Success).IsTrue();

        // Assert: fileResult is in memory
        var fileData = context.Variables.Get("fileResult");
        await Assert.That(fileData).IsNotNull();
        var fileObj = fileData as App.FileSystem.Path;
        await Assert.That(fileObj).IsNotNull();
        await Assert.That(fileObj!.Exists).IsTrue();

        // Assert: output.write was called by the condition's true branch
        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("file exists!" + System.Environment.NewLine);
    }
}
