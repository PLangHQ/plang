using app;
using app.actor.context;
using app.type.item.variable;
using app.module.condition;
using Operator = global::app.data.Operator;
using app.type.item.path;
using Action = global::app.goal.step.action.@this;

namespace PLang.Tests.App.Modules.condition;

public class IfHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;

    public IfHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir).Testing();
    }

    // Runs a step's actions through the REAL read path (a goal off a stream channel),
    // so the actions assemble and their params type/stamp like a .pr off disk —
    // instead of the hand-built shape that bypasses the read.
    private async Task<Data> RunStep(string text, params Action[] actions)
    {
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(_app.actor.list.User.Context, "G", Make.Step(text, actions)));
        return await _app.Start(goal, _app.actor.list.User.Context);
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task Run_Truthy_InitializedNonBool_ReturnsTrue()
    {
        var action = new If(_app.actor.list.User.Context) { Left = _app.actor.list.User.Context.Ok(42), Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator("==")), Right = _app.actor.list.User.Context.Ok(true) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task Run_Truthy_UninitializedLeft_ReturnsFalse()
    {
        var action = new If(_app.actor.list.User.Context) { Left = new Data("", context: _app.actor.list.User.Context), Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator("==")), Right = _app.actor.list.User.Context.Ok(true) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    [Test]
    public async Task Run_WithOperator_DelegatesToEvaluator()
    {
        var action = new If(_app.actor.list.User.Context) { Left = _app.actor.list.User.Context.Ok(10), Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.actor.list.User.Context.Ok(5) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    // A step run through the real read path; answers what it wrote to the output channel. A condition
    // holds its body as its child steps (`condition.if(…) { … }`, read as the builder writes it).
    private async Task<string> Written(string text, params Action[] actions)
    {
        var captureStream = new System.IO.MemoryStream();
        _app.actor.list.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });

        var result = await RunStep(text, actions);
        await result.IsSuccess();

        return System.Text.Encoding.UTF8.GetString(captureStream.ToArray());
    }

    [Test]
    public async Task Run_ConditionTrue_RunsItsBody()
    {
        var ctx = _app.actor.list.User.Context;
        var output = await Written("if true, write true-branch",
            ctx.Action("condition.if(Left=true, Operator=\"==\", Right=true) { output.write(Data=\"true-branch\") }"));
        await Assert.That(output).IsEqualTo("true-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task Run_ConditionFalse_SkipsItsBody()
    {
        var ctx = _app.actor.list.User.Context;
        var output = await Written("if false, write should-not-appear",
            ctx.Action("condition.if(Left=false, Operator=\"==\", Right=true) { output.write(Data=\"should-not-appear\") }"));
        await Assert.That(output).IsEqualTo("");
    }

    [Test]
    public async Task Run_IfElse_TrueRunsThen()
    {
        var ctx = _app.actor.list.User.Context;
        var output = await Written("if 10 > 5 write then-branch, else write else-branch",
            ctx.Action("condition.if(Left=10, Operator=\">\", Right=5) { output.write(Data=\"then-branch\") }"),
            ctx.Action("condition.else() { output.write(Data=\"else-branch\") }"));
        await Assert.That(output).IsEqualTo("then-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task Run_IfElse_FalseRunsElse()
    {
        var ctx = _app.actor.list.User.Context;
        var output = await Written("if 3 > 5 write then-branch, else write else-branch",
            ctx.Action("condition.if(Left=3, Operator=\">\", Right=5) { output.write(Data=\"then-branch\") }"),
            ctx.Action("condition.else() { output.write(Data=\"else-branch\") }"));
        await Assert.That(output).IsEqualTo("else-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task Run_ConditionTrue_NoGoalIfTrue_ReturnsTrueNoCall()
    {
        var action = new If(_app.actor.list.User.Context) { Left = _app.actor.list.User.Context.Ok(10), Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.actor.list.User.Context.Ok(5) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task Run_ConditionFalse_NoGoals_ReturnsFalse()
    {
        var action = new If(_app.actor.list.User.Context) { Left = _app.actor.list.User.Context.Ok(3), Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.actor.list.User.Context.Ok(5) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    [Test]
    public async Task Run_TrueCondition_ReturnsBoolTrue()
    {
        var action = new If(_app.actor.list.User.Context) { Left = _app.actor.list.User.Context.Ok(10), Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.actor.list.User.Context.Ok(5) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value()) is global::app.type.item.@bool.@this).IsTrue();
        await Assert.That(await result.ToBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task Run_FalseCondition_ReturnsBoolFalse()
    {
        var action = new If(_app.actor.list.User.Context) { Left = _app.actor.list.User.Context.Ok(3), Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.actor.list.User.Context.Ok(5) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value()) is global::app.type.item.@bool.@this).IsTrue();
        await Assert.That(await result.ToBooleanAsync()).IsFalse();
    }

    private async Task<global::app.data.@this> Ask(object? left, string op, object? right)
    {
        var ctx = _app.actor.list.User.Context;
        var action = new If(ctx)
        {
            Left = left == null ? null : ctx.Ok(left),
            Operator = ctx.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(op)),
            Right = right == null ? null : ctx.Ok(right)
        };
        await action.Attach(null, ctx);
        return await action.Start();
    }

    // Every negative operator is its positive with the answer inverted.
    [Test]
    [Arguments("notcontains", "contains", "hello admin", "admin")]
    [Arguments("notcontains", "contains", "hello", "admin")]
    [Arguments("notstartswith", "startswith", "/api/users", "/api")]
    [Arguments("notstartswith", "startswith", "/web", "/api")]
    [Arguments("notendswith", "endswith", "report.pdf", ".pdf")]
    [Arguments("notendswith", "endswith", "report.txt", ".pdf")]
    [Arguments("isnotempty", "isempty", "", null)]
    [Arguments("isnotempty", "isempty", "x", null)]
    [Arguments("isnot", "is", "text", "text")]
    public async Task NegativeOperator_IsItsPositiveInverted(string negative, string positive, string left, string? right)
    {
        var yes = await Ask(left, positive, right);
        var no = await Ask(left, negative, right);

        await yes.IsSuccess();
        await no.IsSuccess();
        await Assert.That(await no.ToBooleanAsync()).IsEqualTo(!await yes.ToBooleanAsync());
    }

    [Test]
    public async Task NotIn_IsInInverted()
    {
        var list = new List<object?> { "admin", "owner" };
        await Assert.That(await (await Ask("admin", "notin", list)).ToBooleanAsync()).IsFalse();
        await Assert.That(await (await Ask("guest", "notin", list)).ToBooleanAsync()).IsTrue();
    }

    // `if %unset% > 20`: ordering a variable that holds nothing is the developer's error, never a quiet false.
    [Test]
    public async Task Ordering_AnUnsetVariable_IsAnError()
    {
        var ctx = _app.actor.list.User.Context;
        var unset = new global::app.data.@this("Left", "%unset%",
            ctx.App.type.list[new global::app.type.@this("item", template: "plang"), ctx], context: ctx);
        var action = new If(ctx)
        {
            Left = unset,
            Operator = ctx.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")),
            Right = ctx.Ok(20L),
        };
        await action.Attach(null, ctx);

        var result = await action.Start();

        await result.IsFailure();
    }

    [Test]
    public async Task NegativeOperator_KeepsThePositivesError()
    {
        // `is` with a type name that doesn't exist is the developer's error — so is `isnot`.
        var result = await Ask("x", "isnot", "nosuchtype");
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("UnknownType");
    }

    // --- Data.ToBoolean tests ---

    [Test]
    public async Task IsTruthy_DataWithToBooleanTrue_ReturnsTrue()
    {
        var data = new TestData(true);
        await Assert.That(Operator.IsTruthy(data)).IsTrue();
    }

    [Test]
    public async Task IsTruthy_DataWithToBooleanFalse_ReturnsFalse()
    {
        var data = new TestData(false);
        await Assert.That(Operator.IsTruthy(data)).IsFalse();
    }

    [Test]
    public async Task Run_EqualsTrueWithToBooleanTrue_ReturnsTrue()
    {
        var data = new TestData(true);
        var action = new If(_app.actor.list.User.Context) { Left = data, Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator("==")), Right = _app.actor.list.User.Context.Ok(true) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task Run_EqualsTrueWithToBooleanFalse_ReturnsFalse()
    {
        var data = new TestData(false);
        var action = new If(_app.actor.list.User.Context) { Left = data, Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator("==")), Right = _app.actor.list.User.Context.Ok(true) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    [Test]
    public async Task Run_UnsupportedOperator_ThrowsOnConstruction()
    {
        await Assert.That(() => new Operator("xor")).ThrowsException()
            .WithMessageMatching("*Unsupported operator*");
    }

    [Test]
    public async Task Run_IncompatibleComparisonTypes_ReturnsEvaluationError()
    {
        var action = new If(_app.actor.list.User.Context) { Left = _app.actor.list.User.Context.Ok(new object()), Operator = _app.actor.list.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.actor.list.User.Context.Ok(5) };
        await action.Attach(null, _app.actor.list.User.Context);
        var result = await action.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
        await Assert.That(result.Error!.Message).Contains("cannot order");
    }
}

// Born-typed: truthiness belongs to the VALUE (IBooleanResolvable), not a
// Data subclass override — the fixture expresses custom truthiness the way a
// real value (path, bool) does.
public class TestData : Data
{
    public TestData(bool boolean) : base("test", new TruthyValue(boolean)) { }

    private sealed class TruthyValue(bool b) : global::app.type.item.@this, global::app.data.IBooleanResolvable
    {
        public System.Threading.Tasks.Task<bool> AsBooleanAsync() => System.Threading.Tasks.Task.FromResult(b);
        public override bool IsTruthy() => b;
        public override string ToString() => b ? "true" : "false";
    }
}
