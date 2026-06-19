using app;
using app.actor.context;
using app.variable;
using app.module.condition;
using app.type.path;

namespace PLang.Tests.App.Modules.condition;

public class StepsSubStepTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;

    public StepsSubStepTests()
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

    /// <summary>
    /// A step that runs a condition.if action returning the given bool value.
    /// </summary>
    private static Make.StepDef MakeConditionStep(int indent, bool conditionResult)
        => Make.Step($"if condition = {conditionResult}", indent,
            Make.Action("condition", "if",
                ("left", conditionResult), ("operator", "=="), ("right", true)));

    /// <summary>
    /// A step that writes a marker string to the output channel.
    /// </summary>
    private static Make.StepDef MakeOutputStep(int indent, string marker)
        => Make.Step($"write {marker}", indent,
            Make.Action("output", "write", ("Data", marker)));

    /// <summary>
    /// A step that sets a variable (returns the variable value, not a bool).
    /// </summary>
    private static Make.StepDef MakeSetStep(int indent, string varName, object? value)
        => Make.Step($"set {varName} = {value}", indent,
            Make.Action("variable", "set",
                Make.Param("name", varName, "variable"), ("value", value)));

    /// <summary>
    /// Loads the given step specs through the real read path and returns the
    /// loaded GoalSteps — params born-type exactly like a `.pr` off disk.
    /// </summary>
    private async Task<GoalSteps> LoadSteps(string name, params Make.StepDef[] steps)
        => (await RealGoalLoad.ViaChannel(_app, Make.Goal(name, steps))).Steps;

    private (System.IO.MemoryStream stream, Func<string> getOutput) SetupCapture()
    {
        var stream = new System.IO.MemoryStream();
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, stream,
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
        var steps = await LoadSteps("FalseSkipsChildren",
            MakeConditionStep(0, false),
            MakeOutputStep(4, "should-be-skipped"));
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await result.IsSuccess();
        await Assert.That(getOutput()).DoesNotContain("should-be-skipped");
    }

    [Test]
    public async Task RunAsync_TrueCondition_ExecutesIndentedChildren()
    {
        var (_, getOutput) = SetupCapture();
        var steps = await LoadSteps("TrueExecutesChildren",
            MakeConditionStep(0, true),
            MakeOutputStep(4, "child-executed"));
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await result.IsSuccess();
        await Assert.That(getOutput()).Contains("child-executed");
    }

    [Test]
    public async Task RunAsync_FalseCondition_ResumesAtSameIndent()
    {
        var (_, getOutput) = SetupCapture();
        var steps = await LoadSteps("FalseResumesAtIndent",
            MakeConditionStep(0, false),
            MakeOutputStep(4, "child-skipped"),
            MakeOutputStep(0, "next-runs"));
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await result.IsSuccess();
        var output = getOutput();
        await Assert.That(output).DoesNotContain("child-skipped");
        await Assert.That(output).Contains("next-runs");
    }

    [Test]
    public async Task RunAsync_NestedConditions_InnerFalseSkipsOnlyInner()
    {
        var (_, getOutput) = SetupCapture();
        var steps = await LoadSteps("NestedInnerFalse",
            MakeConditionStep(0, true),        // outer true → children execute
            MakeConditionStep(4, false),        // inner false → inner children skipped
            MakeOutputStep(8, "inner-skipped"), // inner child
            MakeOutputStep(4, "outer-runs"));   // outer child at indent 4
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await result.IsSuccess();
        var output = getOutput();
        await Assert.That(output).DoesNotContain("inner-skipped");
        await Assert.That(output).Contains("outer-runs");
    }

    [Test]
    public async Task RunAsync_NoIndentedChildren_FalseDoesNotSkip()
    {
        var (_, getOutput) = SetupCapture();
        var steps = await LoadSteps("NoChildrenFalseNoSkip",
            MakeConditionStep(0, false),  // false but no indented children
            MakeOutputStep(0, "next-runs")); // same indent — not a child
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await result.IsSuccess();
        await Assert.That(getOutput()).Contains("next-runs");
    }

    [Test]
    public async Task RunAsync_TwoConsecutiveConditions_EachControlsOwnBlock()
    {
        var (_, getOutput) = SetupCapture();
        var steps = await LoadSteps("TwoConsecutiveConditions",
            MakeConditionStep(0, false),
            MakeOutputStep(4, "child-A-skipped"),
            MakeConditionStep(0, true),
            MakeOutputStep(4, "child-B-runs"));
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await result.IsSuccess();
        var output = getOutput();
        await Assert.That(output).DoesNotContain("child-A-skipped");
        await Assert.That(output).Contains("child-B-runs");
    }

    [Test]
    public async Task RunAsync_DeeplyNested_ThreeLevels()
    {
        var (_, getOutput) = SetupCapture();
        var steps = await LoadSteps("DeeplyNestedThreeLevels",
            MakeConditionStep(0, true),
            MakeConditionStep(4, true),
            MakeOutputStep(8, "leaf-runs"));
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await result.IsSuccess();
        await Assert.That(getOutput()).Contains("leaf-runs");
    }

    [Test]
    public async Task RunAsync_NonConditionStep_FalseValue_DoesNotSkip()
    {
        var (_, getOutput) = SetupCapture();
        // variable.set with value false — returns a variable types object, not bool false
        // Even if it somehow returned false, the child should not be skipped
        // because set returns Data.Ok(variable{...}), not Data.Ok(false)
        var steps = await LoadSteps("NonConditionFalseNoSkip",
            MakeSetStep(0, "myVar", false),
            MakeOutputStep(4, "child-runs"));
        var context = _app.User.Context;
        var result = await steps.RunAsync(context);

        await result.IsSuccess();
        // variable.set returns Data.Ok({name, value, type}) — Value is a variable object, not bool false
        // So HasIndentedChildren check passes but Value is not bool false → children execute
        await Assert.That(getOutput()).Contains("child-runs");
    }

    [Test]
    public async Task RunAsync_HasIndentedChildren_CorrectDetection()
    {
        var steps = await LoadSteps("HasIndentedChildren",
            MakeOutputStep(0, "parent"),
            MakeOutputStep(4, "child"),
            MakeOutputStep(0, "sibling"));

        await Assert.That(steps.HasIndentedChildren(0)).IsTrue();  // step[1].Indent > step[0].Indent
        await Assert.That(steps.HasIndentedChildren(1)).IsFalse(); // step[2].Indent < step[1].Indent
        await Assert.That(steps.HasIndentedChildren(2)).IsFalse(); // no step[3]
    }
}
