namespace PLang.Tests.App.DataTests;

/// <summary>
/// A value that holds variables is written as the view says: plang's store keeps it as written (<c>%name%</c>); a dump
/// shows it as written beside the value it resolves to, with its type, or that it is not set.
/// </summary>
public class ViewWriteTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/view-write-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
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
        await Assert.That(dump).Contains("\"type\":\"dict\"");
    }

    [Test] public async Task ADump_ShowsAValueThatDoesntResolve_AsNotSet()
    {
        var dump = await Written(Template("%missing%"), global::app.View.Debug, "json");

        await Assert.That(dump).Contains("\"written\":\"%missing%\"");
        await Assert.That(dump).Contains("not set");
    }

    [Test] public async Task TheStore_KeepsAValueAsWritten()
    {
        await Ctx.Variable.Set("name", global::app.type.item.@this.Create("Ada", Ctx));

        var stored = await Written(Template("%name%"), global::app.View.Store, "plang");

        await Assert.That(stored).Contains("%name%");
        await Assert.That(stored).DoesNotContain("Ada");
    }
}
