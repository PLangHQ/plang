namespace PLang.Tests.App.Modules.signing;

/// <summary>
/// A hash holds its kind. A signature binds its value with the kind the signing setting names
/// (<c>%!signing.setting.hash%</c>, keccak256) and carries it, so it is checked with the kind it was made with; a
/// signature whose hash names no kind there is is refused, never read as a guessed one. Real signing — no mock.
/// </summary>
public class SigningHashKindTests
{
    private static async Task<(global::app.@this app, string root)> Signing()
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_hashkind_" + Guid.NewGuid().ToString("N"))).FullName;
        var app = new global::app.@this(root);
        app.test.list.Open();
        app.TestIdentity();
        await Task.CompletedTask;
        return (app, root);
    }

    private static async Task<byte[]> Written(global::app.@this app, global::app.actor.context.@this ctx, string value)
    {
        using var wire = new MemoryStream();
        await (await app.type.list["wire"].kind["plang"]!.Encode(wire, new global::app.data.@this("v", value, context: ctx), ctx)).IsSuccess();
        return wire.ToArray();
    }

    [Test]
    public async Task ASignature_BindsWithTheSettingsKind_AndIsCheckedWithIt()
    {
        var (app, root) = await Signing();
        try
        {
            var ctx = app.actor.list.User.Context;
            await (await new global::app.type.item.variable.parser.@this("%!signing.setting.hash%").Variable.Single()
                .Set(ctx.Ok(new global::app.type.item.text.@this("sha256")), ctx)).IsSuccess();

            var wire = await Written(app, ctx, "signed with sha256");
            var back = await app.type.list["wire"].kind["plang"]!.Decode(wire, ctx);

            await Assert.That(System.Text.Encoding.UTF8.GetString(wire)).Contains("\"hash\":{\"type\":\"sha256\"");
            await back.IsSuccess();
            await Assert.That((await back.Value())?.ToString()).IsEqualTo("signed with sha256");
        }
        finally { await app.DisposeAsync(); System.IO.Directory.Delete(root, true); }
    }

    [Test]
    public async Task ASignatureWhoseHashIsNoKind_IsRefused_NamingIt()
    {
        var (app, root) = await Signing();
        try
        {
            var ctx = app.actor.list.User.Context;
            var written = System.Text.Encoding.UTF8.GetString(await Written(app, ctx, "x"))
                .Replace("\"hash\":{\"type\":\"keccak256\"", "\"hash\":{\"type\":\"md5\"");

            var back = await app.type.list["wire"].kind["plang"]!.Decode(System.Text.Encoding.UTF8.GetBytes(written), ctx);

            await back.IsFailure();
            await Assert.That(back.Error!.Key).IsEqualTo("SignatureInvalid");
            await Assert.That(back.Error.Message).Contains("md5");
        }
        finally { await app.DisposeAsync(); System.IO.Directory.Delete(root, true); }
    }

    // crypto.verify takes the bound hash's own kind: a sha256 hash value verifies with no Algorithm given
    [Test]
    public async Task CryptoVerify_OfASha256Hash_UsesItsOwnKind()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var hashed = await ctx.Action("crypto.hash(Data=\"hello\", Algorithm=\"sha256\")").Start(ctx);
        await hashed.IsSuccess();

        var verify = new global::app.module.crypto.Verify(ctx) { Data = ctx.Ok("hello"), Hash = hashed };
        await verify.Attach(null, ctx);
        var verified = await verify.Start();

        await verified.IsSuccess();
        await Assert.That((await verified.Value())!.Value).IsTrue();
    }
}
