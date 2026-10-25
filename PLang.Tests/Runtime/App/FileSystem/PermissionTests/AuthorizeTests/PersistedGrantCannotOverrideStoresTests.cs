using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Grant = global::app.type.item.permission.@this;
using MatchMode = global::app.type.item.permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// Security (item-doors, L(2)): <c>&lt;root&gt;/.data/</c> is the system's alone. A grant the user's actor HOLDS —
/// even a persisted "a" one that matches the stores' folder — must not let a non-system actor through: the refusal
/// is decided before any grant is looked up, and no ask is raised.
public class PersistedGrantCannotOverrideStoresTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string file;
    private readonly global::app.@this app;
    private int asked;

    public PersistedGrantCannotOverrideStoresTests()
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sec-l2-" + System.Guid.NewGuid().ToString("N")[..8]);
        // a key the stores' folder holds — beside the settings' database, which the persist really saves into
        file = System.IO.Path.Combine(root, ".data", "secret.key");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        System.IO.File.WriteAllText(file, "SECRET-KEY");
        app = new global::app.@this(root).TestSigning();
        app.actor.list.User.Channel.Register(new Asking(app.actor.list.User.Context, () => asked++));
    }
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx; private readonly System.Action _n;
        public Asking(global::app.actor.context.@this ctx, System.Action n)
        { _ctx = ctx; _n = n; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        { _n(); return Task.FromResult(action.Context.Ok("y")); }
    }

    private async Task Persist(string path, MatchMode match)
    {
        var user = app.actor.list.User;
        var grant = new Grant(user.Name, path, Grant.AllVerbs, match, app.type.list[typeof(Grant)]);
        await (await user.Permission.Add(new global::app.data.@this<Grant>("", grant, context: user.Context), persist: true)).IsSuccess();
        // the grant is really held, so the refusal is the stores' folder's and not a missing grant's
        await Assert.That(await user.Permission.Find(match == MatchMode.glob ? file : path, global::app.type.item.permission.Verb.read)).IsNotNull();
    }

    [Test]
    [Arguments(MatchMode.glob)]
    [Arguments(MatchMode.exact)]
    public async Task APersistedGrantOnTheStoresFile_DoesNotLetTheUserRead(MatchMode match)
    {
        await Persist(match == MatchMode.glob ? System.IO.Path.Combine(root, ".data", "**") : file, match);
        var user = app.actor.list.User.Context;
        var r = await user.File(file).Read(user);
        await Assert.That(asked).IsEqualTo(0);
        await Assert.That(r.Success).IsFalse();
    }

    [Test]
    [Arguments(MatchMode.glob)]
    [Arguments(MatchMode.exact)]
    public async Task APersistedGrantOnTheStoresFile_DoesNotLetTheUserWrite(MatchMode match)
    {
        await Persist(match == MatchMode.glob ? System.IO.Path.Combine(root, ".data", "**") : file, match);
        var user = app.actor.list.User.Context;
        var r = await user.File(file).WriteText("overwritten", user);
        await Assert.That(r.Success).IsFalse();
        await Assert.That(System.IO.File.ReadAllText(file)).IsEqualTo("SECRET-KEY");
    }

    [Test]
    [Arguments(MatchMode.glob)]
    [Arguments(MatchMode.exact)]
    public async Task APersistedGrantOnTheStoresFile_DoesNotLetTheUserDelete(MatchMode match)
    {
        await Persist(match == MatchMode.glob ? System.IO.Path.Combine(root, ".data", "**") : file, match);
        var user = app.actor.list.User.Context;
        var r = await user.File(file).Delete(user);
        await Assert.That(r.Success).IsFalse();
        await Assert.That(System.IO.File.Exists(file)).IsTrue();
    }

    [Test]
    public async Task APersistedGrantOnTheBareStoresFolder_DoesNotLetTheUserList()
    {
        var folder = System.IO.Path.Combine(root, ".data");
        await Persist(folder, MatchMode.exact);
        var user = app.actor.list.User.Context;
        var r = await user.File(folder).List("*", true, user);
        await Assert.That(r.Success).IsFalse();
    }
}
