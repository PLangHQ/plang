using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.SafeFileSystem;

namespace PLang.Tests.Runtime2.Engine;

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
    public async Task OutputWrite_DataParameter_WritesToDefaultChannel()
    {
        var captureStream = new System.IO.MemoryStream();
        _engine.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        var action = new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = "output",
            ActionName = "write",
            Parameters = new List<Data> { new Data("Data", "hello world") }
        };

        var context = _engine.CreateContext();
        var result = await action.RunAsync(_engine, context);

        await Assert.That(result.Success).IsTrue();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("hello world" + System.Environment.NewLine);
    }

    [Test]
    public async Task OutputWrite_WithChannelProperty_WritesToNamedChannel()
    {
        var traceStream = new System.IO.MemoryStream();
        _engine.User.Channels.Register(new Channel(
            "trace", traceStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        // Data with channel property
        var data = new Data("Data", "trace message");
        data.Properties.Set("channel", "trace");

        var action = new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = "output",
            ActionName = "write",
            Parameters = new List<Data> { data }
        };

        var context = _engine.CreateContext();
        var result = await action.RunAsync(_engine, context);

        await Assert.That(result.Success).IsTrue();

        traceStream.Position = 0;
        var output = new System.IO.StreamReader(traceStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("trace message" + System.Environment.NewLine);
    }

    [Test]
    public async Task OutputWrite_BeforeEvent_CanIntercept()
    {
        var captureStream = new System.IO.MemoryStream();
        _engine.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        var context = _engine.CreateContext();

        // Register skip goal
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

        // Register before event
        var onAction = new PLang.Runtime2.modules.@event.On
        {
            Context = context,
            Type = "BeforeAction",
            GoalToCall = new GoalCall { Name = "SkipWrite" },
            ActionPattern = "output.write"
        };
        await onAction.Run();

        var action = new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = "output",
            ActionName = "write",
            Parameters = new List<Data> { new Data("Data", "should not appear") }
        };

        var result = await action.RunAsync(_engine, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("intercepted");

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("");
    }
}
