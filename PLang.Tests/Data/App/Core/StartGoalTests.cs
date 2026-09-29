using app.actor.context;
using app.type.item.variable;
using app.module;

namespace PLang.Tests.App.Core;

public class StartGoalTests
{
    #region Programmatic Construction

    [Test]
    public async Task StartGoal_Programmatic_SetsVariablesAndWritesOutput()
    {
        await using var engine = new global::app.@this("/app").Testing();

        // Capture the REAL output channel — the goal runs through the real output.write,
        // which writes the resolved value to this stream (no hand-rolled handler).
        var captureStream = new System.IO.MemoryStream();
        engine.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this(
            global::app.channel.list.@this.Output, captureStream,
            global::app.channel.ChannelDirection.Output, ownsStream: true) { Mime = "text/plain" });

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal(engine.actor.list.User.Context, "Start",
            Make.Step("set %name% = \"Plang\"",
                Make.Action(engine.actor.list.User.Context, "variable", "set", Make.Param(engine.actor.list.User.Context, "Name", "name", "variable"), ("Value", "Plang"))),
            Make.Step("write out %name%",
                Make.Action(engine.actor.list.User.Context, "output", "write", Make.Template(engine.actor.list.User.Context, "Data", "%name%"))),
            Make.Step("set %newVarName% = %name%",
                Make.Action(engine.actor.list.User.Context, "variable", "set", Make.Param(engine.actor.list.User.Context, "Name", "newVarName", "variable"), Make.Param(engine.actor.list.User.Context, "Value", "%name%", "variable"))),
            Make.Step("write out \"NewVar: %newVarName%\"",
                Make.Action(engine.actor.list.User.Context, "output", "write", Make.Template(engine.actor.list.User.Context, "Data", "NewVar: %newVarName%")))));
        engine.goal.list.Add(goal);

        var context = engine.actor.list.User.Context;
        var result = await engine.Start(goal, context);

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
        await using var engine = new global::app.@this("/app").Testing();

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal(engine.actor.list.User.Context, "Test",
            Make.Step("set myVar",
                Make.Action(engine.actor.list.User.Context, "variable", "set", Make.Param(engine.actor.list.User.Context, "Name", "myVar", "variable"), ("Value", "Hello"))),
            Make.Step("set result = %myVar%",
                Make.Action(engine.actor.list.User.Context, "variable", "set", Make.Param(engine.actor.list.User.Context, "Name", "result", "variable"), Make.Param(engine.actor.list.User.Context, "Value", "%myVar%", "variable")))));
        engine.goal.list.Add(goal);

        var context = engine.actor.list.User.Context;
        var result = await engine.Start(goal, context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("result"))).IsEqualTo("Hello");
    }

    [Test]
    public async Task ResolveValue_StringInterpolation_ReturnsInterpolatedString()
    {
        await using var engine = new global::app.@this("/app").Testing();

        var capture = new CapturedOutput(engine);

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal(engine.actor.list.User.Context, "Test",
            Make.Step("set user",
                Make.Action(engine.actor.list.User.Context, "variable", "set", Make.Param(engine.actor.list.User.Context, "Name", "user", "variable"), ("Value", "World"))),
            Make.Step("write Hello %user%!",
                Make.Action(engine.actor.list.User.Context, "output", "write", Make.Template(engine.actor.list.User.Context, "Data", "Hello %user%!")))));
        engine.goal.list.Add(goal);

        var context = engine.actor.list.User.Context;
        var result = await engine.Start(goal, context);

        await result.IsSuccess();
        await Assert.That(capture.Text).Contains("Hello World!");
    }

    [Test]
    public async Task ResolveValue_LiteralString_RemainsUnchanged()
    {
        await using var engine = new global::app.@this("/app").Testing();

        var capture = new CapturedOutput(engine);

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal(engine.actor.list.User.Context, "Test",
            Make.Step("write literal",
                Make.Action(engine.actor.list.User.Context, "output", "write", ("Data", "no variables here")))));
        engine.goal.list.Add(goal);

        var context = engine.actor.list.User.Context;
        var result = await engine.Start(goal, context);

        await result.IsSuccess();
        await Assert.That(capture.Text).Contains("no variables here");
    }

    // An EMBEDDED reference to an unset variable is an error, same as a full-match one —
    // strict semantics: any %unknown% that isn't set fails at the reference site, never
    // renders literal or empty.
    [Test]
    public async Task ResolveValue_EmbeddedMissingVariable_FailsVariableNotFound()
    {
        await using var engine = new global::app.@this("/app").Testing();

        _ = new CapturedOutput(engine);

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal(engine.actor.list.User.Context, "Test",
            Make.Step("write with unknown var",
                Make.Action(engine.actor.list.User.Context, "output", "write", Make.Template(engine.actor.list.User.Context, "Data", "Value: %unknown%")))));
        engine.goal.list.Add(goal);

        var context = engine.actor.list.User.Context;
        var result = await engine.Start(goal, context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableNotFound");
    }

    // Referencing an unset variable is an error, not a silent null — `set result =
    // %nonexistent%` fails the step with VariableNotFound (the value the source would
    // answer for is missing; there is nothing to assign).
    [Test]
    public async Task ResolveValue_FullMissingVariable_FailsVariableNotFound()
    {
        await using var engine = new global::app.@this("/app").Testing();

        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal(engine.actor.list.User.Context, "Test",
            Make.Step("set result = %nonexistent%",
                Make.Action(engine.actor.list.User.Context, "variable", "set", Make.Param(engine.actor.list.User.Context, "Name", "result", "variable"), Make.Param(engine.actor.list.User.Context, "Value", "%nonexistent%", "variable")))));
        engine.goal.list.Add(goal);

        var context = engine.actor.list.User.Context;
        var result = await engine.Start(goal, context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableNotFound");
    }

    #endregion

    #region Build-Time Defaults

    [Test]
    public async Task Defaults_ResolvedWhenParameterMissing()
    {
        await using var engine = new global::app.@this("/app").Testing();

        // "Type" is NOT in parameters — developer didn't set it
        // "Type" IS in defaults — builder captured it at build time
        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal(engine.actor.list.User.Context, "Test",
            Make.Step("set greeting = hello",
                Make.WithDefaults(engine.actor.list.User.Context, 
                    Make.Action(engine.actor.list.User.Context, "variable", "set", Make.Param(engine.actor.list.User.Context, "Name", "greeting", "variable"), ("Value", "hello")),
                    ("Type", new global::app.type.@this("text"))))));
        engine.goal.list.Add(goal);

        var context = engine.actor.list.User.Context;
        var result = await engine.Start(goal, context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("greeting"))).IsEqualTo("hello");

        // Type should be "string" — resolved from defaults, not null
        var data = await context.Variable.Get("greeting");
        await Assert.That(data?.Type?.Name).IsEqualTo("text");
    }

    [Test]
    public async Task Defaults_ParameterOverridesDefault()
    {
        await using var engine = new global::app.@this("/app").Testing();

        // "Type" is in BOTH parameters and defaults — parameter wins
        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal(engine.actor.list.User.Context, "Test",
            Make.Step("set count = 42",
                Make.WithDefaults(engine.actor.list.User.Context, 
                    Make.Action(engine.actor.list.User.Context, "variable", "set",
                        Make.Param(engine.actor.list.User.Context, "Name", "count", "variable"), ("Value", 42),
                        ("Type", new global::app.type.@this("number", "long"))),
                    ("Type", new global::app.type.@this("text"))))));
        engine.goal.list.Add(goal);

        var context = engine.actor.list.User.Context;
        var result = await engine.Start(goal, context);

        await result.IsSuccess();
        // "long" from parameters, not "string" from defaults
        var data = await context.Variable.Get("count");
        await Assert.That(data?.Type?.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Defaults_NullDefaultsStillWorksWithAttributeFallback()
    {
        await using var engine = new global::app.@this("/app").Testing();

        // No defaults at all — falls through to [Default] attribute on the action
        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal(engine.actor.list.User.Context, "Test",
            Make.Step("set x = y",
                Make.Action(engine.actor.list.User.Context, "variable", "set", Make.Param(engine.actor.list.User.Context, "Name", "x", "variable"), ("Value", "y")))));
        engine.goal.list.Add(goal);

        var context = engine.actor.list.User.Context;
        var result = await engine.Start(goal, context);

        await result.IsSuccess();
        // Type is derived from value ("y" is a string), not from defaults or [Default] attribute
        // This proves the fallback chain works: no defaults → no attribute → auto-derive
        var data = await context.Variable.Get("x");
        await Assert.That((await data.Value())?.ToString()).IsEqualTo("y");
    }

    #endregion
}
