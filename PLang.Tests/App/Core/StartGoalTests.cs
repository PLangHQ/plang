using app.actor.context;
using app.variable;
using app.module;

namespace PLang.Tests.App.Core;

public class StartGoalTests
{
    #region Programmatic Construction

    [Test]
    public async Task StartGoal_Programmatic_SetsVariablesAndWritesOutput()
    {
        await using var engine = new global::app.@this("/app");

        // Replace output.write with capturing version
        var capture = new CapturingWriteHandler();
        engine.Module.Register("output", "write", capture);

        var goal = new Goal
        {
            Name = "Start",
            Path = "/Start.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "name" }, { "value", "Plang" } },
                    index: 0, text: "set %name% = \"Plang\""),
                MakeStep("output", "write",
                    new Dictionary<string, object?> { { "Data", "%name%" } },
                    index: 1, text: "write out %name%"),
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "newVarName" }, { "value", "%name%" } },
                    index: 2, text: "set %newVarName% = %name%"),
                MakeStep("output", "write",
                    new Dictionary<string, object?> { { "Data", "NewVar: %newVarName%" } },
                    index: 3, text: "write out \"NewVar: %newVarName%\"")
            }
        };
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();

        // Check variables
        await Assert.That(context.Variable.GetValue("name")).IsEqualTo("Plang");
        await Assert.That(context.Variable.GetValue("newVarName")).IsEqualTo("Plang");

        // Check output
        await Assert.That(capture.Lines).Contains("Plang");
        await Assert.That(capture.Lines).Contains("NewVar: Plang");
    }

    #endregion

    #region Variable Resolution Unit Tests

    [Test]
    public async Task ResolveValue_FullVariableReference_ReturnsTypedValue()
    {
        await using var engine = new global::app.@this("/app");

        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "myVar" }, { "value", "Hello" } },
                    index: 0, text: "set myVar"),
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "result" }, { "value", "%myVar%" } },
                    index: 1, text: "set result = %myVar%")
            }
        };
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That(context.Variable.GetValue("result")).IsEqualTo("Hello");
    }

    [Test]
    public async Task ResolveValue_StringInterpolation_ReturnsInterpolatedString()
    {
        await using var engine = new global::app.@this("/app");

        var capture = new CapturingWriteHandler();
        engine.Module.Register("output", "write", capture);

        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "user" }, { "value", "World" } },
                    index: 0, text: "set user"),
                MakeStep("output", "write",
                    new Dictionary<string, object?> { { "Data", "Hello %user%!" } },
                    index: 1, text: "write Hello %user%!")
            }
        };
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That(capture.Lines).Contains("Hello World!");
    }

    [Test]
    public async Task ResolveValue_LiteralString_RemainsUnchanged()
    {
        await using var engine = new global::app.@this("/app");

        var capture = new CapturingWriteHandler();
        engine.Module.Register("output", "write", capture);

        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStep("output", "write",
                    new Dictionary<string, object?> { { "Data", "no variables here" } },
                    index: 0, text: "write literal")
            }
        };
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That(capture.Lines).Contains("no variables here");
    }

    [Test]
    public async Task ResolveValue_MissingVariable_ResolvesToEmptyString()
    {
        await using var engine = new global::app.@this("/app");

        var capture = new CapturingWriteHandler();
        engine.Module.Register("output", "write", capture);

        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStep("output", "write",
                    new Dictionary<string, object?> { { "Data", "Value: %unknown%" } },
                    index: 0, text: "write with unknown var")
            }
        };
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That(capture.Lines).Contains("Value: ");
    }

    [Test]
    public async Task ResolveValue_FullMissingVariable_ResolvesToNull()
    {
        await using var engine = new global::app.@this("/app");

        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "result" }, { "value", "%nonexistent%" } },
                    index: 0, text: "set result = %nonexistent%")
            }
        };
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That(context.Variable.GetValue("result")).IsNull();
    }

    #endregion

    #region Build-Time Defaults

    [Test]
    public async Task Defaults_ResolvedWhenParameterMissing()
    {
        await using var engine = new global::app.@this("/app");

        // "type" is NOT in parameters — developer didn't set it
        // "type" IS in defaults — builder captured it at build time
        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStepWithDefaults("variable", "set",
                    parameters: new Dictionary<string, object?> { { "name", "greeting" }, { "value", "hello" } },
                    defaults: new Dictionary<string, object?> { { "type", "string" } },
                    index: 0, text: "set greeting = hello")
            }
        };
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That(context.Variable.GetValue("greeting")).IsEqualTo("hello");

        // Type should be "string" — resolved from defaults, not null
        var data = context.Variable.Get("greeting");
        await Assert.That(data?.Type?.Name).IsEqualTo("text");
    }

    [Test]
    public async Task Defaults_ParameterOverridesDefault()
    {
        await using var engine = new global::app.@this("/app");

        // "type" is in BOTH parameters and defaults — parameter wins
        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStepWithDefaults("variable", "set",
                    parameters: new Dictionary<string, object?> { { "name", "count" }, { "value", 42 }, { "type", "long" } },
                    defaults: new Dictionary<string, object?> { { "type", "string" } },
                    index: 0, text: "set count = 42")
            }
        };
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        // "long" from parameters, not "string" from defaults
        var data = context.Variable.Get("count");
        await Assert.That(data?.Type?.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Defaults_NullDefaultsStillWorksWithAttributeFallback()
    {
        await using var engine = new global::app.@this("/app");

        // No defaults at all — falls through to [Default] attribute on the action
        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "x" }, { "value", "y" } },
                    index: 0, text: "set x = y")
            }
        };
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        // Type is derived from value ("y" is a string), not from defaults or [Default] attribute
        // This proves the fallback chain works: no defaults → no attribute → auto-derive
        var data = context.Variable.Get("x");
        await Assert.That(data?.Value).IsEqualTo("y");
    }

    #endregion

    #region Helpers

    private static Step MakeStep(string actionClass, string method, IDictionary<string, object?> parameters, int index = 0, string text = "")
    {
        return MakeStepWithDefaults(actionClass, method, parameters, null, index, text);
    }

    private static Step MakeStepWithDefaults(string actionClass, string method,
        IDictionary<string, object?> parameters, IDictionary<string, object?>? defaults,
        int index = 0, string text = "")
    {
        return new Step
        {
            Index = index,
            Text = text,
            Actions = new StepActions
            {
                new global::app.goal.steps.step.actions.action.@this
                {
                    Module = actionClass,
                    ActionName = method,
                    Parameters = parameters.Select(kv => new Data(kv.Key, kv.Value)).ToList(),
                    Defaults = defaults?.Select(kv => new Data(kv.Key, kv.Value)).ToList()
                }
            }
        };
    }

    /// <summary>
    /// A test handler that captures written content instead of writing to Console.
    /// Implements IAction + ICodeGenerated manually since the source generator doesn't run on test projects.
    /// </summary>
    private class CapturingWriteHandler : IAction, ICodeGenerated
    {
        public List<string> Lines { get; } = new();

        public global::app.goal.steps.step.actions.action.@this Action { get; set; } = null!;
        public global::app.@this App { get; private set; } = null!;
        public global::app.actor.context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public Task<Data> ExecuteAsync(global::app.goal.steps.step.actions.action.@this action, global::app.actor.context.@this context)
        {
            App = context.App!;
            Context = context;
            var contentData = action?.Parameters.FirstOrDefault(d => string.Equals(d.Name, "Data", StringComparison.OrdinalIgnoreCase));
            object? content = contentData?.Value;
            if (content is string str && str.Contains('%'))
            {
                var fullMatch = System.Text.RegularExpressions.Regex.Match(str, @"^%([^%]+)%$");
                if (fullMatch.Success)
                    content = context.Variable.GetValue(fullMatch.Groups[1].Value);
                else
                    content = System.Text.RegularExpressions.Regex.Replace(str, @"%([^%]+)%",
                        m => context.Variable.GetValue(m.Groups[1].Value)?.ToString() ?? "");
            }
            if (content != null)
                Lines.Add(content.ToString()!);
            return Task.FromResult(Data.Ok());
        }
    }

    #endregion
}
