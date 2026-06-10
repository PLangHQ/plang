namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 2
// Merged plang serializer — application/plang+data is gone, application/plang is the
// single registered serializer; Envelope class is deleted; per-MIME serializers still work.

public class MergedPlangSerializerTests
{
    [Test] public async Task Serializers_GetByType_ApplicationPlangData_ReturnsNull()
    {
        var reg = new global::app.channel.serializer.list.@this();
        await Assert.That(reg.GetByType("application/plang+data")).IsNull();
    }

    [Test] public async Task Serializers_PdataExtension_DoesNotResolve()
    {
        var reg = new global::app.channel.serializer.list.@this();
        await Assert.That(reg.GetByExtension(".pdata")).IsNull();
    }

    [Test] public async Task Serializers_GetByType_ApplicationPlang_ReturnsMergedSerializer()
    {
        var reg = new global::app.channel.serializer.list.@this();
        var s = reg.GetByType("application/plang");
        await Assert.That(s).IsNotNull();
        await Assert.That(s).IsTypeOf<global::app.channel.serializer.plang.@this>();
    }

    [Test] public async Task Serializers_PlangExtension_ResolvesMergedSerializer()
    {
        var reg = new global::app.channel.serializer.list.@this();
        var s = reg.GetByExtension(".plang");
        await Assert.That(s).IsNotNull();
        await Assert.That(s).IsTypeOf<global::app.channel.serializer.plang.@this>();
    }

    [Test] public async Task PlangSerializer_EnvelopeType_NoLongerExistsInAssembly()
    {
        var asm = typeof(global::app.channel.serializer.plang.@this).Assembly;
        var envelope = asm.GetType("app.channel.serializer.plang.Data+Envelope");
        await Assert.That(envelope).IsNull();
        var legacyOuter = asm.GetType("app.channel.serializer.plang.Data");
        await Assert.That(legacyOuter).IsNull();
    }

    [Test] public async Task PlangSerializer_FromEnvelopeFactory_NoLongerExistsOnSerializer()
    {
        var t = typeof(global::app.channel.serializer.plang.@this);
        var method = t.GetMethod("FromEnvelope",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        await Assert.That(method).IsNull();
    }

    [Test] public async Task TextPlain_RoundTrip_DataOkHello_YieldsHelloLiteral()
    {
        var text = new global::app.channel.serializer.Text();
        using var ms = new MemoryStream();
        await text.SerializeAsync(ms, global::app.data.@this.Ok("hello"));
        var wire = System.Text.Encoding.UTF8.GetString(ms.ToArray()).TrimEnd();
        await Assert.That(wire).IsEqualTo("hello");
    }

    [Test] public async Task ApplicationJson_RoundTrip_DataOkHello_StripsWrapperOnWire()
    {
        var json = new global::app.channel.serializer.Json();
        using var ms = new MemoryStream();
        await json.SerializeAsync(ms, global::app.data.@this.Ok("hello"));
        var wire = System.Text.Encoding.UTF8.GetString(ms.ToArray()).TrimEnd();
        await Assert.That(wire).IsEqualTo("\"hello\"");
    }
}
