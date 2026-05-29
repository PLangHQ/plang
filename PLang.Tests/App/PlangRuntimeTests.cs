using app;
using app.actor.context;
using app.variable;
using app.type.path;

namespace PLang.Tests.App;

/// <summary>
/// Tests for the PLang runtime — PLang code executing PLang steps.
/// Validates: kernel dispatch, event resolution, error handling, retry, sub-steps.
/// </summary>
public class PlangRuntimeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;

    public PlangRuntimeTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_runtime_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir);
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    // --- Step 1: Kernel dispatch ---

    [Test]
    public async Task Kernel_Execute_RunsStepActions()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        var step = new Step
        {
            Index = 0,
            Text = "write hello",
            Actions = new StepActions
            {
                new global::app.goal.steps.step.actions.action.@this
                {
                    Module = "output",
                    ActionName = "write",
                    Parameters = new List<Data> { new Data("Data", "hello kernel") }
                }
            }
        };

        var steps = new GoalSteps { step };
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

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
        var context = _app.User.Context;

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
        var context = _app.User.Context;

        // Register a before-step event
        var onAction = new global::app.module.@event.On
        {
            Context = context,
            Type = global::app.@event.EventType.BeforeStep,
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
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

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
                        new global::app.goal.steps.step.actions.action.@this
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

        var context = _app.User.Context;
        var result = await _app.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("hello runtime" + System.Environment.NewLine);
    }
}
