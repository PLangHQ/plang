using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.signing;

/// <summary>
/// Security re-check of X3 (item-doors): a nonce is recorded only after the hash and the signature pass, so a forged or
/// tampered copy of a victim's wire cannot use up the nonce before the genuine wire arrives.
/// </summary>
public class ArrivedNonceNotBurnedTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Before(Test)]
    public async Task Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_burn_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir, global::PLang.Tests.Parent.Actor);
        _app.TestIdentity();
        await Ctx.Variable.Set("NowUtc", new global::app.type.item.datetime.@this(Now, _app.type.list[typeof(global::app.type.item.datetime.@this)]));
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true);
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;
    private global::app.type.kind.@this Plang => _app.type.list.Mime("application/plang");

    private async Task<byte[]> Sent()
    {
        await Ctx.Variable.Set("order", new Dictionary<string, object?> { ["total"] = 5L });
        var signed = await global::PLang.Tests.Shared.Make.Action(Ctx, "signing", "sign", ("Data", "%order%")).Start(Ctx);
        await signed.IsSuccess();
        using var wire = new System.IO.MemoryStream();
        await (await Plang.Encode(wire, signed, Ctx, global::app.View.Out)).IsSuccess();
        return wire.ToArray();
    }

    // flips one character of the quoted value that follows `key`; fails the test if the key isn't on the wire
    private static byte[] Tampered(byte[] wire, string key)
    {
        var text = System.Text.Encoding.UTF8.GetString(wire);
        var at = text.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
        if (at < 0) throw new InvalidOperationException($"no \"{key}\" on the wire: {text}");
        var colon = text.IndexOf(':', at);
        var start = text.IndexOf('"', colon) + 1;
        var chars = text.ToCharArray();
        chars[start] = chars[start] == 'A' ? 'B' : 'A';
        return System.Text.Encoding.UTF8.GetBytes(new string(chars));
    }

    [Test]
    public async Task ATamperedSignature_DoesNotBurnTheNonce_TheGenuineWireStillArrives()
    {
        var wire = await Sent();

        var forged = await Plang.Decode(Tampered(wire, "signature"), Ctx, global::app.View.Out);
        await Assert.That(forged.Success).IsFalse();

        await (await Plang.Decode(wire, Ctx, global::app.View.Out)).IsSuccess();
    }

    [Test]
    public async Task ATamperedHash_DoesNotBurnTheNonce_TheGenuineWireStillArrives()
    {
        var wire = await Sent();

        var forged = await Plang.Decode(Tampered(wire, "value"), Ctx, global::app.View.Out);
        await Assert.That(forged.Success).IsFalse();

        await (await Plang.Decode(wire, Ctx, global::app.View.Out)).IsSuccess();
    }
}
