namespace PLang.Tests.App.Serialization;

/// <summary>
/// A signature read live off the wire is good for %!signing.setting.expiry% after it was made: the window is given at
/// birth by the read, so its Expires is Created + that window when its signer signed none.
/// </summary>
public class SignatureExpiryTests
{
    private static async Task<global::app.type.item.signature.@this> ReadBack(global::app.@this app, string value)
    {
        var ctx = app.actor.list.User.Context;
        var plang = ctx.Format("application/plang");
        var wire = (await plang.Serialize(new global::app.data.@this("x", value, context: ctx), ctx).Value())!.Clr<string>()!;
        var back = plang.Deserialize(wire, ctx);
        await back.IsSuccess();
        return back.Signature!;
    }

    [Test] public async Task LiveRead_ExpiresTheSettingsWindowAfterCreated()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-sigexp-" + Guid.NewGuid().ToString("N")[..8])).Testing();
        var ctx = app.actor.list.User.Context;

        var layer = await ReadBack(app, "first");
        await Assert.That(layer.Origin.Value).IsEqualTo(global::app.type.item.signature.Origin.Live);
        await Assert.That(layer.Expires!.Value).IsEqualTo(layer.Created.Value + TimeSpan.FromMinutes(5));

        await ctx.Setting.Set("signing.setting.expiry", ctx.Ok(new global::app.type.item.duration.@this(TimeSpan.FromMinutes(1))));
        var shorter = await ReadBack(app, "second");
        await Assert.That(shorter.Expires!.Value).IsEqualTo(shorter.Created.Value + TimeSpan.FromMinutes(1));
    }
}
