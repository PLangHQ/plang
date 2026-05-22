using app;
using app.actor.context;
using app.variables;
using app.modules.condition;
using app.types.path;

namespace PLang.Tests.App.Modules.condition;

public class StepsSubStepTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;

    public StepsSubStepTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir);
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    /// <summary>
    /// Creates a step that runs a condition.if action returning the given bool value.
    /// </summary>
    private Step MakeConditionStep(int index, int indent, bool conditionResult)
    {
        return new Step
        {
            Index = index,
            Indent = indent,
            Text = $"if condition = {conditionResult}",
            Actions = new StepActions
            {
                new global::app.goals.goal.steps.step.actions.action.@this
                {
                    Module = "condition",
                    ActionName = "if",
                    Parameters = new List<Data>
                    {
                        new Data("left", conditionResult),
                        new Data("operator", "=="),
                        new Data("right", true)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates a step that writes a marker string to the output channel.
    /// </summary>
    private Step MakeOutputStep(int index, int indent, string marker)
    {
        return new Step
        {
            Index = index,
            Indent = indent,
            Text = $"write {marker}",
            Actions = new StepActions
            {
                new global::app.goals.goal.steps.step.actions.action.@this
                {
                    Module = "output",
                    ActionName = "write",
                    Parameters = new List<Data> { new Data("Data", marker) }
                }
            }
        };
    }

    /// <summary>
    /// Creates a step that sets a variable (returns the variable value, not a bool).
    /// </summary>
    private Step MakeSetStep(int index, int indent, string varName, object? value)
    {
        return new Step
        {
            Index = index,
            Indent = indent,
            Text = $"set {varName} = {value}",
            Actions = new StepActions
            {
                new global::app.goals.goal.steps.step.actions.action.@this
                {
                    Module = "variable",
                    ActionName = "set",
                    Parameters = new List<Data>
                    {
                        new Data("name", varName),
                        new Data("value", value)
                    }
                }
            }
        };
    }

    private (System.IO.MemoryStream stream, Func<string> getOutput) SetupCapture()
    {
        var stream = new System.IO.MemoryStream();
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, stream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        return (stream, () =>
        {
            stream.Position = 0;
            return new System.IO.StreamReader(stream).ReadToEnd();
        });
    }

    [Test]
    public async Task RunAsync_FalseCondition_SkipsIndentedChildren()
    {
        var (_, getOutput) = SetupCapture();
        var steps = new GoalSteps
        {
            MakeConditionStep(0, 0, false),
            MakeOutputStep(1, 4, "should-be-skipped")
        };
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(getOutput()).DoesNotContain("should-be-skipped");
    }

    [Test]
    public async Task RunAsync_TrueCondition_ExecutesIndentedChildren()
    {
        var (_, getOutput) = SetupCapture();
        var steps = new GoalSteps
        {
            MakeConditionStep(0, 0, true),
            MakeOutputStep(1, 4, "child-executed")
        };
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(getOutput()).Contains("child-executed");
    }

    [Test]
    public async Task RunAsync_FalseCondition_ResumesAtSameIndent()
    {
        var (_, getOutput) = SetupCapture();
        var steps = new GoalSteps
        {
            MakeConditionStep(0, 0, false),
            MakeOutputStep(1, 4, "child-skipped"),
            MakeOutputStep(2, 0, "next-runs")
        };
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        var output = getOutput();
        await Assert.That(output).DoesNotContain("child-skipped");
        await Assert.That(output).Contains("next-runs");
    }

    [Test]
    public async Task RunAsync_NestedConditions_InnerFalseSkipsOnlyInner()
    {
        var (_, getOutput) = SetupCapture();
        var steps = new GoalSteps
        {
            MakeConditionStep(0, 0, true),        // outer true → children execute
            MakeConditionStep(1, 4, false),        // inner false → inner children skipped
            MakeOutputStep(2, 8, "inner-skipped"), // inner child
            MakeOutputStep(3, 4, "outer-runs")     // outer child at indent 4
        };
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        var output = getOutput();
        await Assert.That(output).DoesNotContain("inner-skipped");
        await Assert.That(output).Contains("outer-runs");
    }

    [Test]
    public async Task RunAsync_NoIndentedChildren_FalseDoesNotSkip()
    {
        var (_, getOutput) = SetupCapture();
        var steps = new GoalSteps
        {
            MakeConditionStep(0, 0, false),  // false but no indented children
            MakeOutputStep(1, 0, "next-runs") // same indent — not a child
        };
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(getOutput()).Contains("next-runs");
    }

    [Test]
    public async Task RunAsync_TwoConsecutiveConditions_EachControlsOwnBlock()
    {
        var (_, getOutput) = SetupCapture();
        var steps = new GoalSteps
        {
            MakeConditionStep(0, 0, false),
            MakeOutputStep(1, 4, "child-A-skipped"),
            MakeConditionStep(2, 0, true),
            MakeOutputStep(3, 4, "child-B-runs")
        };
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        var output = getOutput();
        await Assert.That(output).DoesNotContain("child-A-skipped");
        await Assert.That(output).Contains("child-B-runs");
    }

    [Test]
    public async Task RunAsync_DeeplyNested_ThreeLevels()
    {
        var (_, getOutput) = SetupCapture();
        var steps = new GoalSteps
        {
            MakeConditionStep(0, 0, true),
            MakeConditionStep(1, 4, true),
            MakeOutputStep(2, 8, "leaf-runs")
        };
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(getOutput()).Contains("leaf-runs");
    }

    [Test]
    public async Task RunAsync_NonConditionStep_FalseValue_DoesNotSkip()
    {
        var (_, getOutput) = SetupCapture();
        // variable.set with value false — returns a variable types object, not bool false
        // Even if it somehow returned false, the child should not be skipped
        // because set returns Data.Ok(variable{...}), not Data.Ok(false)
        var steps = new GoalSteps
        {
            MakeSetStep(0, 0, "myVar", false),
            MakeOutputStep(1, 4, "child-runs")
        };
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        // variable.set returns Data.Ok({name, value, type}) — Value is a variable object, not bool false
        // So HasIndentedChildren check passes but Value is not bool false → children execute
        await Assert.That(getOutput()).Contains("child-runs");
    }

    [Test]
    public async Task RunAsync_HasIndentedChildren_CorrectDetection()
    {
        var steps = new GoalSteps
        {
            MakeOutputStep(0, 0, "parent"),
            MakeOutputStep(1, 4, "child"),
            MakeOutputStep(2, 0, "sibling")
        };

        await Assert.That(steps.HasIndentedChildren(0)).IsTrue();  // step[1].Indent > step[0].Indent
        await Assert.That(steps.HasIndentedChildren(1)).IsFalse(); // step[2].Indent < step[1].Indent
        await Assert.That(steps.HasIndentedChildren(2)).IsFalse(); // no step[3]
    }
}
