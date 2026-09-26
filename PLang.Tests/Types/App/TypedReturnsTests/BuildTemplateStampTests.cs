namespace PLang.Tests.App.TypedReturnsTests;

/// <summary>
/// A row read is exactly its row's type: it is a template only when the row carries the marker
/// (template="plang", born at build), never because of what its value holds — a text holding %x%
/// with no marker is plain text. A row with no type is refused loudly: every parameter carries its
/// type. This is the one place the build's graft and a .pr load both pass through.
/// </summary>
public class BuildTemplateStampTests
{
    private static global::app.data.@this Row(global::app.@this app, string json)
        => new global::app.data.reader.@this().Read(System.Text.Encoding.UTF8.GetBytes(json),
            new global::app.type.reader.ReadContext(app.User.Context));

    [Test]
    public async Task MarkedRow_ReadsAsTemplatePlang()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var row = Row(app, """{"name":"Message","type":{"name":"text","template":"plang"},"value":"hello %name%"}""");
        await Assert.That(row.Type?.Template).IsEqualTo("plang");
    }

    // A value holding %name% with no marker is text — no guess from its content.
    [Test]
    public async Task UnmarkedRowHoldingAVariable_StaysText()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var row = Row(app, """{"name":"Message","type":{"name":"text"},"value":"hello %name%"}""");
        await Assert.That(row.Type?.Template).IsNull();
        await Assert.That((await row.Value())?.ToString()).IsEqualTo("hello %name%");
    }

    [Test]
    public async Task LiteralParam_StaysUnflagged()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var row = Row(app, """{"name":"Message","type":{"name":"text"},"value":"hello world"}""");
        await Assert.That(row.Type?.Template).IsNull();
    }

    [Test]
    public async Task MarkedEmbeddedRef_ReadsAsTemplatePlang()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var row = Row(app, """{"name":"Message","type":{"name":"text","template":"plang"},"value":"count is %n% today"}""");
        await Assert.That(row.Type?.Template).IsEqualTo("plang");
    }

    [Test]
    public async Task UntypedRow_IsRefused_NamingTheProperty()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var refused = Assert.Throws<System.Text.Json.JsonException>(() =>
            Row(app, """{"name":"Path","value":"notes.txt"}"""));
        await Assert.That(refused!.Message).Contains("property 'Path' has no type");
    }
}
