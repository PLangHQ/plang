namespace PLang.Tests.App.TypedReturnsTests;

/// <summary>
/// A %ref% is detected where a row is READ: the data reader gives a string row carrying a variable
/// reference its row's type with template="plang" — plang's own syntax, parsed, never a guessed type —
/// and leaves a literal unflagged. A row with no type is refused loudly: every parameter carries its
/// type. This is the one place the build's graft and a .pr load both pass through.
/// </summary>
public class BuildTemplateStampTests
{
    private static global::app.data.@this Row(global::app.@this app, string json)
        => new global::app.data.reader.@this().Read(System.Text.Encoding.UTF8.GetBytes(json),
            new global::app.type.reader.ReadContext(app.User.Context));

    [Test]
    public async Task RefParam_ReadsAsTemplatePlang()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var row = Row(app, """{"name":"Message","type":{"name":"text"},"value":"hello %name%"}""");
        await Assert.That(row.Type?.Template).IsEqualTo("plang");
    }

    [Test]
    public async Task LiteralParam_StaysUnflagged()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var row = Row(app, """{"name":"Message","type":{"name":"text"},"value":"hello world"}""");
        await Assert.That(row.Type?.Template).IsNull();
    }

    [Test]
    public async Task EmbeddedRef_ReadsAsTemplatePlang()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var row = Row(app, """{"name":"Message","type":{"name":"text"},"value":"count is %n% today"}""");
        await Assert.That(row.Type?.Template).IsEqualTo("plang");
    }

    [Test]
    public async Task UntypedRow_IsRefused_NamingTheParameter()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var refused = Assert.Throws<System.Text.Json.JsonException>(() =>
            Row(app, """{"name":"Path","value":"notes.txt"}"""));
        await Assert.That(refused!.Message).Contains("parameter 'Path' has no type");
    }
}
