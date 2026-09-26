using PLang.Tests.Shared;

namespace PLang.Tests.App.Serialization;

/// <summary>
/// The template flag is EXPLICIT and rides the .pr type entity: a value resolves its %refs%
/// only when its type carries template="plang". A value with a %ref% but no flag is plain data —
/// never resolved. Verified through the real write+read goal path.
/// </summary>
public class TemplateFlagTests
{
    [Test]
    public async Task TemplateParam_ResolvesOnLoad_NoFlagStaysLiteral()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        await app.User.Context.Variable.Set("name", "World");

        var flaggedType = new global::app.type.@this("text", template: "plang");
        var plainType = new global::app.type.@this("text");

        var goal = Make.Goal("G", "/g.goal",
            Make.Step("s",
                Make.Action("output", "write",
                    Make.Param("flagged", "Hello %name%", flaggedType),
                    Make.Param("plain", "Hello %name%", plainType))));

        var loaded = await RealGoalLoad.ViaChannel(app, goal);
        var properties = loaded.Step[0].Code[0].Property;
        var flagged = await properties["flagged"]!.Data(app.User.Context).Value();
        var plain = await properties["plain"]!.Data(app.User.Context).Value();

        await Assert.That(flagged.ToString()).IsEqualTo("Hello World");
        await Assert.That(plain.ToString()).IsEqualTo("Hello %name%");
    }

    // Data from outside is never a template: an authored `%answer%` brings the value it names as it
    // is — an LLM's text holding `%name%` is text, never rendered against the scope that reads it.
    [Test]
    public async Task AReference_BringsItsValueAsIs_NeverRendersIt()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var context = app.User.Context;
        await context.Variable.Set("answer", context.Ok("[1] output.write(Data=\"hello %name%\")"));

        var goal = Make.Goal("G", "/g.goal",
            Make.Step("s",
                Make.Action("output", "write",
                    Make.Param("Data", "%answer%", new global::app.type.@this("item", template: "plang")))));
        var loaded = await RealGoalLoad.ViaChannel(app, goal);
        var relay = loaded.Step[0].Code[0].Property["Data"]!.Data(context);

        await Assert.That((await relay.Value())?.ToString()).IsEqualTo("[1] output.write(Data=\"hello %name%\")");
    }

    // The builder's `llm.query …, write to %answer%`: the set stores what %!data% brings — read later,
    // the answer is still its text.
    [Test]
    public async Task AValueSetThroughAReference_ReadsBackAsIs()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var context = app.User.Context;
        await context.Variable.Set("reply", context.Ok("[1] output.write(Data=\"hello %name%\")"));

        var goal = Make.Goal("G", "/g.goal",
            Make.Step("set %answer% = %reply%",
                Make.Action("variable", "set", Make.Param("Name", "answer", "variable"),
                    Make.Param("Value", "%reply%", new global::app.type.@this("item", template: "plang")))));
        var loaded = await RealGoalLoad.ViaChannel(app, goal);
        await (await loaded.Run(context)).IsSuccess();

        var answer = await context.Variable.Get("answer");
        await Assert.That((await answer.Value())?.ToString()).IsEqualTo("[1] output.write(Data=\"hello %name%\")");
        await Assert.That((await answer.Value())?.ToString()).IsEqualTo("[1] output.write(Data=\"hello %name%\")");
    }

    // The same, with the value still unopened — as a store read-back (the LLM cache) or a lazy file
    // hands it: the set shares the raw, and the raw stays outside data, never the row's template.
    [Test]
    public async Task AnUnopenedValueSetThroughAReference_ReadsBackAsIs()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var context = app.User.Context;
        var unopened = new global::app.type.item.source("[1] output.write(Data=\"hello %name%\")", context.App.Type["text"]);
        await context.Variable.Set("reply", new global::app.data.@this("reply", unopened, context: context));

        var goal = Make.Goal("G", "/g.goal",
            Make.Step("set %answer% = %reply%",
                Make.Action("variable", "set", Make.Param("Name", "answer", "variable"),
                    Make.Param("Value", "%reply%", new global::app.type.@this("item", template: "plang")))));
        var loaded = await RealGoalLoad.ViaChannel(app, goal);
        await (await loaded.Run(context)).IsSuccess();

        var answer = await context.Variable.Get("answer");
        await Assert.That((await answer.Value())?.ToString()).IsEqualTo("[1] output.write(Data=\"hello %name%\")");
    }
}
