using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition;
using PLang.SafeFileSystem;

namespace PLang.Tests.Runtime2.Modules.condition;

public class IfHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly PLang.Runtime2.Engine.@this _engine;

    public IfHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        _engine = new PLang.Runtime2.Engine.@this(_tempDir, fileSystem: _fs);
    }

    public void Dispose()
    {
        _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private PLangContext CreateContext() => _engine.CreateContext();

    [Test]
    public async Task Run_Truthy_InitializedNonBool_ReturnsTrue()
    {
        var action = new If { Context = CreateContext(), Left = Data.Ok(42), Operator = "==", Right = Data.Ok(true) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Run_Truthy_UninitializedLeft_ReturnsFalse()
    {
        var action = new If { Context = CreateContext(), Left = new Data(""), Operator = "==", Right = Data.Ok(true) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Run_WithOperator_DelegatesToEvaluator()
    {
        var action = new If { Context = CreateContext(), Left = Data.Ok(10), Operator = ">", Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Run_ConditionTrue_GoalIfTrue_CallsGoal()
    {
        var captureStream = new System.IO.MemoryStream();
        _engine.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

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
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new List<Data> { new Data("content", "true-branch") }
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
            GoalIfTrue = new GoalCall { Name = "TrueBranch" }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("true-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task Run_ConditionFalse_GoalIfFalse_CallsGoal()
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
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new List<Data> { new Data("content", "false-branch") }
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
    public async Task Run_ConditionTrue_NoGoalIfTrue_ReturnsTrueNoCall()
    {
        var action = new If { Context = CreateContext(), Left = Data.Ok(10), Operator = ">", Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Run_ConditionFalse_NoGoals_ReturnsFalse()
    {
        var action = new If { Context = CreateContext(), Left = Data.Ok(3), Operator = ">", Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Run_TrueCondition_ReturnsBoolTrue()
    {
        var action = new If { Context = CreateContext(), Left = Data.Ok(10), Operator = ">", Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value is bool).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Run_FalseCondition_ReturnsBoolFalse()
    {
        var action = new If { Context = CreateContext(), Left = Data.Ok(3), Operator = ">", Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value is bool).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    public async Task Run_GoalExecutionFails_PropagatesError()
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
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Run_UnsupportedOperator_ThrowsOnConstruction()
    {
        await Assert.That(() => new Operator("xor")).ThrowsException()
            .WithMessageMatching("*Unsupported operator*");
    }

    [Test]
    public async Task Run_IncompatibleComparisonTypes_ReturnsEvaluationError()
    {
        var action = new If { Context = CreateContext(), Left = Data.Ok(new object()), Operator = ">", Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
        await Assert.That(result.Error!.Message).Contains("does not support comparison");
    }
}
