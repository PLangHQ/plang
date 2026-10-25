using FilePath = global::app.type.item.path.file.@this;

namespace PLang.Tests.App;

// An app's folder is its own: a path under it is in-root, the os folder never is, anything else needs a grant — no
// chain of parent apps. %!app.parent% is the link to the plang that started this one. A name is read ignoring its case,
// and two members of one type that a name matches is the program's error naming both.
public class AppRootTests
{
    private sealed class AskCountingChannel : global::app.channel.@this
    {
        public int AskCount;
        private readonly global::app.actor.context.@this _ctx;
        public AskCountingChannel(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            System.Threading.Interlocked.Increment(ref AskCount);
            return Task.FromResult(_ctx.Ok("n"));
        }
    }

    // a test's app is rooted at its runner's folder (test.list makes it so): a file there is read without asking; a
    // file outside it is asked for
    [Test] public async Task AnAppRootedAtTheRunnersFolder_TreatsItAsInRoot()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-approot-" + System.Guid.NewGuid().ToString("N")[..8]);
        var outside = root + "-outside";
        System.IO.Directory.CreateDirectory(root);
        System.IO.Directory.CreateDirectory(outside);
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "in.txt"), "in");
        System.IO.File.WriteAllText(System.IO.Path.Combine(outside, "out.txt"), "out");
        try
        {
            await using var runner = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
            await using var app = new global::app.@this(runner.filesystem.root.Absolute, global::PLang.Tests.Parent.Actor).Testing();
            var ch = new AskCountingChannel(app.actor.list.User.Context);
            app.actor.list.User.Channel.Register(ch);

            await (await app.actor.list.User.Context.File(System.IO.Path.Combine(root, "in.txt")).Touch(app.actor.list.User.Context)).IsSuccess();
            await Assert.That(ch.AskCount).IsEqualTo(0);
            await app.actor.list.User.Context.File(System.IO.Path.Combine(outside, "out.txt")).Touch(app.actor.list.User.Context);
            await Assert.That(ch.AskCount).IsGreaterThan(0);
        }
        finally
        {
            System.IO.Directory.Delete(root, true);
            System.IO.Directory.Delete(outside, true);
        }
    }

    [Test] public async Task TheAppsParent_IsTheLink()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;

        var parent = await new global::app.type.item.variable.@this("!app.parent", ctx.App.type.list).Start(ctx);

        await Assert.That(parent.Peek() is global::app.parent.@this).IsTrue();
    }

    private sealed class CasedItem : global::app.type.item.@this
    {
        [global::app.LlmBuilder] public string Thing { get; set; } = "upper";
        [global::app.LlmBuilder] public string thing { get; set; } = "lower";
    }

    // a write onto a plang value finds its member the way a read does: two members differing only by case is the error
    // naming both, under any spelling
    [Test] public async Task WritingAMemberTwoMembersMatch_IsTheErrorNamingBoth()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        var item = new CasedItem();

        foreach (var spelling in new[] { "thing", "Thing", "THING" })
        {
            var refused = await Assert.That(async () => await item.Set(spelling, false, "x", ctx)).Throws<global::app.error.AppException>();
            await Assert.That(refused!.Error.Message).Contains("has two members named");
            await Assert.That(refused.Error.Message).Contains("`Thing`");
            await Assert.That(refused.Error.Message).Contains("`thing`");
        }
    }

    private sealed class CasedList : global::app.type.item.list.@this
    {
        public CasedList(global::app.type.@this type) : base(type) { }
        [global::app.LlmBuilder] public global::app.type.item.number.@this Total => 1;
        [global::app.LlmBuilder] public global::app.type.item.number.@this total => 2;
    }

    // a value's own marked surface (a list's count, first) is found the same way: two marked members a name matches is
    // the error naming both, under any spelling
    [Test] public async Task TwoMarkedMembersDifferingOnlyByCase_AreTheErrorNamingBoth()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        var held = new global::app.data.@this("rows", new CasedList(app.type.list["list"]), context: ctx);

        foreach (var spelling in new[] { "total", "Total", "TOTAL" })
        {
            var refused = await Assert.That(async () => await held.Get(spelling)).Throws<global::app.error.AppException>();
            await Assert.That(refused!.Error.Message).Contains("has two members named");
            await Assert.That(refused.Error.Message).Contains("`Total`");
            await Assert.That(refused.Error.Message).Contains("`total`");
        }
    }

    private sealed class Cased
    {
        public string Thing { get; set; } = "upper";
        public string thing { get; set; } = "lower";
    }

    [Test] public async Task TwoMembersDifferingOnlyByCase_AreTheErrorNamingBoth()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        var held = new global::app.data.@this("cased", new global::app.type.clr.@this(new Cased(), ctx), context: ctx);

        foreach (var spelling in new[] { "thing", "Thing", "THING" })
        {
            var refused = await Assert.That(async () => await held.Get(spelling)).Throws<global::app.error.AppException>();
            await Assert.That(refused!.Error.Message).Contains("has two members named");
            await Assert.That(refused.Error.Message).Contains("`Thing`");
            await Assert.That(refused.Error.Message).Contains("`thing`");
        }
    }
}
