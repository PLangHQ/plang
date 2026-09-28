using app;
using app.actor.context;
using app.type.item.variable;
using app.type.item.path;

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
        _app = TestApp.Create(_tempDir);
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
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        var step = new Step
        {
            Index = 0,
            Text = "write hello",
            Code = new StepActions
            {
                new global::app.goal.step.action.@this
                {
                    Module = global::PLang.Tests.TestApp.SharedContext.App.Module("output"),
                    Name = "write",
                    Property = global::PLang.Tests.Shared.Make.Properties(new List<Data> { new Data("Data", "hello kernel", context: global::PLang.Tests.TestApp.SharedContext) })
                }
            }
        };

        var steps = new GoalSteps { step };
        var context = _app.User.Context;
        var result = await steps.Start(context);

        await result.IsSuccess();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("hello kernel" + System.Environment.NewLine);
    }

    // --- Step 2: IEvent + Event resolution ---

    [Test]
    public async Task Step_On_Start_Before_IsEmptyWhenNothingIsBound()
    {
        var step = new Step { Index = 0, Text = "test step" };

        await Assert.That(step.on["start"]!.before.Count).IsEqualTo(0);
        await Assert.That(_app.type.list["step"].on["start"]!.before.Count).IsEqualTo(0);
    }

    [Test]
    public async Task OnEvent_BeforeEachStep_BindsOnTheStepTypesStart()
    {
        var context = _app.User.Context;

        var onAction = new global::app.module.action.on.OnEvent(context)
        {
            Item = new global::app.data.@this<global::app.type.item.@this>("Item", _app.type.list["step"], context: context),
            When = (global::app.type.item.choice.@this<global::app.@event.When>)global::app.@event.When.before,
            Event = (global::app.type.item.text.@this)"start",
            Action = Make.Call("LogBefore"),
        };
        await onAction.Start();

        var before = _app.type.list["step"].on["start"]!.before;
        await Assert.That(before.Count).IsEqualTo(1);
        await Assert.That(ReferenceEquals(before[0].Actor, _app.User)).IsTrue();
    }

    // --- Step 5: Full PLang runtime loop ---

    [Test]
    public async Task PlangRuntime_SimpleGoal_ExecutesThroughRunGoal()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("TestGoal",
            Make.Step("write hello",
                Make.Action("output", "write", ("Data", "hello runtime")))));

        var context = _app.User.Context;
        var result = await _app.Start(goal, context);

        await result.IsSuccess();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("hello runtime" + System.Environment.NewLine);
    }
}
