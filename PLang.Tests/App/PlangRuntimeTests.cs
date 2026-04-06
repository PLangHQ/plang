using App;
using App.Context;
using App.Variables;
using App.FileSystem;
using App.FileSystem.Default;

namespace PLang.Tests.App;

/// <summary>
/// Tests for the PLang runtime — PLang code executing PLang steps.
/// Validates: kernel dispatch, event resolution, error handling, retry, sub-steps.
/// </summary>
public class PlangRuntimeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly App.@this _engine;

    public PlangRuntimeTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_runtime_test_" + Guid.NewGuid().ToString("N"));
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

    // --- Step 1: Kernel dispatch ---

    [Test]
    public async Task Kernel_Execute_RunsStepActions()
    {
        var captureStream = new System.IO.MemoryStream();
        _engine.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        var step = new Step
        {
            Index = 0,
            Text = "write hello",
            Actions = new StepActions
            {
                new App.Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = "output",
                    ActionName = "write",
                    Parameters = new List<Data> { new Data("Data", "hello kernel") }
                }
            }
        };

        var steps = new GoalSteps { step };
        var context = _engine.CreateContext();
        var result = await _engine.RunSteps(steps, context);

        await Assert.That(result.Success).IsTrue();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("hello kernel" + System.Environment.NewLine);
    }

    // --- Step 2: IEvent + Event resolution ---

    [Test]
    public async Task Step_Event_Before_ReturnsEmptyWhenNoBindings()
    {
        var step = new Step { Index = 0, Text = "test step" };
        var context = _engine.CreateContext();

        // Step implements IEvent — Event property should exist
        await Assert.That(step.Events).IsNotNull();

        // Inject context so Event can resolve bindings
        step.Events.Context = context;

        await Assert.That(step.Events.Before).IsNotNull();
        await Assert.That(step.Events.Before.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Step_Event_Before_ReturnsMatchingBindings()
    {
        var context = _engine.CreateContext();

        // Register a before-step event
        var onAction = new App.modules.@event.On
        {
            Context = context,
            Type = "BeforeStep",
            GoalToCall = new GoalCall { Name = "LogBefore" },
            StepPattern = "*"
        };
        await onAction.Run();

        var step = new Step { Index = 0, Text = "write hello" };
        step.Events.Context = context;

        var bindings = step.Events.Before;
        await Assert.That(bindings.Count).IsGreaterThan(0);
        await Assert.That(bindings[0].Name).IsEqualTo("LogBefore");
    }

    // --- Step 5: Full PLang runtime loop ---

    [Test]
    public async Task PlangRuntime_SimpleGoal_ExecutesThroughRunGoal()
    {
        var captureStream = new System.IO.MemoryStream();
        _engine.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        var goal = new Goal
        {
            Name = "TestGoal",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "write hello",
                    Actions = new StepActions
                    {
                        new App.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new List<Data> { new Data("Data", "hello runtime") }
                        }
                    }
                }
            }
        };
        foreach (var s in goal.Steps) s.Goal = goal;

        var context = _engine.CreateContext();
        var result = await _engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("hello runtime" + System.Environment.NewLine);
    }
}
