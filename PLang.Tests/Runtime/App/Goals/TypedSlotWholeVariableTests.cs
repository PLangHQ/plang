namespace PLang.Tests.App.Goals;

// A slot's type is the action's promise: a whole variable in a typed slot is made into the slot's type, as a
// template there is (`%x%` as `%x%/` is). An open slot (item) takes the value as it is.
public class TypedSlotWholeVariableTests
{
    // a step as the build writes it: file.exists on Path, its answer written to %a%
    private static string Exists(string given) => $$"""
{"name":"Start","step":[{"index":0,"text":"x","line":{"number":2,"indent":0},"code":[
 {"module":"file","name":"exists","property":[{"name":"Path","type":{"name":"path","template":"plang"},"value":"{{given}}",
   "variable":[{"text":"%missing%","code":[{"variable":"missing"}]}]}]},
 {"module":"variable","name":"set","property":[
   {"name":"Name","type":{"name":"variable"},"value":"%a%","variable":[{"text":"%a%","code":[{"variable":"a"}]}]},
   {"name":"Value","type":{"name":"item","template":"plang"},"value":"%!data%","variable":[{"text":"%!data%","code":[{"variable":"!data"}]}]}]}
]}],"child":[],"file":"Start.goal"}
""";

    private static async Task<global::app.goal.@this> Loaded(global::app.@this app, string pr)
        => await global::PLang.Tests.Shared.RealGoalLoad.Read(app, pr,
            global::app.type.item.path.@this.Resolve("/.build/start.pr", app.actor.list.User.Context));

    [Test]
    [Arguments("%missing%")]
    [Arguments("%missing%/")]
    public async Task FileExists_OnAVariableNamingAMissingFile_AnswersAPathThatIsEmpty(string given)
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("missing", "/nothere");

        await (await (await Loaded(app, Exists(given))).Start(ctx)).IsSuccess();

        var a = await ctx.Variable.Get("a");
        await Assert.That(await a.Value()).IsAssignableTo<global::app.type.item.path.@this>();
        await Assert.That(await a.ToBooleanAsync()).IsFalse();
    }

    // another typed slot: timer.sleep's Duration given a whole variable holding text, settled as a duration
    [Test]
    public async Task ADurationSlot_GivenAWholeVariableHoldingText_ReadsADuration()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("wait", "10ms");
        var goal = await Loaded(app, """
{"name":"Start","step":[{"index":0,"text":"x","line":{"number":2,"indent":0},"code":[
 {"module":"timer","name":"sleep","property":[{"name":"Duration","type":{"name":"duration","template":"plang"},"value":"%wait%",
   "variable":[{"text":"%wait%","code":[{"variable":"wait"}]}]}]}
]}],"child":[],"file":"Start.goal"}
""");

        // the slot as an action answering it settles it
        var slot = goal.Step[0].Code[0]["Duration"]!.Data(ctx).As<global::app.type.item.duration.@this>();
        var settled = await slot.Settle();

        await Assert.That(await settled.Value()).IsAssignableTo<global::app.type.item.duration.@this>();
    }

    // a typed slot settling a reference to content not yet read makes its type without reading it: the content is
    // read at its first use. Reading that json as the dict waits on the kind design's stage 3 (a json source made
    // into a dict reads through the json type); here only that settle reads nothing.
    [Test]
    public async Task ATypedSlot_SettlingAReferenceToUnreadContent_DoesNotReadIt()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var unread = ctx.App.type.list[new global::app.type.@this("item", "json"), ctx].Make("{\"a\":1}", ctx);
        await ctx.Variable.Set("j", new global::app.data.@this("j", unread, context: ctx));
        var slot = ((global::app.data.@this)global::PLang.Tests.Shared.Make.Param(ctx, "Data", "%j%", "dict").value!)
            .As<global::app.type.item.dict.@this>();

        var settled = await slot.Settle();

        await Assert.That(settled.Peek()).IsAssignableTo<global::app.type.item.source>();
        await Assert.That((await ctx.Variable.Get("j")).Peek()).IsSameReferenceAs(unread);
    }

    // an open slot takes the value as it is: variable.set keeps a text a text
    [Test]
    public async Task AnOpenSlot_KeepsTheVariablesOwnType()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("missing", "/nothere");
        var goal = await Loaded(app, """
{"name":"Start","step":[{"index":0,"text":"x","line":{"number":2,"indent":0},"code":[
 {"module":"variable","name":"set","property":[
   {"name":"Name","type":{"name":"variable"},"value":"%a%","variable":[{"text":"%a%","code":[{"variable":"a"}]}]},
   {"name":"Value","type":{"name":"item","template":"plang"},"value":"%missing%","variable":[{"text":"%missing%","code":[{"variable":"missing"}]}]}]}
]}],"child":[],"file":"Start.goal"}
""");

        await (await goal.Start(ctx)).IsSuccess();

        await Assert.That(await (await ctx.Variable.Get("a")).Value()).IsAssignableTo<global::app.type.item.text.@this>();
    }
}
