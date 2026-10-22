using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PermissionRecord = global::app.type.item.permission.@this;
using PermissionSetting = global::app.actor.permission.setting.@this;
using Verb = global::app.type.item.permission.Verb;
using MatchMode = global::app.type.item.permission.Match;

namespace PLang.Tests.Runtime.App.Settings;

/// Security (item-doors review): a step's own write to <c>%!app.actor.permission.setting.grant%</c> followed by
/// <c>save %!app.actor.permission.setting%</c> must not mint a standing grant the actor was never asked for.
public class StepWrittenGrantTests
{
    [Test]
    public async Task AStepThatSetsGrantAndSaves_DoesNotGrantItselfAnOutOfRootPath()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sec-" + System.Guid.NewGuid().ToString("N")[..8]);
        var app = new global::app.@this(root);
        app.TestIdentity();
        var user = app.actor.list.User;
        var ctx = user.Context;                       // a step's context: its Setting is a child layer of the actor's

        var outside = "/etc/plang-sec-proof";
        var forged = new PermissionRecord(user.Name, outside, PermissionRecord.AllVerbs, MatchMode.glob,
            app.type.list[typeof(PermissionRecord)]);
        var list = new global::app.type.item.list.@this<PermissionRecord>(new List<PermissionRecord> { forged },
            app.type.list[typeof(global::app.type.item.list.@this<PermissionRecord>)]);

        // set %!app.actor.permission.setting.grant% = [...]   (the setting node's own Set, as a step runs it)
        var node = await ctx.Setting.Get<PermissionSetting>();
        var set = await node.Set("grant", false, list, ctx);
        await Assert.That(set.Success).IsTrue();

        // save %!app.actor.permission.setting%   (the variable now reads with this run's value on it)
        var saved = await ctx.Setting.Get<PermissionSetting>();
        var save = await ctx.Setting.Save(saved);
        await Assert.That(save.Success).IsTrue();

        // the actor's permission now answers a grant nobody gave it
        var found = await user.Permission.Find(ctx.File(outside), Verb.write);
        await Assert.That(found).IsNull();
    }
}
