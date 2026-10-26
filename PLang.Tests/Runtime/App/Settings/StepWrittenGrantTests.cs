using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PermissionRecord = global::app.type.item.permission.@this;
using PermissionSetting = global::app.actor.permission.setting.@this;
using IdentitySetting = global::app.module.identity.setting.@this;
using Verb = global::app.type.item.permission.Verb;
using MatchMode = global::app.type.item.permission.Match;

namespace PLang.Tests.Runtime.App.Settings;

/// An actor's own setting (its grants, its identity) takes its values from its actor's own row alone, and only its
/// owner's code writes that row: no step writes it, saves it or removes it, and no run value reaches it. A step that
/// sets <c>%!app.actor.permission.setting.grant%</c> and saves it mints no grant the actor was never asked for.
public class StepWrittenGrantTests : System.IAsyncDisposable
{
    private const string Outside = "/etc/plang-sec-proof";
    private readonly global::app.@this app;
    private readonly global::app.actor.@this user;
    private readonly global::app.actor.context.@this ctx;   // a step's context: its Setting a child layer of the actor's

    public StepWrittenGrantTests()
    {
        app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sec-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor);
        app.TestIdentity();
        user = app.actor.list.User;
        ctx = user.Context;
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.type.item.list.@this<PermissionRecord> Forged()
        => new(new List<PermissionRecord> { new(user.Name, Outside, PermissionRecord.AllVerbs, MatchMode.glob, app.type.list[typeof(PermissionRecord)]) },
            app.type.list[typeof(global::app.type.item.list.@this<PermissionRecord>)]);

    private static async Task Denied(global::app.data.@this answer)
    {
        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("PermissionDenied");
        await Assert.That(answer.Error!.Status.Code.ToInt32()).IsEqualTo(403);
    }

    private global::app.data.@this<global::app.type.item.setting.@this> Given(global::app.type.item.setting.@this setting)
        => new("Setting", setting, context: ctx);

    // set %!app.actor.permission.setting.grant% = [...]
    [Test] public async Task TheNodesSetOfGrant_IsDenied()
    {
        var node = await ctx.Setting.Get<PermissionSetting>();
        await Denied(await node.Set("grant", false, Forged(), ctx));
        await Assert.That(await user.Permission.Find(ctx.File(Outside), Verb.write)).IsNull();
    }

    // save %!app.actor.permission.setting%, save %!identity.setting%
    [Test] public async Task SaveOfAnActorsOwnSetting_IsDenied()
    {
        await Denied(await new global::app.module.setting.Save(ctx) { Setting = Given(await ctx.Setting.Get<PermissionSetting>()) }.Start());
        await Denied(await new global::app.module.setting.Save(ctx) { Setting = Given(await ctx.Setting.Get<IdentitySetting>()) }.Start());
    }

    // remove %!identity.setting% — a step that removes the identity row loses the key
    [Test] public async Task RemoveOfAnActorsOwnSetting_IsDenied()
    {
        await Denied(await new global::app.module.setting.Remove(ctx) { Setting = Given(await ctx.Setting.Get<PermissionSetting>()) }.Start());
        await Denied(await new global::app.module.setting.Remove(ctx) { Setting = Given(await ctx.Setting.Get<IdentitySetting>()) }.Start());
    }

    // a run value at the address, written by another route than the node, is never read into the setting
    [Test] public async Task ARunValueAtTheGrantsAddress_IsNotRead()
    {
        ctx.Setting.Set("app.actor.permission.setting.grant", new global::app.data.@this("grant", Forged(), context: ctx));
        user.Setting.Set("app.actor.permission.setting.grant", new global::app.data.@this("grant", Forged(), context: ctx));

        await Assert.That((await ctx.Setting.Get<PermissionSetting>()).Grant.CountRaw).IsEqualTo(0);
        await Assert.That(await user.Permission.Find(ctx.File(Outside), Verb.write)).IsNull();
    }

    // --app.actor.permission.setting.grant=[…] on the command line answers the same denial, and writes nothing
    [Test] public async Task TheCommandLineNamingAnActorsOwnSetting_IsDenied()
    {
        var system = app.actor.list.System;
        await Denied(await global::app.start.@this.Parse(["--app.actor.permission.setting.grant=[]"]).Set(system.Setting, system.Context));
        await Denied(await global::app.start.@this.Parse(["--identity.setting.identity=[]"]).Set(system.Setting, system.Context));
    }

    // the owner's own door still writes the row: a grant given after the prompt is saved, and found
    [Test] public async Task PermissionAdd_StillSaves_AndFindAnswersIt()
    {
        // a grant is on the place as the OS names it
        var grant = PermissionRecord.Request(user, ctx.File(Outside).Location(ctx.Filesystem)!.Absolute, Verb.write, MatchMode.exact);
        await user.Permission.Add(new global::app.data.@this<PermissionRecord>("", grant, context: ctx), persist: true);

        await Assert.That(await user.Permission.Find(ctx.File(Outside), Verb.write)).IsNotNull();
        await Assert.That((await user.Setting.Get<PermissionSetting>()).Grant.CountRaw).IsEqualTo(1);
    }
}
