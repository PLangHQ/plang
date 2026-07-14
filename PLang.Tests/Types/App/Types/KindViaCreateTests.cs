using text = global::app.type.item.text.@this;

namespace PLang.Tests.App.Types;

// W8 — KindHooks + the four X.Build statics are deleted. The kind now derives by building
// through the family's eager door (App.Type[name].Create) and reading it off the built value.
// This pins the architect's representative literals through that one door, using the same input
// shape the build site holds (a text.@this literal).
public class KindViaCreateTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-kind-via-create-" + System.Guid.NewGuid().ToString("N")[..8]));
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private string? KindOf(string typeName, object? raw)
    {
        var ctx = app.User.Context;
        var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this(typeName), context: ctx);
        return ctx.App.Type[typeName].Create(raw, carrier)?.Type.Kind?.Name;
    }

    [Test] public async Task Number_IntLiteral()     => await Assert.That(KindOf("number", (text)"42")).IsEqualTo("int");
    [Test] public async Task Number_DecimalLiteral()  => await Assert.That(KindOf("number", (text)"3.14")).IsEqualTo("decimal");
    [Test] public async Task Number_ExponentLiteral() => await Assert.That(KindOf("number", (text)"1e3")).IsEqualTo("double");
    [Test] public async Task Image_Extension()        => await Assert.That(KindOf("image", "photo.jpg")).IsEqualTo("jpg");
    [Test] public async Task Path_BareIsFile()        => await Assert.That(KindOf("path", "/srv/a.txt")).IsEqualTo("file");
    // Scheme-accurate (W8 option A): a built HttpPath reports its real scheme, so https stays https
    // (the deleted hook collapsed http+https → "http"). data-urls deferred to the base64 type.
    [Test] public async Task Path_HttpsScheme_IsHttps() => await Assert.That(KindOf("path", "https://example.com/a")).IsEqualTo("https");
}
