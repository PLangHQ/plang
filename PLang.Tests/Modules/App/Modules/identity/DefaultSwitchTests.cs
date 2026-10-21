namespace PLang.Tests.App.Modules.identity;

// The default identity moves in one write. A write signs with the default identity, and a moment with no default
// stored would be filled by promoting another — so the old default would win over the new one. Real signing and the
// real identity provider: the test signer never asks for an identity, which hides it.
public class DefaultSwitchTests
{
    private static async Task<T> InApp<T>(Func<global::app.@this, Task<T>> then)
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-default-switch-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        try
        {
            await using var app = new global::app.@this(root);
            return await then(app);
        }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }

    private static async Task Run(global::app.actor.context.@this ctx, string action, params (string, object?)[] properties)
        => await (await Make.Action(ctx, "identity", action, properties).Start(ctx)).IsSuccess();

    private static async Task<string?> MyIdentityName(global::app.actor.context.@this ctx)
        => (await (await new global::app.type.item.variable.@this("MyIdentity.Name", ctx.App.type.list).Start(ctx)).Value())?.ToString();

    private static async Task<int> Defaults(global::app.@this app)
        => (await app.actor.list.System.Setting.Get<global::app.module.identity.setting.@this>()).Identity
            .Items(app.actor.list.System.Context).Count(row => row.Peek() is global::app.module.identity.Identity { IsDefault: true });

    [Test] public async Task SetDefault_MovesTheDefault_AndLeavesExactlyOne() => await InApp(async app =>
    {
        var ctx = app.actor.list.User.Context;
        await Assert.That(await MyIdentityName(ctx)).IsEqualTo("default");
        await Run(ctx, "create", ("Name", "TestSwitchA"));
        await Run(ctx, "create", ("Name", "TestSwitchB"));

        await Run(ctx, "setDefault", ("Name", "TestSwitchB"));

        await Assert.That(await MyIdentityName(ctx)).IsEqualTo("TestSwitchB");
        await Assert.That(await Defaults(app)).IsEqualTo(1);
        return true;
    });

    [Test] public async Task CreateAsDefault_OverAnExistingDefault_LeavesExactlyOne() => await InApp(async app =>
    {
        var ctx = app.actor.list.User.Context;
        await Assert.That(await MyIdentityName(ctx)).IsEqualTo("default");

        await Run(ctx, "create", ("Name", "TestSwitchA"), ("Default", true));

        await Assert.That(await MyIdentityName(ctx)).IsEqualTo("TestSwitchA");
        await Assert.That(await Defaults(app)).IsEqualTo(1);
        return true;
    });
}
