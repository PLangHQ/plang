using System.Reflection;
using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 4
// Properties get a wire scope: C# type becomes Dictionary<string, object?> of primitives;
// the wire emits them as a nested `properties` object next to name/type/value/signature.

public class PropertiesWireShapeTests
{
    private static global::app.@this NewApp() => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "plang-prop-" + Guid.NewGuid().ToString("N")[..8])).Testing();

    private static (global::app.type.kind.@this plang, global::app.data.@this data, Action dispose)
        SeedData(string name = "thing", object? value = null)
    {
        var app = NewApp();
        var plang = app.actor.list.User.Context.Format("application/plang");
        var d = new global::app.data.@this(name, value ?? "v", context: app.actor.list.User.Context);
        return (plang, d, () => app.DisposeAsync().GetAwaiter().GetResult());
    }

    // A Data serialized within an actor scope is wrapped in a `signature` layer;
    // the data record (with name/type/value/properties) rides under `value`. Tests
    // that inspect the data envelope unwrap to it here.
    private static JsonElement Inner(string wire)
    {
        using var doc = JsonDocument.Parse(wire);
        var root = doc.RootElement;
        return root.TryGetProperty("@schema", out var s) && s.GetString() == "signature"
            ? root.GetProperty("value").Clone()
            : root.Clone();
    }

    [Test] public async Task Property_IsAListOfPropertyRows_NotADictionary_NotDatas()
    {
        var t = typeof(global::app.type.property.list.@this);
        // rows of name, type and plang value — read async through Value/Get, never a mutable dictionary or Datas
        await Assert.That(typeof(IReadOnlyList<global::app.type.property.@this>).IsAssignableFrom(t)).IsTrue();
        await Assert.That(typeof(IDictionary<string, object?>).IsAssignableFrom(t)).IsFalse();
        await Assert.That(typeof(System.Collections.Generic.IList<global::app.data.@this>).IsAssignableFrom(t)).IsFalse();
    }

    private static async Task<global::app.data.@this> RoundTrip(object propValue)
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Property.Set("k", propValue);
            var wire = (await plang.Serialize(d, d.Context).Value())!.Clr<string>()!;
            var back = plang.Deserialize(wire, d.Context);
            return back;
        }
        finally { dispose(); }
    }

    [Test] public async Task Properties_RoundTrip_StringPrimitive()
    {
        var back = await RoundTrip("hello");
        await Assert.That(((await back.Property.Value("k")))?.ToString()).IsEqualTo("hello");
    }

    [Test] public async Task Properties_RoundTrip_IntPrimitive()
    {
        var back = await RoundTrip(42);
        await Assert.That(await back.Property.Get<long>("k")).IsEqualTo(42L);
    }

    [Test] public async Task Properties_RoundTrip_LongPrimitive()
    {
        var back = await RoundTrip(123456789012L);
        await Assert.That((await back.Property.Get<long>("k"))).IsEqualTo(123456789012L);
    }

    [Test] public async Task Properties_RoundTrip_DoublePrimitive()
    {
        var back = await RoundTrip(3.14);
        await Assert.That(await back.Property.Get<double>("k")).IsEqualTo(3.14);
    }

    [Test] public async Task Properties_RoundTrip_BoolPrimitive()
    {
        var back = await RoundTrip(true);
        await Assert.That((await back.Property.Get<bool>("k"))).IsEqualTo(true);
    }

    [Test] public async Task Properties_RoundTrip_DateTimePrimitive()
    {
        var dt = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var back = await RoundTrip(dt);
        // DateTime serialises to ISO 8601 string; read-back is a string. Coerce.
        await Assert.That(DateTime.Parse((await back.Property.Value("k"))!.ToString()!).ToUniversalTime()).IsEqualTo(dt);
    }

    [Test] public async Task Properties_RoundTrip_ByteArrayPrimitive()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var back = await RoundTrip(bytes);
        // byte[] serialises to base64 string on the wire; read-back is the string.
        await Assert.That(await back.Property.Get<string>("k")).IsEqualTo(Convert.ToBase64String(bytes));
    }

    [Test] public async Task Properties_RoundTrip_NestedDictOfPrimitives()
    {
        var dict = new Dictionary<string, object?> { ["cost"] = 100L, ["model"] = "claude" };
        var back = await RoundTrip(dict);
        // the dict comes back as the dict it was written as, its entries keeping their types
        var roundDict = (await back.Property.Value("k")) as global::app.type.item.dict.@this;
        await Assert.That(roundDict).IsNotNull();
        var cost = await roundDict!.Get("cost", back.Context!)!.Value();
        await Assert.That(cost is global::app.type.item.number.@this).IsTrue();
        await Assert.That(cost!.ToString()).IsEqualTo("100");
        await Assert.That((await roundDict.Get("model", back.Context!)!.Value())?.ToString()).IsEqualTo("claude");
    }

    [Test] public async Task Properties_RoundTrip_ListOfPrimitives()
    {
        var list = new List<object?> { 1L, 2L, "three" };
        var back = await RoundTrip(list);
        // the list comes back as the list it was written as, its elements keeping their types
        var roundList = (await back.Property.Value("k")) as global::app.type.item.list.@this;
        await Assert.That(roundList).IsNotNull();
        await Assert.That(roundList!.Count.ToString()).IsEqualTo("3");
        await Assert.That((await roundList.At(2L, back.Context!).Value())?.ToString()).IsEqualTo("three");
        await Assert.That(await roundList.At(0L, back.Context!).Value() is global::app.type.item.number.@this).IsTrue();
    }

    [Test] public async Task Wire_PropertiesEmittedAsNestedObject_SiblingOfReservedFields()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Property.Set("cost", 100L);
            var wire = (await plang.Serialize(d, d.Context).Value())!.Clr<string>()!;
            var rec = Inner(wire);
            await Assert.That(rec.TryGetProperty("properties", out var props)).IsTrue();
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
            d.Property.Set("cost", 100L);
            var wire = (await plang.Serialize(d, d.Context).Value())!.Clr<string>()!;
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
            var wire = (await plang.Serialize(d, d.Context).Value())!.Clr<string>()!;
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
            d.Property.Set("value", "stays-in-properties-scope");
            var wire = (await plang.Serialize(d, d.Context).Value())!.Clr<string>()!;
            var back = plang.Deserialize(wire, d.Context);
            await Assert.That(((await back.Property.Value("value")))?.ToString()).IsEqualTo("stays-in-properties-scope");
        }
        finally { dispose(); }
    }

    [Test] public async Task Properties_KeyNamedSignature_RoundTripsIntact()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Property.Set("signature", "not-the-outer-sig");
            var wire = (await plang.Serialize(d, d.Context).Value())!.Clr<string>()!;
            var back = plang.Deserialize(wire, d.Context);
            await Assert.That(((await back.Property.Value("signature")))?.ToString()).IsEqualTo("not-the-outer-sig");
        }
        finally { dispose(); }
    }

    [Test] public async Task Properties_KeyNamedName_RoundTripsIntact()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Property.Set("name", "metadata-name");
            var wire = (await plang.Serialize(d, d.Context).Value())!.Clr<string>()!;
            var back = plang.Deserialize(wire, d.Context);
            await Assert.That(((await back.Property.Value("name")))?.ToString()).IsEqualTo("metadata-name");
        }
        finally { dispose(); }
    }

    [Test] public async Task Properties_IntValue_ReadBackAsANumber()
    {
        var back = await RoundTrip(42);
        await Assert.That(await back.Property.Value("k")).IsTypeOf<global::app.type.item.number.@this>();
    }

    // Properties ride inside the signed value: real signing refuses a tampered Properties value on read.
    [Test] public async Task OuterSignature_AfterPropertiesValueTamper_FailsVerify()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-propwire-" + System.Guid.NewGuid().ToString("N")[..8])).TestIdentity();
        var ctx = app.actor.list.User.Context;
        var plang = ctx.Format("application/plang");
        var d = new global::app.data.@this("thing", "v", context: ctx);
        d.Property.Set("cost", 100L);
        var wire = (await plang.Serialize(d, ctx).Value())!.Clr<string>()!;
        var tampered = wire.Replace("\"cost\":100", "\"cost\":999");
        await Assert.That(tampered).IsNotEqualTo(wire);
        // the same Data signed again (its own nonce) reads back untampered
        await plang.Deserialize((await plang.Serialize(d, ctx).Value())!.Clr<string>()!, ctx).IsSuccess();

        var back = plang.Deserialize(tampered, ctx);

        await back.IsFailure();
        await Assert.That(back.Error!.Key).IsEqualTo("DataHashMismatch");
    }

    [Test] public async Task Wire_PropertiesValues_HaveNoNestedSignatures()
    {
        var (plang, d, dispose) = SeedData();
        try
        {
            d.Property.Set("cost", 100L);
            d.Property.Set("model", "claude");
            var wire = (await plang.Serialize(d, d.Context).Value())!.Clr<string>()!;
            var rec = Inner(wire);
            var props = rec.GetProperty("properties");
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
            d.Property.Set("k", "v");
            var wire = (await plang.Serialize(d, d.Context).Value())!.Clr<string>()!;
            // Inject a top-level field at the start of the object.
            var injected = wire.Replace("{\"name\":", "{\"traceId\":\"abc\",\"name\":");
            var back = plang.Deserialize(injected, d.Context);
            // Properties dictionary doesn't capture the unknown field.
            await Assert.That(back.Property.Contains("traceId")).IsFalse();
        }
        finally { dispose(); }
    }
}
