namespace PLang.Tests.App.Types;

using Limit = global::app.module.llm.type.limit.@this;

/// <summary>
/// A plang value read as tokens: a dict handed to a slot of another plang type is read by that type's own reader
/// (a record type with none of its own reads as a record, made by its Create) — never lowered through C#; and what
/// the value's tokens give is what the same value's json gives.
/// </summary>
public class ItemReaderTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/item-reader-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    // a value with a member of a module's record type
    private sealed class box : global::app.type.item.@this
    {
        public box(global::app.type.@this type) : base(type) { }
        public Limit? Limit { get; set; }
    }

    private global::app.type.item.@this Dict()
        => global::app.type.item.@this.Create(new Dictionary<string, object?> { ["token"] = 500L, ["tool"] = 3L }, Ctx);

    [Test] public async Task ADictIntoARecordMember_ArrivesAsTheRecord()
    {
        var owner = new box(_app.type.list["item"]);
        await owner.Set("limit", false, Dict(), Ctx);

        await Assert.That(owner.Limit).IsNotNull();
        await Assert.That(owner.Limit!.Token.ToString()).IsEqualTo("500");
        await Assert.That(owner.Limit.Tool.ToString()).IsEqualTo("3");
        await Assert.That(owner.Limit.Retry.ToString()).IsEqualTo("0");
    }

    // a list of a record type holds that type: each element read by the element type's reader, Record when it has none
    [Test] public async Task AListOfMessagesReadFromJson_HoldsMessages()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("[{\"role\":\"user\",\"content\":\"hi\"}]");
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var json = new global::app.type.item.kind.json.Reader(utf8, bytes);
        var list = _app.type.list.Reader.Reader("list", "message", Ctx)
            .Read(ref json, "message", new global::app.type.reader.ReadContext(Ctx)) as global::app.type.item.list.@this;

        await Assert.That(list).IsNotNull();
        var element = list!.Items(Ctx).Single().Peek() as global::app.module.llm.type.message.@this;
        await Assert.That(element).IsNotNull();
        await Assert.That(element!.Role).IsEqualTo("user");
        await Assert.That(element.Content).IsEqualTo("hi");
    }

    [Test] public async Task ADictReadAsTokens_IsTheRecordItsJsonReadsAs()
    {
        var reader = _app.type.list.Reader.Reader("limit", null, Ctx);

        var viaTokens = Dict().Read(reader, null, Ctx) as Limit;

        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"token\":500,\"tool\":3}");
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var json = new global::app.type.item.kind.json.Reader(utf8, bytes);
        var viaJson = reader.Read(ref json, null, new global::app.type.reader.ReadContext(Ctx)) as Limit;

        await Assert.That(viaTokens).IsNotNull();
        await Assert.That(viaJson).IsNotNull();
        await Assert.That(viaTokens!.Token.ToString()).IsEqualTo(viaJson!.Token.ToString());
        await Assert.That(viaTokens.Tool.ToString()).IsEqualTo(viaJson.Tool.ToString());
        await Assert.That(viaTokens.Retry.ToString()).IsEqualTo(viaJson.Retry.ToString());
    }
}
