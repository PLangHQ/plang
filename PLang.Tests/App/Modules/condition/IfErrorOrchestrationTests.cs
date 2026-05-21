using app;
using app.actor.context;
using app.variables;
using app.filesystem;
using app.filesystem.Default;
using Action = global::app.goals.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.Modules.condition;

/// <summary>
/// Pins condition.if's orchestration contract so the silent-error regression
/// doesn't come back. Two invariants:
///   (1) When the chosen branch's action errors, the error propagates out of
///       Step.RunAsync — NOT swallowed by the Handled flag.
///   (2) On success, Handled=true is set on the orchestrated result so
///       Step.RunAsync knows to stop iterating siblings (otherwise the actions
///       condition.if already ran would be re-executed).
/// </summary>
public class IfErrorOrchestrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly global::app.@this _app;

    public IfErrorOrchestrationTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_if_err_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        _app = new global::app.@this(_tempDir, fileSystem: _fs);
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task If_OrchestratedBranchAction_ReturnsError_PropagatesThroughStep()
    {
        var condAction = new Action
        {
            Module = "condition", ActionName = "if",
            Parameters = new List<Data>
            {
                new Data("Left", true), new Data("Operator", "=="), new Data("Right", true)
            }
        };
        var goalCallAction = new Action
        {
            Module = "goal", ActionName = "call",
            Parameters = new List<Data>
            {
                new Data("goalname", new Dictionary<string, object?> { ["name"] = "DoesNotExist" })
            }
        };

        var step = new Step
        {
            Index = 0, Text = "if true, call DoesNotExist",
            Actions = new StepActions { condAction, goalCallAction }
        };
        condAction.Step = step;
        goalCallAction.Step = step;

        var result = await step.RunAsync(_app.User.Context);

        // The 404 must surface. Handled=true on condition.if's result is a
        // control-flow signal to Step.RunAsync (don't re-iterate siblings),
        // not a license to swallow the error. Pin the error identity so an
        // unrelated error path leaking through wouldn't pass.
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task If_OrchestratedSuccess_MarksResultHandled()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        var condAction = new Action
        {
            Module = "condition", ActionName = "if",
            Parameters = new List<Data>
            {
                new Data("Left", true), new Data("Operator", "=="), new Data("Right", true)
            }
        };
        var writeAction = new Action
        {
            Module = "output", ActionName = "write",
            Parameters = new List<Data> { new Data("Data", "ran") }
        };

        var step = new Step
        {
            Index = 0, Text = "if true, write ran",
            Actions = new StepActions { condAction, writeAction }
        };
        condAction.Step = step;
        writeAction.Step = step;

        var result = await step.RunAsync(_app.User.Context);

        await Assert.That(result.Success).IsTrue();

        // Sanity: branch actually ran.
        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("ran" + System.Environment.NewLine);

        // Handled must be set so parents know the siblings were consumed.
        // Without this, Step.RunAsync would double-execute write-ran.
        await Assert.That(result.Handled).IsTrue();
    }
}
