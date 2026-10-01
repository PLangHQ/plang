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

    // Two values that don't compare are named by what they are, not by the slots that held them: the condition's
    // Left and Right are `%variables%` in slots typed `item` — the educator's "'item' and 'item'", a digest against
    // a text that is no digest's text.
    [Test]
    public async Task Compare_Incomparable_NamesTheValuesTypes_NotTheSlots()
    {
        var goal = await RealGoalLoad.Read(_app, """
            {"name": "Compare", "path": "/Compare.goal", "step": [
              {"index": 0, "text": "hash abc", "line": {"number": 2}, "code": [
                {"module": "crypto", "name": "hash", "property": [
                  {"name": "Data", "type": {"name": "text"}, "value": "abc"},
                  {"name": "Algorithm", "type": {"name": "text"}, "value": "sha256"}]},
                {"module": "variable", "name": "set", "property": [
                  {"name": "Name", "type": {"name": "variable"}, "value": "%digest%", "variable": [{"text": "%digest%", "code": [{"variable": "digest"}]}]},
                  {"name": "Value", "type": {"name": "item", "template": "plang"}, "value": "%!data%", "variable": [{"text": "%!data%", "code": [{"variable": "!data"}]}]}]}]},
              {"index": 1, "text": "set word", "line": {"number": 3}, "code": [
                {"module": "variable", "name": "set", "property": [
                  {"name": "Name", "type": {"name": "variable"}, "value": "%word%", "variable": [{"text": "%word%", "code": [{"variable": "word"}]}]},
                  {"name": "Value", "type": {"name": "text"}, "value": "hello"}]}]},
              {"index": 2, "text": "compare them", "line": {"number": 4}, "code": [
                {"module": "condition", "name": "compare", "property": [
                  {"name": "Left", "type": {"name": "item", "template": "plang"}, "value": "%digest%", "variable": [{"text": "%digest%", "code": [{"variable": "digest"}]}]},
                  {"name": "Operator", "type": {"name": "choice", "kind": "operator"}, "value": "=="},
                  {"name": "Right", "type": {"name": "item", "template": "plang"}, "value": "%word%", "variable": [{"text": "%word%", "code": [{"variable": "word"}]}]}]}]}]}
            """);
        var ctx = _app.actor.list.User.Context;

        await (await goal.Step[0].Start(ctx)).IsSuccess();
        await (await goal.Step[1].Start(ctx)).IsSuccess();
        var compared = await goal.Step[2].Start(ctx);

        await compared.IsFailure();
        await Assert.That(compared.Error!.Message).Contains("'hash' and 'text'");
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
        { Mime = "text/plain", Framed = true });

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
