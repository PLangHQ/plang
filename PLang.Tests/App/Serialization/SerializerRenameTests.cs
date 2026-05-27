using System.Reflection;
using app.channels.serializers;
using app.channels.serializers.serializer;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 1
// OBP rename pass on the ISerializer family + Serializers registry.
// Coverage matrix rows 1.3, 1.4, 1.5, 1.6. The owner is "serializer", so the qualifier
// suffix on ContentType / FileExtension carries no information; same for *Core on channel.

public class SerializerRenameTests
{
    // 1.3 — serializer.Type returns the MIME string on each concrete serializer.
    [Test] public async Task Type_OnPlangSerializer_ReturnsApplicationPlang()
    {
        var s = new global::app.channels.serializers.serializer.plang.@this();
        await Assert.That(s.Type).IsEqualTo("application/plang");
    }

    [Test] public async Task Type_OnJsonSerializer_ReturnsApplicationJson()
    {
        var s = new global::app.channels.serializers.serializer.Json();
        await Assert.That(s.Type).IsEqualTo("application/json");
    }

    [Test] public async Task Type_OnTextSerializer_ReturnsTextPlain()
    {
        var s = new global::app.channels.serializers.serializer.Text();
        await Assert.That(s.Type).IsEqualTo("text/plain");
    }

    // 1.4 — serializer.Extension returns the dotted extension on each concrete serializer.
    [Test] public async Task Extension_OnPlangSerializer_ReturnsDotPlang()
    {
        var s = new global::app.channels.serializers.serializer.plang.@this();
        await Assert.That(s.Extension).IsEqualTo(".plang");
    }

    [Test] public async Task Extension_OnJsonSerializer_ReturnsDotJson()
    {
        var s = new global::app.channels.serializers.serializer.Json();
        await Assert.That(s.Extension).IsEqualTo(".json");
    }

    [Test] public async Task Extension_OnTextSerializer_ReturnsDotTxt()
    {
        var s = new global::app.channels.serializers.serializer.Text();
        await Assert.That(s.Extension).IsEqualTo(".txt");
    }

    // 1.5 — Serializers.GetByType resolves the plang serializer; the previous
    //       GetByContentType name is gone.
    [Test] public async Task Serializers_GetByType_ResolvesPlangSerializer()
    {
        var registry = new global::app.channels.serializers.@this();
        var plang = registry.GetByType("application/plang");
        await Assert.That(plang).IsNotNull();
        await Assert.That(plang!.Type).IsEqualTo("application/plang");
    }

    [Test] public async Task Serializers_GetByContentType_MethodRemoved()
    {
        var t = typeof(global::app.channels.serializers.@this);
        var legacy = t.GetMethod("GetByContentType", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(legacy).IsNull();
    }

    // 1.6 — Serializers.Types enumerable lists registered MIMEs; ContentTypes is gone.
    [Test] public async Task Serializers_Types_EnumeratesRegisteredMimes()
    {
        var registry = new global::app.channels.serializers.@this();
        var types = registry.Types.ToList();
        await Assert.That(types).Contains("application/json");
        await Assert.That(types).Contains("application/plang");
        await Assert.That(types).Contains("text/plain");
    }

    [Test] public async Task Serializers_ContentTypes_PropertyRemoved()
    {
        var t = typeof(global::app.channels.serializers.@this);
        var legacy = t.GetProperty("ContentTypes", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(legacy).IsNull();
    }

    // Old name on instance — covers the failure-matrix row for "ContentType access at callsite".
    [Test] public async Task PlangSerializer_ContentType_PropertyRemoved()
    {
        var t = typeof(global::app.channels.serializers.serializer.plang.@this);
        var legacy = t.GetProperty("ContentType", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(legacy).IsNull();
    }

    [Test] public async Task PlangSerializer_FileExtension_PropertyRemoved()
    {
        var t = typeof(global::app.channels.serializers.serializer.plang.@this);
        var legacy = t.GetProperty("FileExtension", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(legacy).IsNull();
    }
}
