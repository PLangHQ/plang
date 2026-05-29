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
        // Each level: {"name":"a","value": ... }   value slot drives the
        // LiftDataIfShaped recursion that resets STJ's depth budget.
        var sb = new StringBuilder();
        for (int i = 0; i < depth; i++) sb.Append("{\"name\":\"a\",\"value\":");
        sb.Append("\"leaf\"");
        for (int i = 0; i < depth; i++) sb.Append('}');
        return sb.ToString();
    }

    [Test] public async Task Deserialize_ShallowNesting_StillWorks()
    {
        // Sanity: 16-level nesting is within budget and round-trips.
        var plang = new global::app.channel.serializer.plang.@this();
        var json = DeeplyNestedWireJson(16);
        var result = plang.Deserialize(json);
        await Assert.That(result.Success).IsTrue();
    }

    [Test] public async Task Deserialize_DepthBomb_RejectsAsTypedError_NotCrash()
    {
        // 200 levels: well past the 64-level cap. Must surface a typed
        // PlangDeserializeError, NOT a StackOverflowException (which would
        // unrecoverably crash the test process).
        var plang = new global::app.channel.serializer.plang.@this();
        var json = DeeplyNestedWireJson(200);
        var result = plang.Deserialize(json);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("PlangDeserializeError");
    }

    [Test] public async Task Deserialize_DepthBomb_FromStream_RejectsAsTypedError()
    {
        // Same shape, async-stream path — covers the DeserializeAsync entry too.
        var plang = new global::app.channel.serializer.plang.@this();
        var json = DeeplyNestedWireJson(200);
        var bytes = Encoding.UTF8.GetBytes(json);
        using var ms = new MemoryStream(bytes);
        var result = await plang.DeserializeAsync(ms);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("PlangDeserializeError");
    }
}
