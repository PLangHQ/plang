namespace PLang.Tests.App.DataTests;

/// <summary>
/// A value that holds variables is written as the view says: plang's store keeps it as written (<c>%name%</c>); a dump
/// shows it as written beside the value it resolves to, with its type, or that it is not set.
/// </summary>
public class ViewWriteTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/view-write-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing().Compact();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    // %name% as a step gives it: a text that is a template
    private global::app.data.@this Template(string text)
        => new("x", new global::app.type.item.text.@this(text,
            _app.type.list[new global::app.type.@this("text", template: new global::app.type.item.template.kind.plang.@this()), Ctx]), context: Ctx);

    private async Task<string> Written(global::app.data.@this data, global::app.View view, string format)
    {
        using var stream = new System.IO.MemoryStream();
        var kind = format == "plang" ? _app.type.list["wire"].kind["plang"]! : _app.type.list["item"].kind["json"]!;
        await (await kind.Encode(stream, data, Ctx, view)).IsSuccess();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    [Test] public async Task ADump_ShowsAValueAsWritten_AndTheValueItResolvesTo_WithItsType()
    {
        await Ctx.Variable.Set("user", global::app.type.item.@this.Create(new Dictionary<string, object?> { ["name"] = "Ada" }, Ctx));

        var dump = await Written(Template("%user%"), global::app.View.Debug, "json");

        await Assert.That(dump).Contains("\"written\":\"%user%\"");
        await Assert.That(dump).Contains("\"name\":\"Ada\"");
        // the type as json writes one: its identity
        await Assert.That(dump).Contains("\"type\":{\"name\":\"dict\"}");
    }

    [Test] public async Task ADump_ShowsAValueThatDoesntResolve_AsNotSet()
    {
        var dump = await Written(Template("%missing%"), global::app.View.Debug, "json");

        await Assert.That(dump).Contains("\"written\":\"%missing%\"");
        await Assert.That(dump).Contains("not set");
    }

    // a .pr row's container that holds a variable (list.query's Query, as wherevariable.test.pr has it), read with
    // its goal
    private async Task<global::app.data.@this> ContainerTemplate()
    {
        const string pr = """
            {"name":"Start","step":[{"index":0,"text":"list.query(List=%users%, Query={where: {field: \"age\", op: \">\", value: %min%}})","line":{"number":1},
              "code":[{"module":"list","name":"query","property":[
                {"name":"Query","type":{"name":"query","template":"plang"},"value":{"where":{"field":"age","op":">","value":"%min%"}},"variable":[{"text":"%min%","code":[{"variable":"min"}]}]}]}]}]}
            """;
        var goal = await RealGoalLoad.Read(_app, pr);
        return goal.Step[0].Code[0].Property["Query"]!.Data(Ctx);
    }

    // as written: the slice the .pr holds, once — never the decoded structure with its variable dumped inside it
    private const string AsAuthored = "\"written\":{\"where\":{\"field\":\"age\",\"op\":\">\",\"value\":\"%min%\"}}";

    [Test] public async Task ADump_ShowsAContainerTemplateAsWritten_AndNotSet_WhenItsVariableIsnt()
    {
        var dump = await Written(await ContainerTemplate(), global::app.View.Debug, "json");

        await Assert.That(dump).StartsWith("{" + AsAuthored + ",\"value\":");
        await Assert.That(dump).Contains("{\"written\":\"%min%\",\"value\":\"not set\"}");
    }

    [Test] public async Task ADump_ShowsAContainerTemplateAsWritten_AndTheValueItResolvesTo()
    {
        await Ctx.Variable.Set("min", global::app.type.item.@this.Create(30L, Ctx));

        var dump = await Written(await ContainerTemplate(), global::app.View.Debug, "json");

        await Assert.That(dump).StartsWith("{" + AsAuthored + ",\"value\":");
        await Assert.That(dump).Contains("\"value\":30");
    }

    // only a name that reads nothing is not set: a value that resolves to a failure is shown as that error
    [Test] public async Task ADump_ShowsAValueThatResolvesToAFailure_AsItsError()
    {
        await Ctx.Variable.Set("bad", Ctx.Error(new global::app.error.@this("the price feed is down", "FeedDown", 503)));

        var dump = await Written(Template("%bad%"), global::app.View.Debug, "json");

        await Assert.That(dump).Contains("\"written\":\"%bad%\"");
        await Assert.That(dump).Contains("the price feed is down");
        await Assert.That(dump).DoesNotContain("not set");
    }

    // a value whose diagnostics throw on read
    private sealed class Faulty : global::app.type.item.@this
    {
        public Faulty(global::app.type.@this type) : base(type) { }

        [global::app.Debug] public string Probe => throw new System.InvalidOperationException("the probe broke");
    }

    [Test] public async Task ADump_ShowsADiagnosticThatThrows_AsItsError_AndGoesOn()
    {
        var faulty = new global::app.data.@this("f", new Faulty(_app.type.list["item"]), context: Ctx);

        var dump = await Written(faulty, global::app.View.Debug, "json");

        await Assert.That(dump).Contains("\"probe\":");
        await Assert.That(dump).Contains("the probe broke");
    }

    // characters nobody should see: whole in plang's own store, whichever format writes it; **** in every other view
    [Test]
    [Arguments("Store", "plang", "s3cret")]
    [Arguments("Store", "json", "s3cret")]
    [Arguments("Out", "plang", "****")]
    [Arguments("Out", "json", "****")]
    [Arguments("Debug", "json", "****")]
    public async Task ASensitiveValue_IsShownAsTheViewShowsIt(string view, string format, string shown)
    {
        var secret = new global::app.data.@this("key", new global::app.type.item.sensitive.@this("s3cret", _app.type.list["sensitive"]), context: Ctx);

        var written = await Written(secret, View(view), format);

        await Assert.That(written).Contains($"\"{shown}\"");
        if (shown == "****") await Assert.That(written).DoesNotContain("s3cret");
    }

    // a Data's name is plang's own store's: kept there, never shown on the wire
    [Test]
    [Arguments("Store", true)]
    [Arguments("Out", false)]
    public async Task ARecordsName_IsKeptOnlyInTheStore(string view, bool named)
    {
        var data = new global::app.data.@this("label", global::app.type.item.@this.Create("Ada", Ctx), context: Ctx);

        var written = await Written(data, View(view), "plang");

        await Assert.That(written.Contains("\"name\":\"label\"")).IsEqualTo(named);
    }

    private static global::app.View View(string name) => name switch
    {
        "Store" => global::app.View.Store,
        "Out" => global::app.View.Out,
        _ => global::app.View.Debug,
    };

    [Test] public async Task TheStore_KeepsAValueAsWritten()
    {
        await Ctx.Variable.Set("name", global::app.type.item.@this.Create("Ada", Ctx));

        var stored = await Written(Template("%name%"), global::app.View.Store, "plang");

        await Assert.That(stored).Contains("%name%");
        await Assert.That(stored).DoesNotContain("Ada");
    }
}
