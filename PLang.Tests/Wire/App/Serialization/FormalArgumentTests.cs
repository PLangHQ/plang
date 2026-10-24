namespace PLang.Tests.App.Serialization;

/// <summary>
/// A goal call's arguments are a list of Data, so formal writes them as the list they are — each Data its record,
/// <c>{name: type = value}</c> — and reads them back as that list of named values.
/// </summary>
public class FormalArgumentTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
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

    // formal writes a step's look, never plang's store: characters nobody should see stay masked
    [Test] public async Task ASensitiveValue_InFormal_IsMasked()
    {
        var writer = new global::app.goal.step.action.formal.Writer(Ctx);

        await new global::app.type.item.sensitive.@this("s3cret", app.type.list["sensitive"]).Output(writer);

        await Assert.That(writer.ToString()).Contains("****");
        await Assert.That(writer.ToString()).DoesNotContain("s3cret");
    }

    // an object's entries keep their type: a typed entry is a Data of that type under its key
    [Test]
    [Arguments("[{name: text = \"search\"}]", "name", "text")]
    [Arguments("[{port: text = 8080}]", "port", "text")]
    public async Task ATypedEntryInAnObject_KeepsItsType(string tools, string key, string type)
    {
        var step = new global::app.goal.step.@this(app.type.list) { Goal = FormalWriterTests.Goal(Ctx) };

        var read = await new global::app.goal.step.action.formal.Reader(step, app.module.list)
            .Read($"llm.query(Message=\"hi\", Tool={tools})", Ctx);

        await read.IsSuccess();
        var query = ((global::app.goal.step.action.list.@this)read.Peek()!)[0];
        var tool = await new global::app.data.@this("Tool", query["Tool"]!.Value!, context: Ctx).Value();
        var element = (global::app.type.item.dict.@this)(await ((global::app.type.item.list.@this)tool).Items(Ctx).Single().Value())!;
        var entry = element.Entries(Ctx).Single(e => e.Name == key);
        await Assert.That(entry.Type!.Name).IsEqualTo(type);
    }

    // a slot that holds objects keeps them: only a slot that declares its elements are arguments reads records as them
    [Test]
    [Arguments("[{name: \"search\"}, {name: \"fetch\"}]", 2)]
    [Arguments("[{name: text = \"search\"}]", 1)]
    public async Task AListOfOneKeyObjects_InASlotOfObjects_StaysObjects(string tools, int count)
    {
        var step = new global::app.goal.step.@this(app.type.list) { Goal = FormalWriterTests.Goal(Ctx) };

        var read = await new global::app.goal.step.action.formal.Reader(step, app.module.list)
            .Read($"llm.query(Message=\"hi\", Tool={tools})", Ctx);

        await read.IsSuccess();
        var query = ((global::app.goal.step.action.list.@this)read.Peek()!)[0];
        var tool = await new global::app.data.@this("Tool", query["Tool"]!.Value!, context: Ctx).Value();
        var elements = ((global::app.type.item.list.@this)tool).Items(Ctx).ToList();
        await Assert.That(elements.Count).IsEqualTo(count);
        foreach (var element in elements)
            await Assert.That(await element.Value()).IsTypeOf<global::app.type.item.dict.@this>();
    }
}
