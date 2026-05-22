using app;
using app.actor.context;
using app.variables;
using app.modules.condition;
using app.types.path;
using app.types.path.Default;
using Action = global::app.goals.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.Modules.condition;

public class IfHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly global::app.@this _app;

    public IfHandlerTests()
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

    [Test]
    public async Task Run_Truthy_InitializedNonBool_ReturnsTrue()
    {
        var action = new If { Context = _app.User.Context, Left = Data.Ok(42), Operator = new Operator("=="), Right = Data.Ok(true) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Run_Truthy_UninitializedLeft_ReturnsFalse()
    {
        var action = new If { Context = _app.User.Context, Left = new Data(""), Operator = new Operator("=="), Right = Data.Ok(true) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Run_WithOperator_DelegatesToEvaluator()
    {
        var action = new If { Context = _app.User.Context, Left = Data.Ok(10), Operator = new Operator(">"), Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Run_ConditionTrue_OrchestrateThenBranch()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        // Build a step with: condition.if, then output.write
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
    public async Task Run_ConditionFalse_SkipsThenBranch()
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
            Parameters = new List<Data> { new Data("Data", "should-not-appear") }
        };

        var step = new Step
        {
            Index = 0, Text = "if false, write (should skip)",
            Actions = new StepActions { condAction, thenAction }
        };
        condAction.Step = step;

        var result = await step.RunAsync(_app.User.Context);

        await Assert.That(result.Success).IsTrue();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("");
    }

    [Test]
    public async Task Run_IfElse_TrueRunsThen()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        // if true → write "then", else → write "else"
        var condAction = new Action
        {
            Module = "condition", ActionName = "if",
            Parameters = new List<Data>
            {
                new Data("Left", 10), new Data("Operator", ">"), new Data("Right", 5)
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
            Index = 0, Text = "if x > 5 write then, else write else",
            Actions = new StepActions { condAction, thenAction, elseCondAction, elseAction }
        };
        condAction.Step = step;

        var result = await step.RunAsync(_app.User.Context);

        await Assert.That(result.Success).IsTrue();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("then-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task Run_IfElse_FalseRunsElse()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        // if false → skip then, else always true → write "else"
        var condAction = new Action
        {
            Module = "condition", ActionName = "if",
            Parameters = new List<Data>
            {
                new Data("Left", 3), new Data("Operator", ">"), new Data("Right", 5)
            }
        };
        var thenAction = new Action
        {
            Module = "output", ActionName = "write",
            Parameters = new List<Data> { new Data("Data", "then-branch") }
        };
        // "else" is a condition that's always true
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
            Index = 0, Text = "if x > 5 write then, else write else",
            Actions = new StepActions { condAction, thenAction, elseCondAction, elseAction }
        };
        condAction.Step = step;

        var result = await step.RunAsync(_app.User.Context);

        await Assert.That(result.Success).IsTrue();

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("else-branch" + System.Environment.NewLine);
    }

    [Test]
    public async Task Run_ConditionTrue_NoGoalIfTrue_ReturnsTrueNoCall()
    {
        var action = new If { Context = _app.User.Context, Left = Data.Ok(10), Operator = new Operator(">"), Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Run_ConditionFalse_NoGoals_ReturnsFalse()
    {
        var action = new If { Context = _app.User.Context, Left = Data.Ok(3), Operator = new Operator(">"), Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Run_TrueCondition_ReturnsBoolTrue()
    {
        var action = new If { Context = _app.User.Context, Left = Data.Ok(10), Operator = new Operator(">"), Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value is bool).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Run_FalseCondition_ReturnsBoolFalse()
    {
        var action = new If { Context = _app.User.Context, Left = Data.Ok(3), Operator = new Operator(">"), Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value is bool).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    public async Task Run_Negate_FlipsTrue_ToFalse()
    {
        var action = new If { Context = _app.User.Context, Left = Data.Ok(10), Operator = new Operator(">"), Right = Data.Ok(5), Negate = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Run_Negate_FlipsFalse_ToTrue()
    {
        var action = new If { Context = _app.User.Context, Left = Data.Ok(3), Operator = new Operator(">"), Right = Data.Ok(5), Negate = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
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
        var action = new If { Context = _app.User.Context, Left = data, Operator = new Operator("=="), Right = Data.Ok(true) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Run_EqualsTrueWithToBooleanFalse_ReturnsFalse()
    {
        var data = new TestData(false);
        var action = new If { Context = _app.User.Context, Left = data, Operator = new Operator("=="), Right = Data.Ok(true) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(false);
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
        var action = new If { Context = _app.User.Context, Left = Data.Ok(new object()), Operator = new Operator(">"), Right = Data.Ok(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("EvaluationError");
        await Assert.That(result.Error!.Message).Contains("does not support comparison");
    }

    /// <summary>
    /// Simulates: outer goal has if/else → then-branch calls inner goal →
    /// inner goal also has if/else. The inner condition must orchestrate independently.
    /// Bug: shared guard variable on Context.Variables blocks inner orchestration.
    /// </summary>
    [Test]
    public async Task Run_InnerGoalCondition_OrchestatesIndependently()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        // --- Inner goal: if true → write "inner-then", else → write "inner-else" ---
        var innerCondAction = new Action
        {
            Module = "condition", ActionName = "if",
            Parameters = new List<Data>
            {
                new Data("Left", true), new Data("Operator", "=="), new Data("Right", true)
            }
        };
        var innerThenAction = new Action
        {
            Module = "output", ActionName = "write",
            Parameters = new List<Data> { new Data("Data", "inner-then") }
        };
        var innerElseCondAction = new Action
        {
            Module = "condition", ActionName = "if",
            Parameters = new List<Data>
            {
                new Data("Left", true), new Data("Operator", "=="), new Data("Right", true)
            }
        };
        var innerElseAction = new Action
        {
            Module = "output", ActionName = "write",
            Parameters = new List<Data> { new Data("Data", "inner-else") }
        };

        var innerStep = new Step
        {
            Index = 0, Text = "if true write inner-then, else write inner-else",
            Actions = new StepActions { innerCondAction, innerThenAction, innerElseCondAction, innerElseAction }
        };
        innerCondAction.Step = innerStep;

        // Simulate the bug: the outer goal's condition has already set the guard
        // on the SAME context (because RunGoalAsync passes context by reference).
        // With the buggy code (Variables-based guard), the inner condition sees it
        // and skips orchestration — actions run sequentially instead of branched.
        var context = _app.User.Context;
        context.Variables.Set(new Data("__condition_orchestrating__", true));

        // Run the inner step (which shares the same context as the outer)
        var result = await innerStep.RunAsync(context);

        await Assert.That(result.Success).IsTrue();

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

public class TestData : Data
{
    private readonly bool _boolean;
    public TestData(bool boolean) : base("test", "some-value") => _boolean = boolean;
    public override bool ToBoolean() => _boolean;
}
