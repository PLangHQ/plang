using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Grant = global::app.type.item.permission.@this;
using MatchMode = global::app.type.item.permission.Match;
using Verb = global::app.type.item.permission.Verb;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// What the actor says yes to is kept as it really was: a one-time yes sits beside what its role stands on, never in
/// its place, and a revoke never reaches a standing grant; an "always" whose save failed answers that failure, never
/// a yes.
public class GrantKeepTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly global::app.@this app;

    public GrantKeepTests()
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-grantkeep-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        app = new global::app.@this(root).TestSigning();
    }
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.@this User => app.actor.list.User;
    private string Os => app.filesystem.os.Absolute;

    private global::app.data.@this OnTheOsFolder(params Verb[] verbs)
        => new global::app.data.@this<Grant>("", new Grant(User.Name, Os, new HashSet<Verb>(verbs), MatchMode.exact, app.type.list[typeof(Grant)]), context: User.Context);

    // the user's role stands read and execute on the os folder; a "y" to write there keeps them
    [Test]
    public async Task AOneTimeYes_KeepsTheStandingGrant()
    {
        await (await User.Permission.Add(OnTheOsFolder(Verb.write), persist: false)).IsSuccess();

        await Assert.That(await User.Permission.Find(app.filesystem.os, Verb.write)).IsNotNull().Because("the yes is kept");
        await Assert.That(await User.Permission.Find(app.filesystem.os, Verb.read)).IsNotNull().Because("the standing read stays");
        await Assert.That(await User.Permission.Find(app.filesystem.os, Verb.execute)).IsNotNull().Because("the standing execute stays");
    }

    [Test]
    public async Task ARevoke_LeavesTheStandingGrant()
    {
        await User.Permission.Add(OnTheOsFolder(Verb.write), persist: false);
        await User.Permission.Revoke(new Grant(User.Name, Os, new HashSet<Verb> { Verb.write }, MatchMode.exact, app.type.list[typeof(Grant)]));

        await Assert.That(await User.Permission.Find(app.filesystem.os, Verb.write)).IsNull().Because("the yes is revoked");
        await Assert.That(await User.Permission.Find(app.filesystem.os, Verb.read)).IsNotNull().Because("the role's grant is no revoke's");
    }

    // a data that holds no grant is the caller's bug: adding nothing is no yes
    [Test]
    public async Task AddingNoGrant_IsAnError()
    {
        var kept = await User.Permission.Add(new global::app.data.@this("", (object?)null, context: User.Context), persist: false);
        await kept.IsFailure();
    }

    // the settings' database can't be written (it is no database): an "always" answers that, never Ok
    [Test]
    public async Task AFailedAlwaysSave_AnswersItsError()
    {
        await using var broken = Broken();
        var user = broken.actor.list.User;
        var grant = new Grant(user.Name, "/elsewhere", Grant.AllVerbs, MatchMode.exact, broken.type.list[typeof(Grant)]);

        var kept = await user.Permission.Add(new global::app.data.@this<Grant>("", grant, context: user.Context), persist: true);

        await kept.IsFailure();
    }

    // an out-of-root read answered "a" where the always can't be saved: the read is that failure, not a yes
    [Test]
    public async Task AnAlwaysAnswer_WhoseSaveFailed_IsNotAYes()
    {
        var outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-grantkeep-out-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        System.IO.File.WriteAllText(System.IO.Path.Combine(outside, "x.txt"), "outside");
        await using var broken = Broken();
        broken.actor.list.User.Channel.Register(new Answering(broken.actor.list.User.Channel, broken.actor.list.User.Context, "a"));
        var user = broken.actor.list.User.Context;

        var read = await user.File(System.IO.Path.Combine(outside, "x.txt")).Read(user);

        await read.IsFailure();
    }

    // an app whose settings' database is a file of text
    private global::app.@this Broken()
    {
        var at = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-grantkeep-broken-" + System.Guid.NewGuid().ToString("N")[..8]);
        var file = System.IO.Path.Combine(at, ".data", "setting", "data.sqlite");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        System.IO.File.WriteAllText(file, "not a database");
        return new global::app.@this(at).TestSigning();
    }

    private sealed class Answering : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx; private readonly string _answer;
        public Answering(global::app.channel.list.@this channels, global::app.actor.context.@this ctx, string answer) : base(channels)
        { _ctx = ctx; _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
            => Task.FromResult(action.Context.Ok(_answer));
    }
}
