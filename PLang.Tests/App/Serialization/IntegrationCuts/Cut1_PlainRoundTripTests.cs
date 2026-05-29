using System.Text.Json;

namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-serialize-cleanup — Integration Cut 1: Plain Data round-trip with implicit signing.
//
// Proves end-to-end:
//   - ISerializer input contract holds (Stage 1)
//   - wire converter Write/Read are symmetric (Stage 2)
//   - sign-if-missing fires automatically during the converter walk (Stage 2)
//   - canonicalization hash matches the wire shape (Stage 2 canonicalization fix)

public class Cut1_PlainRoundTripTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "plang-cut1-" + Guid.NewGuid().ToString("N")[..8]));

    private static async Task<(string wireJson, global::app.data.@this readBack, global::app.@this app)> WriteAndRead(string name, object? value)
    {
        var app = NewApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channels.Serializers.GetByMimeType("application/plang");

        var data = new global::app.data.@this(name, value) { Context = app.User.Context };
        var wire = plang.Serialize(data).Value!;
        var back = plang.Deserialize(wire);
        return (wire, (global::app.data.@this)back.Value!, app);
    }

    [Test] public async Task Cut1_WireJson_HasFourTopLevelFields_NameTypeValueSignature()
    {
        var (wire, _, app) = await WriteAndRead("greeting", "hello");
        await using (app)
        {
            using var doc = JsonDocument.Parse(wire);
            var root = doc.RootElement;
            await Assert.That(root.ValueKind).IsEqualTo(JsonValueKind.Object);

            var fields = new HashSet<string>();
            foreach (var p in root.EnumerateObject()) fields.Add(p.Name);

            await Assert.That(fields.Contains("name")).IsTrue();
            await Assert.That(fields.Contains("type")).IsTrue();
            await Assert.That(fields.Contains("value")).IsTrue();
            await Assert.That(fields.Contains("signature")).IsTrue();
            // pre-Stage-4, no `properties` field is emitted.
            await Assert.That(fields.Contains("properties")).IsFalse();
        }
    }

    [Test] public async Task Cut1_ReadBack_PreservesValueAndName()
    {
        var (_, back, app) = await WriteAndRead("greeting", "hello");
        await using (app)
        {
            await Assert.That(back.Name).IsEqualTo("greeting");
            await Assert.That(back.Value as string).IsEqualTo("hello");
        }
    }

    [Test] public async Task Cut1_ReadBack_SignaturePopulatedFromImplicitWriteSign()
    {
        var (_, back, app) = await WriteAndRead("greeting", "hello");
        await using (app)
        {
            await Assert.That(back.Signature).IsNotNull();
        }
    }

    [Test] public async Task Cut1_CryptoVerify_SucceedsAgainstWireBytes()
    {
        var (_, back, app) = await WriteAndRead("greeting", "hello");
        await using (app)
        {
            back.Context = app.User.Context;
            var verify = await app.RunAction<global::app.module.signing.verify>(
                new global::app.module.signing.verify
                {
                    Data = back,
                    SkipFreshnessCheck = new global::app.data.@this<bool>("", true)
                }, app.User.Context);
            await Assert.That(verify.Success).IsTrue();
        }
    }
}
