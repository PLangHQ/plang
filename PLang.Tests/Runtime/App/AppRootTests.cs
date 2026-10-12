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
        public AskCountingChannel() { Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            System.Threading.Interlocked.Increment(ref AskCount);
            return Task.FromResult(global::app.data.@this.Ok("n"));
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
            await using var runner = new global::app.@this(root).Testing();
            await using var app = new global::app.@this(runner.AbsolutePath).Testing();
            var ch = new AskCountingChannel();
            app.actor.list.User.Channel.Register(ch);

            await (await new FilePath(System.IO.Path.Combine(root, "in.txt")).Touch(app.actor.list.User.Context)).IsSuccess();
            await Assert.That(ch.AskCount).IsEqualTo(0);
            await new FilePath(System.IO.Path.Combine(outside, "out.txt")).Touch(app.actor.list.User.Context);
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
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;

        var parent = await new global::app.type.item.variable.@this("!app.parent").Start(ctx);

        await Assert.That(parent.Peek() is global::app.parent.@this).IsTrue();
    }

    private sealed class Cased
    {
        public string Thing { get; set; } = "upper";
        public string thing { get; set; } = "lower";
    }

    [Test] public async Task TwoMembersDifferingOnlyByCase_AreTheErrorNamingBoth()
    {
        await using var app = new global::app.@this("/app").Testing();
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
