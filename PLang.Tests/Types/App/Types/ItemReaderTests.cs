namespace PLang.Tests.App.Types;

using Limit = global::app.module.llm.type.limit.@this;

/// <summary>
/// A plang value read as tokens: a dict handed to a slot of another plang type is read by that type's own reader
/// (a record type with none of its own reads as a record, made by its Create) — never lowered through C#; and what
/// the value's tokens give is what the same value's json gives.
/// </summary>
public class ItemReaderTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/item-reader-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private global::app.type.item.@this Dict()
        => global::app.type.item.@this.Create(new Dictionary<string, object?> { ["token"] = 500L, ["tool"] = 3L }, Ctx);

    private ReaderBox Box()
    {
        if (_app.type.list.Items().All(t => t.Name != "readerbox")) _app.type.list.Add(typeof(ReaderBox), Ctx, "readerbox");
        return new(_app.type.list[typeof(ReaderBox)]);
    }

    [Test] public async Task ADictIntoARecordMember_ArrivesAsTheRecord()
    {
        var owner = Box();
        await (await owner.Set("limit", false, Dict(), Ctx)).IsSuccess();

        await Assert.That(owner.Limit).IsNotNull();
        await Assert.That(owner.Limit!.Token.ToString()).IsEqualTo("500");
        await Assert.That(owner.Limit.Tool.ToString()).IsEqualTo("3");
        await Assert.That(owner.Limit.Retry.ToString()).IsEqualTo("0");
    }

    // a value reads its members from what it is: a file, a kind of path, answers its own class's size
    [Test] public async Task AFile_AnswersItsSize_ThroughItsClass()
    {
        var file = global::app.type.item.path.@this.Resolve("/notes.txt", Ctx);
        await Assert.That(file).IsTypeOf<global::app.type.item.path.file.@this>();
        await Assert.That(_app.type.list.Member(file.GetType())["size"]).IsNotNull();
        var size = await file.Get(new global::app.data.@this("f", file, context: Ctx), "size");
        await Assert.That(size.Error?.Key).IsNotEqualTo("NotFound");
    }

    // a dict read into a record is read as tokens, and a value nested in it has none to give yet: the write answers
    // that as its error, never a .NET exception
    [Test] public async Task ADictHoldingADict_IntoARecordMember_AnswersNotTokens()
    {
        var owner = Box();
        var nested = global::app.type.item.@this.Create(new Dictionary<string, object?>
            { ["token"] = 500L, ["extra"] = new Dictionary<string, object?> { ["inner"] = 1L } }, Ctx);

        var answer = await owner.Set("limit", false, nested, Ctx);

        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("NotTokens");
    }

    // an entry still encoded (a .pr's lazy slice) is read as the value it decodes to, in the read's context: the write
    // answers what the same dict decoded answers
    [Test] public async Task ADictHoldingAnUnreadSlice_IntoARecordMember_ReadsTheSliceAsItsValue()
    {
        var owner = Box();
        var slice = Ctx.App.type.list[new global::app.type.@this("dict"), Ctx].Make("{\"inner\": 1}",
            (global::app.type.item.wire.kind.plang.@this)Ctx.App.type.list["wire"].kind["plang"]!, global::app.View.Out);
        var nested = global::app.type.item.@this.Create(new Dictionary<string, object?> { ["token"] = 500L, ["extra"] = slice }, Ctx);

        var answer = await owner.Set("limit", false, nested, Ctx);

        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("NotTokens");
    }

    // a member that can't be written answers why, never a throw
    [Test] public async Task AReadOnlyMember_RefusesTheWriteWithWhy()
    {
        var answer = await Box().Set("label", false, new global::app.type.item.text.@this("x", _app.type.list["text"]), Ctx);

        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("OwnMember");
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

        var viaTokens = Dict().Read(reader, null, Ctx, global::app.View.Out) as Limit;

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

/// <summary>A value with a member of a module's record type, and one it only answers — the members its type lists.</summary>
[global::app.Attributes.PlangType("readerbox")]
public sealed class ReaderBox : global::app.type.item.@this
{
    public ReaderBox(global::app.type.@this type) : base(type) { }

    [global::app.LlmBuilder] public global::app.module.llm.type.limit.@this? Limit { get; set; }

    [global::app.LlmBuilder] public global::app.type.item.text.@this? Label => null;
}
