using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.SafeFileSystem;

namespace PLang.Tests.Runtime2.Engine;

/// <summary>
/// Tests for [Method] attribute — engine methods registered as PLang-callable actions.
/// </summary>
public class MethodAttributeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly PLang.Runtime2.Engine.@this _engine;

    public MethodAttributeTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_method_test_" + Guid.NewGuid().ToString("N"));
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

    [Test]
    public async Task OutputWrite_ViaMethod_WritesToChannel()
    {
        var captureStream = new System.IO.MemoryStream();
        _engine.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        // Build an action that maps to output.write
        var action = new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = "output",
            ActionName = "write",
            Parameters = new List<Data> { new Data("Content", "hello from method") }
        };

        var context = _engine.CreateContext();
        var result = await action.RunAsync(_engine, context);

        await Assert.That(result.Success).IsTrue();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("hello from method" + System.Environment.NewLine);
    }

    [Test]
    public async Task OutputWrite_BeforeEvent_CanInterceptAction()
    {
        var captureStream = new System.IO.MemoryStream();
        _engine.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        var context = _engine.CreateContext();

        // Register a before-action event that skips the write
        var skipGoal = new Goal
        {
            Name = "SkipWrite",
            Path = "/SkipWrite.goal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "skip action",
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "event",
                            ActionName = "skipAction",
                            Parameters = new List<Data> { new Data("Value", "intercepted") }
                        }
                    }
                }
            }
        };
        _engine.Goals.Add(skipGoal);

        // Register before event on output.write
        var onAction = new PLang.Runtime2.modules.@event.On
        {
            Context = context,
            Type = "BeforeAction",
            GoalToCall = new GoalCall { Name = "SkipWrite" },
            ActionPattern = "output.write"
        };
        await onAction.Run();

        // Now try to write — should be intercepted
        var action = new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = "output",
            ActionName = "write",
            Parameters = new List<Data> { new Data("Content", "should not appear") }
        };

        var result = await action.RunAsync(_engine, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("intercepted");

        // Stream should be empty — write was skipped
        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("");
    }
}
