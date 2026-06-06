using System.Reflection;
using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 4
// Properties get a wire scope: C# type becomes Dictionary<string, object?> of primitives;
// the wire emits them as a nested `properties` object next to name/type/value/signature.

public class PropertiesWireShapeTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "plang-prop-" + Guid.NewGuid().ToString("N")[..8]));

    private static (global::app.channel.serializer.plang.@this plang, global::app.data.@this data, Action dispose)
        SeedData(string name = "thing", object? value = null)
    {
        var app = NewApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");
        var d = new global::app.data.@this(name, value ?? "v") { Context = app.User.Context };
        return (plang, d, () => app.DisposeAsync().GetAwaiter().GetResult());
    }

    [Test] public async Task Properties_Surface_IsDictionaryStringObject_NotIListData()
    {
        var t = typeof(global::app.data.Properties);
        await Assert.That(typeof(IDictionary<string, object?>).IsAssignableFrom(t)).IsTrue();
        await Assert.That(typeof(System.Collections.Generic.IList<global::app.data.@this>).IsAssignableFrom(t)).IsFalse();
    }

    private static async Task<global::app.data.@this> RoundTrip(object propValue)
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Properties["k"] = propValue;
            var wire = plang.Serialize(d).Value!;
            var back = plang.Deserialize(wire);
            return (global::app.data.@this)back.Value!;
        }
        finally { dispose(); }
    }

    [Test] public async Task Properties_RoundTrip_StringPrimitive()
    {
        var back = await RoundTrip("hello");
        await Assert.That(back.Properties["k"]).IsEqualTo("hello");
    }

    [Test] public async Task Properties_RoundTrip_IntPrimitive()
    {
        var back = await RoundTrip(42);
        await Assert.That(Convert.ToInt64(back.Properties["k"])).IsEqualTo(42L);
    }

    [Test] public async Task Properties_RoundTrip_LongPrimitive()
    {
        var back = await RoundTrip(123456789012L);
        await Assert.That(back.Properties["k"]).IsEqualTo(123456789012L);
    }

    [Test] public async Task Properties_RoundTrip_DoublePrimitive()
    {
        var back = await RoundTrip(3.14);
        // JSON 3.14 deserialises to decimal in our reader path; coerce for equality.
        await Assert.That(Convert.ToDouble(back.Properties["k"])).IsEqualTo(3.14);
    }

    [Test] public async Task Properties_RoundTrip_BoolPrimitive()
    {
        var back = await RoundTrip(true);
        await Assert.That(back.Properties["k"]).IsEqualTo(true);
    }

    [Test] public async Task Properties_RoundTrip_DateTimePrimitive()
    {
        var dt = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var back = await RoundTrip(dt);
        // DateTime serialises to ISO 8601 string; read-back is a string. Coerce.
        await Assert.That(DateTime.Parse(back.Properties["k"]!.ToString()!).ToUniversalTime()).IsEqualTo(dt);
    }

    [Test] public async Task Properties_RoundTrip_ByteArrayPrimitive()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var back = await RoundTrip(bytes);
        // byte[] serialises to base64 string on the wire; read-back is the string.
        await Assert.That(back.Properties["k"]).IsEqualTo(Convert.ToBase64String(bytes));
    }

    [Test] public async Task Properties_RoundTrip_NestedDictOfPrimitives()
    {
        var dict = new Dictionary<string, object?> { ["cost"] = 100L, ["model"] = "claude" };
        var back = await RoundTrip(dict);
        var roundDict = back.Properties["k"] as Dictionary<string, object?>;
        await Assert.That(roundDict).IsNotNull();
        await Assert.That(roundDict!["cost"]).IsEqualTo(100L);
        await Assert.That(roundDict["model"]).IsEqualTo("claude");
    }

    [Test] public async Task Properties_RoundTrip_ListOfPrimitives()
    {
        var list = new List<object?> { 1L, 2L, "three" };
        var back = await RoundTrip(list);
        var roundList = back.Properties["k"] as List<object?>;
        await Assert.That(roundList).IsNotNull();
        await Assert.That(roundList!.Count).IsEqualTo(3);
        await Assert.That(roundList[2]).IsEqualTo("three");
    }

    [Test] public async Task Wire_PropertiesEmittedAsNestedObject_SiblingOfReservedFields()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Properties["cost"] = 100L;
            var wire = plang.Serialize(d).Value!;
            using var doc = JsonDocument.Parse(wire);
            await Assert.That(doc.RootElement.TryGetProperty("properties", out var props)).IsTrue();
            await Assert.That(props.ValueKind).IsEqualTo(JsonValueKind.Object);
            await Assert.That(props.GetProperty("cost").GetInt64()).IsEqualTo(100L);
        }
        finally { dispose(); }
    }

    [Test] public async Task Wire_PropertyKey_DoesNotLeakToRootLevel()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Properties["cost"] = 100L;
            var wire = plang.Serialize(d).Value!;
            using var doc = JsonDocument.Parse(wire);
            await Assert.That(doc.RootElement.TryGetProperty("cost", out _)).IsFalse();
        }
        finally { dispose(); }
    }

    [Test] public async Task Wire_EmptyProperties_OmitsPropertiesFieldEntirely()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            var wire = plang.Serialize(d).Value!;
            using var doc = JsonDocument.Parse(wire);
            await Assert.That(doc.RootElement.TryGetProperty("properties", out _)).IsFalse();
        }
        finally { dispose(); }
    }

    [Test] public async Task Properties_KeyNamedValue_RoundTripsIntact()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Properties["value"] = "stays-in-properties-scope";
            var wire = plang.Serialize(d).Value!;
            var back = (global::app.data.@this)plang.Deserialize(wire).Value!;
            await Assert.That(back.Properties["value"]).IsEqualTo("stays-in-properties-scope");
        }
        finally { dispose(); }
    }

    [Test] public async Task Properties_KeyNamedSignature_RoundTripsIntact()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Properties["signature"] = "not-the-outer-sig";
            var wire = plang.Serialize(d).Value!;
            var back = (global::app.data.@this)plang.Deserialize(wire).Value!;
            await Assert.That(back.Properties["signature"]).IsEqualTo("not-the-outer-sig");
        }
        finally { dispose(); }
    }

    [Test] public async Task Properties_KeyNamedName_RoundTripsIntact()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Properties["name"] = "metadata-name";
            var wire = plang.Serialize(d).Value!;
            var back = (global::app.data.@this)plang.Deserialize(wire).Value!;
            await Assert.That(back.Properties["name"]).IsEqualTo("metadata-name");
        }
        finally { dispose(); }
    }

    [Test] public async Task Properties_IntValue_ReadBackAsLong_JsonPromotion()
    {
        var back = await RoundTrip(42);
        // JSON has no distinct int type — TryGetInt64 wins, so we get long on read.
        await Assert.That(back.Properties["k"]).IsTypeOf<long>();
    }

    [Test] public async Task OuterSignature_AfterPropertiesValueTamper_FailsVerify()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            // EnsureSigned requires an Actor — bare context fixtures skip signing.
            // Use SeedData's app.User.Context which carries an actor.
            d.Properties["cost"] = 100L;
            d.EnsureSigned();
            var wire = plang.Serialize(d).Value!;
            var tampered = wire.Replace("\"cost\":100", "\"cost\":999");
            await Assert.That(tampered).IsNotEqualTo(wire);

            var back = (global::app.data.@this)plang.Deserialize(tampered).Value!;
            back.Context = d.Context;
            var app = d.Context!.App;
            var verify = await app.RunAction<global::app.module.signing.verify>(
                new global::app.module.signing.verify
                {
                    Data = back,
                    SkipFreshnessCheck = new global::app.data.@this<global::app.type.@bool.@this>("", true)
                }, d.Context);
            await verify.IsFailure();
        }
        finally { dispose(); }
    }

    [Test] public async Task Wire_PropertiesValues_HaveNoNestedSignatures()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Properties["cost"] = 100L;
            d.Properties["model"] = "claude";
            var wire = plang.Serialize(d).Value!;
            using var doc = JsonDocument.Parse(wire);
            var props = doc.RootElement.GetProperty("properties");
            // Each Property value is a primitive — no signature objects under properties.
            foreach (var p in props.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Object)
                    await Assert.That(p.Value.TryGetProperty("signature", out _)).IsFalse();
            }
        }
        finally { dispose(); }
    }

    [Test] public async Task WireRead_UnknownTopLevelField_SilentlyIgnored_NotCapturedAsProperty()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Properties["k"] = "v";
            var wire = plang.Serialize(d).Value!;
            // Inject a top-level field at the start of the object.
            var injected = wire.Replace("{\"name\":", "{\"traceId\":\"abc\",\"name\":");
            var back = (global::app.data.@this)plang.Deserialize(injected).Value!;
            // Properties dictionary doesn't capture the unknown field.
            await Assert.That(back.Properties.ContainsKey("traceId")).IsFalse();
        }
        finally { dispose(); }
    }

    [Test] public async Task Properties_OldIListByIntIndexer_NoLongerExists()
    {
        var t = typeof(global::app.data.Properties);
        // The old IList surface had this[int]; new IDictionary surface only has this[string].
        var intIndexer = t.GetProperty("Item", new[] { typeof(int) });
        await Assert.That(intIndexer).IsNull();
    }
}
