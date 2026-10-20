namespace PLang.Tests.App.Serialization;

/// <summary>
/// A goal call's arguments are a list of Data, so formal writes them as the list they are — each Data its record,
/// <c>{name: type = value}</c> — and reads them back as that list of named values.
/// </summary>
public class FormalArgumentTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    [Test] public async Task ArgumentsInFormal_AreAListOfRecords()
    {
        var call = Make.Call(Ctx, "Send", ("to", "%user.email%"), ("subject", "Hi"));
        var writer = new global::app.goal.step.action.formal.Writer(Ctx);

        await call.Output(writer);

        await Assert.That(writer.ToString()).Contains("Parameter: list = [{to: text = \"%user.email%\"}, {subject: text = \"Hi\"}]");
    }

    [Test] public async Task AListOfRecords_ReadsBackAsNamedArguments()
    {
        var goal = FormalWriterTests.Goal(Ctx);
        var step = new global::app.goal.step.@this(app.type.list) { Goal = goal };

        var read = await new global::app.goal.step.action.formal.Reader(step, app.module.list)
            .Read("goal.call(Name: goal = \"Send\", Parameter: list = [{to: item = %user.email%}, {subject: text = \"Hi\"}])", Ctx);

        await read.IsSuccess();
        var call = ((global::app.goal.step.action.list.@this)read.Peek()!)[0];
        var parameter = await new global::app.data.@this("Parameter", call["Parameter"]!.Value!, context: Ctx).Value();
        var rows = ((global::app.type.item.list.@this)parameter).Items(Ctx).ToList();
        await Assert.That(rows.Select(r => r.Name).ToArray()).IsEquivalentTo(new[] { "to", "subject" });
    }

    // a slot that holds objects keeps them: only a slot that declares its elements are arguments reads records as them
    [Test, Skip("red until the slot declares its elements (185 major 2): today a list of one-key objects reads as arguments")]
    public async Task AListOfOneKeyObjects_InASlotOfObjects_StaysObjects()
    {
        var step = new global::app.goal.step.@this(app.type.list) { Goal = FormalWriterTests.Goal(Ctx) };

        var read = await new global::app.goal.step.action.formal.Reader(step, app.module.list)
            .Read("llm.query(Message=\"hi\", Tool=[{name: \"search\"}, {name: \"fetch\"}])", Ctx);

        await read.IsSuccess();
        var query = ((global::app.goal.step.action.list.@this)read.Peek()!)[0];
        var tool = await new global::app.data.@this("Tool", query["Tool"]!.Value!, context: Ctx).Value();
        var elements = ((global::app.type.item.list.@this)tool).Items(Ctx).ToList();
        await Assert.That(elements.Count).IsEqualTo(2);
        foreach (var element in elements)
            await Assert.That(await element.Value()).IsTypeOf<global::app.type.item.dict.@this>();
    }
}
