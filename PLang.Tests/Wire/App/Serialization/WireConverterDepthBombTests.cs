using System.Text;

namespace PLang.Tests.App.Serialization;

// Security v1 F1 regression — pre-auth StackOverflow DoS via
// nested-Data wire deserialization. Wire.LiftDataIfShaped
// rebuilds a fresh Utf8JsonReader on every level (depth counter resets),
// so STJ's MaxDepth=64 only applied per-level. Bound the recursion to
// the same MaxReadDepth via an AsyncLocal so deep input rejects as a
// typed JsonException, not a stack overflow.

public class WireConverterDepthBombTests
{
    private static string DeeplyNestedWireJson(int depth)
    {
        // Each level: {"name":"a","type":{"name":"item"},"value": ... } — every row carries its type;
        // the open item slot's value is the next level, which drives the nested read.
        var sb = new StringBuilder();
        for (int i = 0; i < depth; i++) sb.Append("{\"name\":\"a\",\"type\":{\"name\":\"item\"},\"value\":");
        sb.Append("\"leaf\"");
        for (int i = 0; i < depth; i++) sb.Append('}');
        return sb.ToString();
    }

    [Test] public async Task Deserialize_ShallowNesting_StillWorks()
    {
        // Sanity: 16-level nesting is within budget and round-trips.
        var plang = new global::app.channel.serializer.plang.@this(global::PLang.Tests.TestApp.SharedContext);
        var json = DeeplyNestedWireJson(16);
        var result = plang.Deserialize(json);
        await result.IsSuccess();
        // The open item slot's content opens as a VALUE — a dict, never a bare Data — and the
        // next level rides as that dict's `value` entry (a container entry may be a Data).
        var outer = await result.Value();
        await Assert.That(outer).IsTypeOf<global::app.type.item.dict.@this>();
        var next = ((global::app.type.item.dict.@this)outer).Get("value", global::PLang.Tests.TestApp.SharedContext);
        await Assert.That(next).IsNotNull();
        await Assert.That(await next!.Value()).IsTypeOf<global::app.type.item.dict.@this>();   // level 2 opens the same way
    }

    [Test] public async Task Deserialize_DepthBomb_RejectsAsTypedError_NotCrash()
    {
        // 200 levels: well past the 64-level cap. Must surface a typed
        // PlangDeserializeError, NOT a StackOverflowException (which would
        // unrecoverably crash the test process).
        var plang = new global::app.channel.serializer.plang.@this(global::PLang.Tests.TestApp.SharedContext);
        var json = DeeplyNestedWireJson(200);
        var result = plang.Deserialize(json);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("PlangDeserializeError");
        // Rejected for its depth — by the reader's own depth budget or the nested-Data MaxReadDepth.
        await Assert.That(result.Error!.Message).Contains("depth");
    }

    [Test] public async Task Deserialize_DepthBomb_FromStream_RejectsAsTypedError()
    {
        // Same shape, async-stream path — covers the DeserializeAsync entry too.
        var plang = new global::app.channel.serializer.plang.@this(global::PLang.Tests.TestApp.SharedContext);
        var json = DeeplyNestedWireJson(200);
        var bytes = Encoding.UTF8.GetBytes(json);
        using var ms = new MemoryStream(bytes);
        var result = await plang.DeserializeAsync(ms);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("PlangDeserializeError");
    }
}
