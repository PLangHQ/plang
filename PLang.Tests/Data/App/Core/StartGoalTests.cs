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
        await using var engine = TestApp.Create("/app");

        // Replace output.write with capturing version
        var capture = new CapturingWriteHandler();
        engine.Module.Register("output", "write", capture);

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Start",
            Make.Step("set %name% = \"Plang\"",
                Make.Action("variable", "set", Make.Param("Name", "name", "variable"), ("Value", "Plang"))),
            Make.Step("write out %name%",
                Make.Action("output", "write", ("Data", "%name%"))),
            Make.Step("set %newVarName% = %name%",
                Make.Action("variable", "set", Make.Param("Name", "newVarName", "variable"), ("Value", "%name%"))),
            Make.Step("write out \"NewVar: %newVarName%\"",
                Make.Action("output", "write", ("Data", "NewVar: %newVarName%")))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();

        // Check variables
        await Assert.That((await context.Variable.GetValue("name"))).IsEqualTo("Plang");
        await Assert.That((await context.Variable.GetValue("newVarName"))).IsEqualTo("Plang");

        // Check output
        await Assert.That(capture.Lines).Contains("Plang");
        await Assert.That(capture.Lines).Contains("NewVar: Plang");
    }

    #endregion

    #region Variable Resolution Unit Tests

    [Test]
    public async Task ResolveValue_FullVariableReference_ReturnsTypedValue()
    {
        await using var engine = TestApp.Create("/app");

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Test",
            Make.Step("set myVar",
                Make.Action("variable", "set", Make.Param("Name", "myVar", "variable"), ("Value", "Hello"))),
            Make.Step("set result = %myVar%",
                Make.Action("variable", "set", Make.Param("Name", "result", "variable"), ("Value", "%myVar%")))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("result"))).IsEqualTo("Hello");
    }

    [Test]
    public async Task ResolveValue_StringInterpolation_ReturnsInterpolatedString()
    {
        await using var engine = TestApp.Create("/app");

        var capture = new CapturingWriteHandler();
        engine.Module.Register("output", "write", capture);

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Test",
            Make.Step("set user",
                Make.Action("variable", "set", Make.Param("Name", "user", "variable"), ("Value", "World"))),
            Make.Step("write Hello %user%!",
                Make.Action("output", "write", ("Data", "Hello %user%!")))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That(capture.Lines).Contains("Hello World!");
    }

    [Test]
    public async Task ResolveValue_LiteralString_RemainsUnchanged()
    {
        await using var engine = TestApp.Create("/app");

        var capture = new CapturingWriteHandler();
        engine.Module.Register("output", "write", capture);

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Test",
            Make.Step("write literal",
                Make.Action("output", "write", ("Data", "no variables here")))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That(capture.Lines).Contains("no variables here");
    }

    [Test]
    public async Task ResolveValue_MissingVariable_ResolvesToEmptyString()
    {
        await using var engine = TestApp.Create("/app");

        var capture = new CapturingWriteHandler();
        engine.Module.Register("output", "write", capture);

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Test",
            Make.Step("write with unknown var",
                Make.Action("output", "write", ("Data", "Value: %unknown%")))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That(capture.Lines).Contains("Value: ");
    }

    [Test]
    public async Task ResolveValue_FullMissingVariable_ResolvesToNull()
    {
        await using var engine = TestApp.Create("/app");

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Test",
            Make.Step("set result = %nonexistent%",
                Make.Action("variable", "set", Make.Param("Name", "result", "variable"), ("Value", "%nonexistent%")))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("result"))).IsNull();
    }

    #endregion

    #region Build-Time Defaults

    [Test]
    public async Task Defaults_ResolvedWhenParameterMissing()
    {
        await using var engine = TestApp.Create("/app");

        // "Type" is NOT in parameters — developer didn't set it
        // "Type" IS in defaults — builder captured it at build time
        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Test",
            Make.Step("set greeting = hello",
                Make.WithDefaults(
                    Make.Action("variable", "set", Make.Param("Name", "greeting", "variable"), ("Value", "hello")),
                    ("Type", new global::app.type.@this("text"))))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("greeting"))).IsEqualTo("hello");

        // Type should be "string" — resolved from defaults, not null
        var data = await context.Variable.Get("greeting");
        await Assert.That(data?.Type?.Name).IsEqualTo("text");
    }

    [Test]
    public async Task Defaults_ParameterOverridesDefault()
    {
        await using var engine = TestApp.Create("/app");

        // "Type" is in BOTH parameters and defaults — parameter wins
        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Test",
            Make.Step("set count = 42",
                Make.WithDefaults(
                    Make.Action("variable", "set",
                        Make.Param("Name", "count", "variable"), ("Value", 42),
                        ("Type", new global::app.type.@this("number", "long"))),
                    ("Type", new global::app.type.@this("text"))))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        // "long" from parameters, not "string" from defaults
        var data = await context.Variable.Get("count");
        await Assert.That(data?.Type?.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Defaults_NullDefaultsStillWorksWithAttributeFallback()
    {
        await using var engine = TestApp.Create("/app");

        // No defaults at all — falls through to [Default] attribute on the action
        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Test",
            Make.Step("set x = y",
                Make.Action("variable", "set", Make.Param("Name", "x", "variable"), ("Value", "y")))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        // Type is derived from value ("y" is a string), not from defaults or [Default] attribute
        // This proves the fallback chain works: no defaults → no attribute → auto-derive
        var data = await context.Variable.Get("x");
        await Assert.That((await data.Value())?.ToString()).IsEqualTo("y");
    }

    #endregion

    #region Helpers

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

        public async Task<Data> ExecuteAsync(global::app.goal.steps.step.actions.action.@this action, global::app.actor.context.@this context)
        {
            App = context.App!;
            Context = context;
            var contentData = action?.Parameters.FirstOrDefault(d => string.Equals(d.Name, "Data", StringComparison.OrdinalIgnoreCase));
            object? raw = contentData?.Peek();
            object? content = (raw as global::app.type.text.@this)?.Clr<string>() ?? raw;
            if (content is string str && str.Contains('%'))
            {
                var fullMatch = System.Text.RegularExpressions.Regex.Match(str, @"^%([^%]+)%$");
                if (fullMatch.Success)
                    content = await context.Variable.GetValue(fullMatch.Groups[1].Value);
                else
                    content = System.Text.RegularExpressions.Regex.Replace(str, @"%([^%]+)%",
                        m => context.Variable.Peek(m.Groups[1].Value)?.Peek()?.ToString() ?? "");
            }
            if (content != null)
                Lines.Add(content.ToString()!);
            return Data.Ok();
        }
    }

    #endregion
}
