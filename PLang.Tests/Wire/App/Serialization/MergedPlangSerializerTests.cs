namespace PLang.Tests.App.Serialization;

// application/plang is plang's one own format (the wire type's plang kind) — application/plang+data and
// .pdata name nothing; the per-MIME value formats still write their own shape.

public class MergedPlangSerializerTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/MergedPlang-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    [Test] public async Task Mime_ApplicationPlangData_IsNotPlangsFormat()
    {
        var type = app.type.list.Stamp("application/plang+data", Ctx);
        await Assert.That(type.Name).IsEqualTo("binary");
        await Assert.That(type.kind.IsEmpty).IsTrue();
    }

    [Test] public async Task Extension_Pdata_IsNotPlangsFormat()
    {
        var type = app.type.list.Extension(".pdata", Ctx);
        await Assert.That(type.Name).IsEqualTo("binary");
        await Assert.That(type.kind.Name).IsEqualTo("pdata");
    }

    [Test] public async Task Extension_Plang_IsPlangsOwnFormat()
    {
        var type = app.type.list.Extension(".plang", Ctx);
        await Assert.That(type.Name).IsEqualTo("wire");
        await Assert.That(type.kind.Name).IsEqualTo("plang");
        await Assert.That(type.kind).IsTypeOf<global::app.type.item.wire.kind.plang.@this>();
    }

    [Test] public async Task TextPlain_RoundTrip_DataOkHello_YieldsHelloLiteral()
    {
        using var ms = new MemoryStream();
        await Ctx.Format("text/plain").Encode(ms, app.Ok("hello"), Ctx);
        var wire = System.Text.Encoding.UTF8.GetString(ms.ToArray()).TrimEnd();
        await Assert.That(wire).IsEqualTo("hello");
    }

    [Test] public async Task ApplicationJson_RoundTrip_DataOkHello_StripsWrapperOnWire()
    {
        using var ms = new MemoryStream();
        await Ctx.Format("application/json").Encode(ms, app.Ok("hello"), Ctx);
        var wire = System.Text.Encoding.UTF8.GetString(ms.ToArray()).TrimEnd();
        await Assert.That(wire).IsEqualTo("\"hello\"");
    }
}
