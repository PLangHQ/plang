using app;
using app.actor.context;
using app.variable;
using app.type.item.path;

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
    private readonly global::app.@this _app;

    public IfErrorOrchestrationTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_if_err_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
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
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("IfCallMissing",
            Make.Step("if true, call DoesNotExist",
                Make.Action("condition", "if", ("Left", true), ("Operator", "=="), ("Right", true)),
                Make.Action("goal", "call",
                    ("goalname", new Dictionary<string, object?> { ["name"] = "DoesNotExist" })))));
        var step = goal.Step.list.First();

        var result = await step.Run(_app.User.Context);

        // The 404 must surface. Handled=true on condition.if's result is a
        // control-flow signal to Step.RunAsync (don't re-iterate siblings),
        // not a license to swallow the error. Pin the error identity so an
        // unrelated error path leaking through wouldn't pass.
        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task If_OrchestratedSuccess_MarksResultHandled()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("IfWriteRan",
            Make.Step("if true, write ran",
                Make.Action("condition", "if", ("Left", true), ("Operator", "=="), ("Right", true)),
                Make.Action("output", "write", ("Data", "ran")))));
        var step = goal.Step.list.First();

        var result = await step.Run(_app.User.Context);

        await result.IsSuccess();

        // Sanity: branch actually ran.
        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("ran" + System.Environment.NewLine);

        // Handled must be set so parents know the siblings were consumed.
        // Without this, Step.RunAsync would double-execute write-ran.
        await Assert.That(result.Handled).IsTrue();
    }
}
