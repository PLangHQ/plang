using app;
using app.actor.context;
using app.variable;
using app.module.condition;
using app.type.path;
using Action = global::app.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.Modules.condition;

public class IfHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;

    public IfHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
    }

    // Runs a step's actions through the REAL read path (a goal off a stream channel),
    // so the actions assemble and their params type/stamp like a .pr off disk —
    // instead of the hand-built shape that bypasses the read.
    private async Task<Data> RunStep(string text, params Action[] actions)
    {
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("G", Make.Step(text, actions)));
        return await _app.RunGoalAsync(goal, _app.User.Context);
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
        var action = new If(_app.User.Context) { Left = _app.User.Context.Ok(42), Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator("==")), Right = _app.User.Context.Ok(true) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task Run_Truthy_UninitializedLeft_ReturnsFalse()
    {
        var action = new If(_app.User.Context) { Left = new Data("", context: _app.User.Context), Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator("==")), Right = _app.User.Context.Ok(true) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    [Test]
    public async Task Run_WithOperator_DelegatesToEvaluator()
    {
        var action = new If(_app.User.Context) { Left = _app.User.Context.Ok(10), Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.User.Context.Ok(5) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task Run_ConditionTrue_OrchestrateThenBranch()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        // A step with: condition.if, then output.write
        var result = await RunStep("if true, write true-branch",
            Make.Action("condition", "if", ("Left", true), ("Operator", "=="), ("Right", true)),
            Make.Action("output", "write", ("Data", "true-branch")));

        await result.IsSuccess();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("true-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task Run_ConditionFalse_SkipsThenBranch()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        var result = await RunStep("if false, write (should skip)",
            Make.Action("condition", "if", ("Left", false), ("Operator", "=="), ("Right", true)),
            Make.Action("output", "write", ("Data", "should-not-appear")));

        await result.IsSuccess();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("");
    }

    [Test]
    public async Task Run_IfElse_TrueRunsThen()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        // if true → write "then", else → write "else"
        var result = await RunStep("if x > 5 write then, else write else",
            Make.Action("condition", "if", ("Left", 10), ("Operator", ">"), ("Right", 5)),
            Make.Action("output", "write", ("Data", "then-branch")),
            Make.Action("condition", "if", ("Left", true), ("Operator", "=="), ("Right", true)),
            Make.Action("output", "write", ("Data", "else-branch")));

        await result.IsSuccess();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("then-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task Run_IfElse_FalseRunsElse()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        // if false → skip then, else always true → write "else"
        var result = await RunStep("if x > 5 write then, else write else",
            Make.Action("condition", "if", ("Left", 3), ("Operator", ">"), ("Right", 5)),
            Make.Action("output", "write", ("Data", "then-branch")),
            // "else" is a condition that's always true
            Make.Action("condition", "if", ("Left", true), ("Operator", "=="), ("Right", true)),
            Make.Action("output", "write", ("Data", "else-branch")));

        await result.IsSuccess();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("else-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task Run_ConditionTrue_NoGoalIfTrue_ReturnsTrueNoCall()
    {
        var action = new If(_app.User.Context) { Left = _app.User.Context.Ok(10), Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.User.Context.Ok(5) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task Run_ConditionFalse_NoGoals_ReturnsFalse()
    {
        var action = new If(_app.User.Context) { Left = _app.User.Context.Ok(3), Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.User.Context.Ok(5) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    [Test]
    public async Task Run_TrueCondition_ReturnsBoolTrue()
    {
        var action = new If(_app.User.Context) { Left = _app.User.Context.Ok(10), Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.User.Context.Ok(5) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value()) is global::app.type.item.@bool.@this).IsTrue();
        await Assert.That(await result.ToBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task Run_FalseCondition_ReturnsBoolFalse()
    {
        var action = new If(_app.User.Context) { Left = _app.User.Context.Ok(3), Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.User.Context.Ok(5) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value()) is global::app.type.item.@bool.@this).IsTrue();
        await Assert.That(await result.ToBooleanAsync()).IsFalse();
    }

    [Test]
    public async Task Run_Negate_FlipsTrue_ToFalse()
    {
        var action = new If(_app.User.Context) { Left = _app.User.Context.Ok(10), Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.User.Context.Ok(5), Negate = (global::app.type.item.@bool.@this)true };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("false");
    }

    [Test]
    public async Task Run_Negate_FlipsFalse_ToTrue()
    {
        var action = new If(_app.User.Context) { Left = _app.User.Context.Ok(3), Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.User.Context.Ok(5), Negate = (global::app.type.item.@bool.@this)true };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
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
        var action = new If(_app.User.Context) { Left = data, Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator("==")), Right = _app.User.Context.Ok(true) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("true");
    }

    [Test]
    public async Task Run_EqualsTrueWithToBooleanFalse_ReturnsFalse()
    {
        var data = new TestData(false);
        var action = new If(_app.User.Context) { Left = data, Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator("==")), Right = _app.User.Context.Ok(true) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

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
        var action = new If(_app.User.Context) { Left = _app.User.Context.Ok(new object()), Operator = _app.User.Context.Ok<global::app.type.item.choice.@this<Operator>>((global::app.type.item.choice.@this<Operator>)new Operator(">")), Right = _app.User.Context.Ok(5) };
        await action.Attach(null, _app.User.Context);
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
        await Assert.That(result.Error!.Message).Contains("cannot order");
    }

    /// <summary>
    /// Simulates: outer goal has if/else → then-branch calls inner goal →
    /// inner goal also has if/else. The inner condition must orchestrate independently.
    /// Bug: shared guard variable on Context.Variable blocks inner orchestration.
    /// </summary>
    [Test]
    public async Task Run_InnerGoalCondition_OrchestatesIndependently()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        // --- Inner goal: if true → write "inner-then", else → write "inner-else" ---
        // Simulate the bug: the outer goal's condition has already set the guard on the
        // SAME context (RunGoalAsync passes context by reference). With the buggy code
        // (Variables-based guard) the inner condition sees it and skips orchestration —
        // actions run sequentially instead of branched.
        _app.User.Context.Variable.Set(new Data("__condition_orchestrating__", true, context: _app.User.Context));

        var result = await RunStep("if true write inner-then, else write inner-else",
            Make.Action("condition", "if", ("Left", true), ("Operator", "=="), ("Right", true)),
            Make.Action("output", "write", ("Data", "inner-then")),
            Make.Action("condition", "if", ("Left", true), ("Operator", "=="), ("Right", true)),
            Make.Action("output", "write", ("Data", "inner-else")));

        await result.IsSuccess();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();

        // The inner if is true, so "inner-then" should appear.
        // With the bug: orchestration is skipped, step runs actions sequentially,
        // condition returns true (Handled=false), then output.write runs "inner-then",
        // BUT the else condition also runs and writes "inner-else" too.
        // With the fix: orchestration works, only "inner-then" is written.
        await Assert.That(output).IsEqualTo("inner-then" + System.Environment.NewLine);
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
