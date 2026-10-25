namespace PLang.Tests.App.Serialization;

// A Data's digest covers its kept members, so it must not hang on the order they were added in: the same members,
// added in either order, digest the same (else a signed Data with kept members fails its verify for nothing).
public class KeptDigestOrderTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task TheSameKeptMembers_AddedInOppositeOrder_DigestTheSame()
    {
        var ctx = app.actor.list.User.Context;
        var names = Enumerable.Range(0, 100).Select(i => "member" + i).ToList();
        var forward = new global::app.data.@this("x", "value", context: ctx);
        var backward = new global::app.data.@this("x", "value", context: ctx);
        foreach (var name in names) forward.Kept.Add(name, new global::app.data.@this(name, name, context: ctx));
        foreach (var name in Enumerable.Reverse(names)) backward.Kept.Add(name, new global::app.data.@this(name, name, context: ctx));

        var kind = (await ctx.Setting.Get<global::app.module.signing.setting.@this>()).Hash.Value;
        var one = await forward.Digest(global::app.View.Out, kind, ctx);
        var two = await backward.Digest(global::app.View.Out, kind, ctx);

        await Assert.That(one.DigestEquals(two)).IsTrue();
    }
}
