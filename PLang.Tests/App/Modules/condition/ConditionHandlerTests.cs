using global::app.actor.context;
using app;
using global::app.variables;
using global::app.modules.condition;
using global::app.filesystem;
using global::app.filesystem.Default;
using Action = global::app.goals.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.actions.condition;

public class ConditionHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly global::app.@this _app;

    public ConditionHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
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

    // --- Unit tests: no branching ---

    [Test]
    public async Task IfTrue_NoGoals_ReturnsSuccessWithTrue()
    {
        var action = new If { Context = _app.User.Context, Left = Data.Ok(true), Operator = new Operator("=="), Right = Data.Ok(true) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task IfFalse_NoGoals_ReturnsSuccessWithFalse()
    {
        var action = new If { Context = _app.User.Context, Left = Data.Ok(false), Operator = new Operator("=="), Right = Data.Ok(true) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    // --- Orchestration tests: condition + actions in same step ---

    [Test]
    public async Task IfTrue_Orchestrate_RunsThenBranch()
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
        var thenAction = new Action
        {
            Module = "output", ActionName = "write",
            Parameters = new List<Data> { new Data("Data", "true-branch") }
        };

        var step = new Step
        {
            Index = 0, Text = "if true, write true-branch",
            Actions = new StepActions { condAction, thenAction }
        };
        condAction.Step = step;

        var result = await step.RunAsync(_app.User.Context);

        await Assert.That(result.Success).IsTrue();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("true-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task IfFalse_Orchestrate_RunsElseBranch()
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
                new Data("Left", false), new Data("Operator", "=="), new Data("Right", true)
            }
        };
        var thenAction = new Action
        {
            Module = "output", ActionName = "write",
            Parameters = new List<Data> { new Data("Data", "then-branch") }
        };
        var elseCondAction = new Action
        {
            Module = "condition", ActionName = "if",
            Parameters = new List<Data>
            {
                new Data("Left", true), new Data("Operator", "=="), new Data("Right", true)
            }
        };
        var elseAction = new Action
        {
            Module = "output", ActionName = "write",
            Parameters = new List<Data> { new Data("Data", "else-branch") }
        };

        var step = new Step
        {
            Index = 0, Text = "if false then, else write else",
            Actions = new StepActions { condAction, thenAction, elseCondAction, elseAction }
        };
        condAction.Step = step;

        var result = await step.RunAsync(_app.User.Context);

        await Assert.That(result.Success).IsTrue();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("else-branch" + System.Environment.NewLine);
    }
}
