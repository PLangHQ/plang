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

        // Capture the REAL output channel — the goal runs through the real output.write,
        // which writes the resolved value to this stream (no hand-rolled handler).
        var captureStream = new System.IO.MemoryStream();
        engine.User.Channel.Register(new global::app.channel.type.stream.@this(
            global::app.channel.list.@this.Output, captureStream,
            global::app.channel.ChannelDirection.Output, ownsStream: true) { Mime = "text/plain" });

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Start",
            Make.Step("set %name% = \"Plang\"",
                Make.Action("variable", "set", Make.Param("Name", "name", "variable"), ("Value", "Plang"))),
            Make.Step("write out %name%",
                Make.Action("output", "write", Make.Template("Data", "%name%"))),
            Make.Step("set %newVarName% = %name%",
                Make.Action("variable", "set", Make.Param("Name", "newVarName", "variable"), Make.Param("Value", "%name%", "variable"))),
            Make.Step("write out \"NewVar: %newVarName%\"",
                Make.Action("output", "write", Make.Template("Data", "NewVar: %newVarName%")))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();

        // Check variables
        await Assert.That((await context.Variable.GetValue("name"))).IsEqualTo("Plang");
        await Assert.That((await context.Variable.GetValue("newVarName"))).IsEqualTo("Plang");

        // Check output (real channel)
        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).Contains("Plang");
        await Assert.That(output).Contains("NewVar: Plang");
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
                Make.Action("variable", "set", Make.Param("Name", "result", "variable"), Make.Param("Value", "%myVar%", "variable")))));
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
                Make.Action("output", "write", Make.Template("Data", "Hello %user%!")))));
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

    // An EMBEDDED reference to an unset variable is an error, same as a full-match one —
    // strict semantics: any %unknown% that isn't set fails at the reference site, never
    // renders literal or empty.
    [Test]
    public async Task ResolveValue_EmbeddedMissingVariable_FailsVariableNotFound()
    {
        await using var engine = TestApp.Create("/app");

        var capture = new CapturingWriteHandler();
        engine.Module.Register("output", "write", capture);

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Test",
            Make.Step("write with unknown var",
                Make.Action("output", "write", Make.Template("Data", "Value: %unknown%")))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableNotFound");
    }

    // Referencing an unset variable is an error, not a silent null — `set result =
    // %nonexistent%` fails the step with VariableNotFound (the value the source would
    // answer for is missing; there is nothing to assign).
    [Test]
    public async Task ResolveValue_FullMissingVariable_FailsVariableNotFound()
    {
        await using var engine = TestApp.Create("/app");

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("Test",
            Make.Step("set result = %nonexistent%",
                Make.Action("variable", "set", Make.Param("Name", "result", "variable"), Make.Param("Value", "%nonexistent%", "variable")))));
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableNotFound");
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

        public global::app.goal.step.action.@this Action { get; set; } = null!;
        public global::app.@this App { get; private set; } = null!;
        public global::app.actor.context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public Task<global::app.error.IError?> Attach(global::app.goal.step.action.@this action, global::app.actor.context.@this context)
        { Action = action; App = context.App!; Context = context; return Task.FromResult<global::app.error.IError?>(null); }

        public async Task<Data> Execute()
        {
            var contentData = Action?.Parameters.FirstOrDefault(d => string.Equals(d.Name, "Data", StringComparison.OrdinalIgnoreCase));
            if (contentData != null)
            {
                // Resolve via the value's OWN door — a template (text- or source-born) fills
                // its %refs%; a plain value answers itself. Mirrors real output.write, no regex.
                var item = await contentData.Value();
                Lines.Add(item?.ToString() ?? "");
            }
            return Context.App.Ok();
        }
    }

    #endregion
}
