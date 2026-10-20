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
}
