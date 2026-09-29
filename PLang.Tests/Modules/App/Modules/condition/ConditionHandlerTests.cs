using app.actor.context;
using app;
using app.type.item.variable;
using app.module.condition;
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
        _app = new global::app.@this(_tempDir).Testing();
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

    // --- A condition's body is its child steps: the step runs the body only when the condition holds ---

    // A step run through its own door; answers what it wrote to the output channel. A condition holds its
    // body as its child steps (`condition.if(…) { … }`, read as the builder writes it).
    private async Task<string> Written(string text, params Action[] actions)
    {
        var captureStream = new System.IO.MemoryStream();
        _app.actor.list.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });

        var step = new Step { Index = 0, Text = text };
        foreach (var action in actions) step.Code.Add(action.In(step));

        var result = await step.Start(_app.actor.list.User.Context);
        await result.IsSuccess();

        return System.Text.Encoding.UTF8.GetString(captureStream.ToArray());
    }

    [Test]
    public async Task IfTrue_RunsItsBody()
    {
        var ctx = _app.actor.list.User.Context;
        var output = await Written("if true, write true-branch",
            ctx.Action("condition.if(Left=true, Operator=\"==\", Right=true) { output.write(Data=\"true-branch\") }"));
        await Assert.That(output).IsEqualTo("true-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task IfFalse_RunsTheElseBody()
    {
        var ctx = _app.actor.list.User.Context;
        var output = await Written("if false write then-branch, else write else-branch",
            ctx.Action("condition.if(Left=false, Operator=\"==\", Right=true) { output.write(Data=\"then-branch\") }"),
            ctx.Action("condition.else() { output.write(Data=\"else-branch\") }"));
        await Assert.That(output).IsEqualTo("else-branch" + System.Environment.NewLine);
    }
}
