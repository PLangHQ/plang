using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Goals;

/// <summary>
/// A template a list action is given renders where it is written — at the add — as a set renders its value:
/// `add "%lesson.examples%/%rel%" to list %exampleFiles%` holds the path, never the template; so does a dict's key
/// set to one (`set %files.k% = "%lesson.examples%/%rel%"`).
/// </summary>
public class ListTemplateTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/tmp/listtpl-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private global::app.goal.step.action.@this List(string action, string value)
        => Make.Action(Ctx, "list", action, Make.Param(Ctx, "ListName", "exampleFiles", "variable"),
            Make.Param(Ctx, "Value", value, new global::app.type.@this("item", template: new global::app.type.item.template.kind.plang.@this())));

    private async Task<global::app.data.@this> Run(params Make.StepDef[] steps)
    {
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(_app, Make.Goal(Ctx, "G", "/g.goal", steps));
        return await goal.Start(Ctx);
    }

    [Test]
    public async Task AnAddedTemplate_RendersAtTheAdd()
    {
        await Ctx.Variable.Set("lesson", new Dictionary<string, object?> { ["examples"] = "/lessons/one" });
        await Ctx.Variable.Set("rel", "a.md");

        await (await Run(Make.Step("add the path", List("add", "%lesson.examples%/%rel%")))).IsSuccess();
        // what it was written with changes after: the list keeps what was added
        await Ctx.Variable.Set("rel", "b.md");

        var files = (global::app.type.item.list.@this)(await (await Ctx.Variable.Get("exampleFiles")).Value())!;
        await Assert.That((await files.Items(Ctx).Single().Value())?.ToString()).IsEqualTo("/lessons/one/a.md");
    }

    [Test]
    public async Task AMemberSetToATemplate_RendersAtTheSet()
    {
        await Ctx.Variable.Set("files", new Dictionary<string, object?>());
        await Ctx.Variable.Set("lesson", new Dictionary<string, object?> { ["examples"] = "/lessons/one" });
        await Ctx.Variable.Set("rel", "a.md");

        var set = Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", "%files.k%", "variable"),
            Make.Param(Ctx, "Value", "%lesson.examples%/%rel%", new global::app.type.@this("item", template: new global::app.type.item.template.kind.plang.@this())));
        await (await Run(Make.Step("set the key", set))).IsSuccess();
        // what it was written with changes after: the dict keeps what was set
        await Ctx.Variable.Set("rel", "b.md");

        var files = (global::app.type.item.dict.@this)(await (await Ctx.Variable.Get("files")).Value())!;
        await Assert.That((await (await files.Get(await Ctx.Variable.Get("files"), "k")).Value())?.ToString()).IsEqualTo("/lessons/one/a.md");
    }

    [Test]
    public async Task AContainsGivenATemplate_ComparesWhatItRenders()
    {
        await Ctx.Variable.Set("exampleFiles", new List<object?> { "/lessons/one/a.md" });
        await Ctx.Variable.Set("lesson", new Dictionary<string, object?> { ["examples"] = "/lessons/one" });
        await Ctx.Variable.Set("rel", "a.md");

        var found = await Run(Make.Step("contains", List("contains", "%lesson.examples%/%rel%")));

        await Assert.That(await found.ToBooleanAsync()).IsTrue();
    }
}
