namespace PLang.Tests.App.Types;

/// <summary>Security probe (json-lines, kinds walkable): a goal can read a kind's facts along %!app.type.kind.json.X% but
/// must not rewrite the shared kind (mime, extension, compressible, text-ness) — a run never writes an object its app shares.
/// Asserts the SAFE behaviour: red is a goal re-pointing json's mime or extension for every app.</summary>
public class KindWalkWriteProbeTests
{
    private static global::app.type.item.variable.@this Path(global::app.@this app, string path)
        => new global::app.type.item.variable.parser.@this(path, app.type.list).Variable.Single();

    [Test]
    [Arguments("mime", "text/evil")]
    [Arguments("extension", ".evil")]
    [Arguments("compressible", "false")]
    [Arguments("istext", "false")]
    [Arguments("name", "evil")]
    public async Task AGoal_CannotRewriteAKindsFacts(string member, string value)
    {
        await using var app = new global::app.@this("/tmp/kwp-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var json = (global::app.type.item.kind.json.@this)app.type.list["item"].kind["json"]!;
        var mime = string.Join(",", json.Mime); var ext = string.Join(",", json.Extension);
        var compressible = json.Compressible; var isText = json.IsText; var name = json.Name;

        try { await Path(app, $"%!app.type.kind.json.{member}%").Set(context.Ok(context.Text(value)), context); } catch (System.Exception) { }

        await Assert.That(string.Join(",", json.Mime)).IsEqualTo(mime);
        await Assert.That(string.Join(",", json.Extension)).IsEqualTo(ext);
        await Assert.That(json.Compressible).IsEqualTo(compressible);
        await Assert.That(json.IsText).IsEqualTo(isText);
        await Assert.That(json.Name).IsEqualTo(name);
        await Assert.That(app.type.list.Mime("application/json")).IsSameReferenceAs(json);
    }
}
