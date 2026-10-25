using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.signing;

/// <summary>
/// A replay is a wire arriving twice: an arrival's nonce is presented once — by the first verify of the layer that
/// arrival's read birthed — and every copy of that Data shares it. So a value that arrived verifies and names its signer
/// as often as it is asked; the same wire decoded again is NonceReplay. On a fixed clock (NowUtc), so nothing here
/// depends on how fast it runs.
/// </summary>
public class ArrivedSignatureTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Before(Test)]
    public async Task Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_arrived_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir, global::PLang.Tests.Parent.Actor);
        _app.TestIdentity();
        await Clock(Now);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true);
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;
    private global::app.type.kind.@this Plang => _app.type.list.Mime("application/plang");

    private async Task Clock(DateTimeOffset at)
        => await Ctx.Variable.Set("NowUtc", new global::app.type.item.datetime.@this(at, _app.type.list[typeof(global::app.type.item.datetime.@this)]));

    private async Task<Data> Program(string module, string action, params (string, object?)[] properties)
        => await global::PLang.Tests.Shared.Make.Action(Ctx, module, action, properties).Start(Ctx);

    private async Task<Data> Read(string path)
        => await new global::app.type.item.variable.@this(path, _app.type.list).Start(Ctx);

    // a signed order, sent: the bytes that cross
    private async Task<byte[]> Sent()
    {
        await Ctx.Variable.Set("order", new Dictionary<string, object?> { ["total"] = 5L });
        var signed = await Program("signing", "sign", ("Data", "%order%"));
        await signed.IsSuccess();
        await Ctx.Variable.Set("signed", signed);
        using var wire = new System.IO.MemoryStream();
        await (await Plang.Encode(wire, signed, Ctx, global::app.View.Out)).IsSuccess();
        return wire.ToArray();
    }

    private async Task<Data> Arrived(byte[] wire)
    {
        var arrived = await Plang.Decode(wire, Ctx, global::app.View.Out);
        await arrived.IsSuccess();
        await Ctx.Variable.Set("arrived", arrived);
        return arrived;
    }

    [Test]
    public async Task AnArrivedValue_Verifies()
    {
        await Arrived(await Sent());

        await (await Program("signing", "verify", ("Data", "%arrived%"))).IsSuccess();
        await (await Program("signing", "verify", ("Data", "%arrived%"))).IsSuccess();
    }

    [Test]
    public async Task AnArrivedValue_NamesItsSigner()
    {
        await Arrived(await Sent());
        var signer = await Read("signed!signature.identity");
        await signer.IsSuccess();

        var identity = await Read("arrived!signature.identity");

        await identity.IsSuccess();
        await Assert.That((await identity.Value())?.ToString()).IsEqualTo((await signer.Value())?.ToString());
    }

    // a copy holds the same arrival: its nonce already presented, never presented again
    [Test]
    public async Task ACopyOfAnArrivedValue_Verifies()
    {
        var arrived = await Arrived(await Sent());
        await (await Program("signing", "verify", ("Data", "%arrived%"))).IsSuccess();
        await Ctx.Variable.Set("copy", arrived.Copy("copy"));

        await (await Program("signing", "verify", ("Data", "%copy%"))).IsSuccess();
    }

    [Test]
    public async Task TheSameWire_DecodedTwice_IsReplayTheSecondTime()
    {
        var wire = await Sent();
        await (await Plang.Decode(wire, Ctx, global::app.View.Out)).IsSuccess();

        var again = await Plang.Decode(wire, Ctx, global::app.View.Out);

        await Assert.That(again.Error?.Key).IsEqualTo("NonceReplay");
    }

    [Test]
    public async Task TheSameWire_DecodedAtOnce_ArrivesOnce()
    {
        var wire = await Sent();

        var reads = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Plang.Decode(wire, Ctx, global::app.View.Out))));

        await Assert.That(reads.Count(r => r.Success)).IsEqualTo(1);
        await Assert.That(reads.Where(r => !r.Success).All(r => r.Error!.Key == "NonceReplay")).IsTrue();
    }

    // the nonce is remembered while the arrival's window runs: the same wire inside it is replay; past it, expired
    [Test]
    public async Task TheNonce_IsRememberedForTheWindow()
    {
        var wire = await Sent();
        await Arrived(wire);

        await Clock(Now.AddMinutes(4));
        await Assert.That((await Plang.Decode(wire, Ctx, global::app.View.Out)).Error?.Key).IsEqualTo("NonceReplay");

        await Clock(Now.AddMinutes(6));
        await Assert.That((await Plang.Decode(wire, Ctx, global::app.View.Out)).Error?.Key).IsEqualTo("Expired");
    }

    // a signature layer planted inside a signed value, claiming another signer: refused with the outer, or the inner
    // reads as unsigned — never as signed by whom it claims
    [Test]
    public async Task ANestedLayer_PlantedInASignedValue_IsRefusedOrReadsAsUnsigned()
    {
        var planted = System.Text.Json.Nodes.JsonNode.Parse(await Sent())!;
        planted["identity"] = "someone-else";
        await Ctx.Variable.Set("outer", new Dictionary<string, object?> { ["inner"] = "placeholder" });
        var outer = await Program("signing", "sign", ("Data", "%outer%"));
        await outer.IsSuccess();
        using var stream = new System.IO.MemoryStream();
        await (await Plang.Encode(stream, outer, Ctx, global::app.View.Out)).IsSuccess();
        var node = System.Text.Json.Nodes.JsonNode.Parse(stream.ToArray())!;
        node["value"]!["value"]!["inner"] = planted;

        var arrived = await Plang.Decode(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()), Ctx, global::app.View.Out);

        if (!arrived.Success) return;
        await Ctx.Variable.Set("arrived", arrived);
        await Assert.That((await Program("signing", "verify", ("Data", "%arrived.inner%"))).Error?.Key).IsEqualTo("NoSignature");
    }
}
