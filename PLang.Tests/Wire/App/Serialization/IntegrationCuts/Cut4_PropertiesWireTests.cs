using System.Text.Json;

namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-serialize-cleanup — Integration Cut 4: Properties wire shape + navigation.

public class Cut4_PropertiesWireTests
{
    private static global::app.@this NewApp() => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "plang-cut4-" + Guid.NewGuid().ToString("N")[..8])).Testing();

    private static async Task<(string wire, global::app.data.@this back, global::app.@this app)> WriteAndRead()
    {
        var app = NewApp();
        var plang = app.actor.list.User.Context.Format("application/plang");
        var d = new global::app.data.@this("response", "Hello!", context: app.actor.list.User.Context);
        d.Properties["cost"] = 100;
        d.Properties["model"] = "claude-opus-4-7";
        var wire = (await plang.Serialize(d, app.actor.list.User.Context).Value())!.Clr<string>()!;
        var back = plang.Deserialize(wire, app.actor.list.User.Context);
        return (wire, back, app);
    }

    // A Data serialized within an actor scope is wrapped in a `signature` layer;
    // the data record (name/type/value/properties) rides under `value`.
    private static JsonElement Inner(string wire)
    {
        using var doc = JsonDocument.Parse(wire);
        var root = doc.RootElement;
        return root.TryGetProperty("@schema", out var s) && s.GetString() == "signature"
            ? root.GetProperty("value").Clone()
            : root.Clone();
    }

    [Test] public async Task Cut4_WireJson_HasFiveTopLevelFields_IncludingNestedProperties()
    {
        var (wire, _, app) = await WriteAndRead();
        await using (app)
        {
            // The outbound wire is the signature LAYER; the data record rides under value.
            using var doc = JsonDocument.Parse(wire);
            await Assert.That(doc.RootElement.GetProperty("@schema").GetString()).IsEqualTo("signature");

            var rec = Inner(wire);
            var fields = new HashSet<string>();
            foreach (var p in rec.EnumerateObject()) fields.Add(p.Name);
            // binding label off the outbound wire
            await Assert.That(fields.Contains("name")).IsFalse();
            await Assert.That(fields.Contains("type")).IsTrue();
            await Assert.That(fields.Contains("value")).IsTrue();
            await Assert.That(fields.Contains("properties")).IsTrue();
            // The data record no longer carries a `signature` field — signing is the outer layer.
            await Assert.That(fields.Contains("signature")).IsFalse();
        }
    }

    [Test] public async Task Cut4_PropertiesObject_IsNested_NotFlattenedToRoot()
    {
        var (wire, _, app) = await WriteAndRead();
        await using (app)
        {
            var rec = Inner(wire);
            var props = rec.GetProperty("properties");
            await Assert.That(props.ValueKind).IsEqualTo(JsonValueKind.Object);
            await Assert.That(props.GetProperty("cost").GetInt64()).IsEqualTo(100L);
            await Assert.That(props.GetProperty("model").GetString()).IsEqualTo("claude-opus-4-7");
            // Not at the data record's root.
            await Assert.That(rec.TryGetProperty("cost", out _)).IsFalse();
        }
    }

    [Test] public async Task Cut4_ReadBack_PropertiesValuesPreserved_IntPromotedToLong()
    {
        var (_, back, app) = await WriteAndRead();
        await using (app)
        {
            await Assert.That((await back.Properties.Value("cost"))).IsEqualTo(100L);
            await Assert.That(((await back.Properties.Value("model")))?.ToString()).IsEqualTo("claude-opus-4-7");
        }
    }

    // Properties ride inside the signed value: real signing refuses a tampered Properties value on read.
    [Test] public async Task Cut4_TamperingPropertyValue_FailsOuterSignatureVerify()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-cut4-" + System.Guid.NewGuid().ToString("N")[..8])).TestIdentity();
        var ctx = app.actor.list.User.Context;
        var plang = ctx.Format("application/plang");
        var d = new global::app.data.@this("response", "Hello!", context: ctx);
        d.Properties["cost"] = 100;
        var wire = (await plang.Serialize(d, ctx).Value())!.Clr<string>()!;
        var tampered = wire.Replace("\"cost\":100", "\"cost\":999");
        await Assert.That(tampered).IsNotEqualTo(wire);
        // the same Data signed again (its own nonce) reads back untampered
        await plang.Deserialize((await plang.Serialize(d, ctx).Value())!.Clr<string>()!, ctx).IsSuccess();

        var back = plang.Deserialize(tampered, ctx);

        await back.IsFailure();
        await Assert.That(back.Error!.Key).IsEqualTo("DataHashMismatch");
    }
}
