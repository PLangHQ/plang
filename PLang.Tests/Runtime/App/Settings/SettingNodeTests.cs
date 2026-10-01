namespace PLang.Tests.App.Settings;

/// <summary>
/// A setting node is both a switch and a record: read as a bool it is whether it is on, it answers enabled and
/// disabled like every setting node, a bool written onto it turns it on or off, and its members are set beside it.
/// </summary>
public class SettingNodeTests
{
    private static string Path => new global::app.callstack.setting.@this().Path;

    private static async Task<global::app.data.@this> Read(string text, global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this(text).Variable.Single().Start(ctx);

    // set %!…diff% = true, then set %!…diff.deep% = true: the node is on, and so is its member.
    [Test] public async Task ANodeSetWholeAndByItsMembers_KeepsBoth()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.System.Context;
        await ctx.Setting.Set(Path + ".diff", ctx.Ok(true));
        await ctx.Setting.Set(Path + ".diff.deep", ctx.Ok(true));

        var setting = ctx.Setting.Of<global::app.callstack.setting.@this>();
        await Assert.That(setting.Diff.Enabled.Value).IsTrue();
        await Assert.That(setting.Diff.Deep.Value).IsTrue();
    }

    // %!…diff.enabled% and %!…diff.disabled% answer, whatever the node holds.
    [Test] public async Task ANode_AnswersEnabledAndDisabled()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.System.Context;

        await Assert.That((await (await Read("%!app.callstack.setting.diff.enabled%", ctx)).Value())?.ToString()).IsEqualTo("false");
        await Assert.That((await (await Read("%!app.callstack.setting.diff.disabled%", ctx)).Value())?.ToString()).IsEqualTo("true");
        await Assert.That((await (await Read("%!app.callstack.setting.frame.enabled%", ctx)).Value())?.ToString()).IsEqualTo("true");
    }

    // A node read as a bool is whether it is on.
    [Test] public async Task ANode_ReadAsABool_IsWhetherItIsOn()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.System.Context;
        await Assert.That(await (await Read("%!app.callstack.setting.diff%", ctx)).ToBooleanAsync()).IsFalse();

        await ctx.Setting.Set(Path + ".diff", ctx.Ok(true));
        await Assert.That(await (await Read("%!app.callstack.setting.diff%", ctx)).ToBooleanAsync()).IsTrue();
    }

    // A node takes a bool or its members — a number written onto it is refused.
    [Test] public async Task ANode_RefusesAValueThatIsNoBool()
    {
        await using var app = new global::app.@this("/test").Testing();
        var applied = new global::app.callstack.setting.@this().Apply(
            new Dictionary<string, object?> { ["diff"] = 5 }, app.actor.list.System.Context);

        await Assert.That(applied.Success).IsFalse();
        await Assert.That(applied.Error!.Message).Contains("switch");
    }
}
