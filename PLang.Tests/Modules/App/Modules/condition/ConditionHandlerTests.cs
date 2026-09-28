using app.actor.context;
using app;
using app.type.item.variable;
using app.module.action.condition;
using Operator = global::app.data.Operator;
using app.type.item.path;
using Action = global::app.goal.step.action.@this;

namespace PLang.Tests.App.actions.condition;

public class ConditionHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;

    public ConditionHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
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
        var action = new If(_app.actor.list.User.Context) { Left = _app.actor.list.User.Context.Ok(true), Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator("==")), Right = _app.actor.list.User.Context.Ok(true) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task IfFalse_NoGoals_ReturnsSuccessWithFalse()
    {
        var action = new If(_app.actor.list.User.Context) { Left = _app.actor.list.User.Context.Ok(false), Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator("==")), Right = _app.actor.list.User.Context.Ok(true) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    // --- Orchestration tests: condition + actions in same step ---

    [Test]
    public async Task IfTrue_Orchestrate_RunsThenBranch()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.actor.list.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        var condAction = new Action
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("condition"), Name = "if",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<Data>
            {
                new Data("Left", true, context: _app.actor.list.User.Context), new Data("Operator", "==", context: _app.actor.list.User.Context), new Data("Right", true, context: _app.actor.list.User.Context)
            })
        };
        var thenAction = new Action
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("output"), Name = "write",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<Data> { new Data("Data", "true-branch", context: _app.actor.list.User.Context) })
        };

        var step = new Step { Index = 0, Text = "if true, write true-branch" };
        step.Code.Add(condAction.In(step));
        step.Code.Add(thenAction);

        var result = await step.Start(_app.actor.list.User.Context);

        await result.IsSuccess();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("true-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task IfFalse_Orchestrate_RunsElseBranch()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.actor.list.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        var condAction = new Action
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("condition"), Name = "if",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<Data>
            {
                new Data("Left", false, context: _app.actor.list.User.Context), new Data("Operator", "==", context: _app.actor.list.User.Context), new Data("Right", true, context: _app.actor.list.User.Context)
            })
        };
        var thenAction = new Action
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("output"), Name = "write",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<Data> { new Data("Data", "then-branch", context: _app.actor.list.User.Context) })
        };
        var elseCondAction = new Action
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("condition"), Name = "if",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<Data>
            {
                new Data("Left", true, context: _app.actor.list.User.Context), new Data("Operator", "==", context: _app.actor.list.User.Context), new Data("Right", true, context: _app.actor.list.User.Context)
            })
        };
        var elseAction = new Action
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("output"), Name = "write",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<Data> { new Data("Data", "else-branch", context: _app.actor.list.User.Context) })
        };

        var step = new Step { Index = 0, Text = "if false then, else write else" };
        step.Code.Add(condAction.In(step));
        step.Code.Add(thenAction);
        step.Code.Add(elseCondAction);
        step.Code.Add(elseAction);

        var result = await step.Start(_app.actor.list.User.Context);

        await result.IsSuccess();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("else-branch" + System.Environment.NewLine);
    }
}
