using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using IdentityRecord = global::app.module.identity.Identity;

namespace PLang.Tests.Runtime.App.IdentityLeak;

/// documentation test: pins what a yes to the export ask means — the step gets the key as text it can send anywhere
/// (decision 310a). Green by design: it documents the accepted consent semantics, it is not a hole. A yes hands the
/// step the private key; copied into a plain text its [Sensitive] tag is gone, and the step may write it to a file or
/// post it to a url. The protection after a yes is the ask's wording and its scope (per call, never kept), not the tag.
public class ConsentedExportMeansTheStepHoldsTheKeyTests : System.IAsyncDisposable
{
    // TestSigning's key mock hands every generated pair this private key
    private const string Key = "test-private-key";

    private readonly string root;
    private readonly global::app.@this app;

    public ConsentedExportMeansTheStepHoldsTheKeyTests()
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sec-yes-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        app = new global::app.@this(root).TestSigning();
    }
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this User => app.actor.list.User.Context;

    private async Task<global::app.data.@this> Run(string formal) => await (await User.Action(formal)).Start(User);

    // The actor said yes to the export ask: the step is handed the key, and the ask was raised for it.
    private async Task<(string text, Asking asked)> ExportedAsText()
    {
        var asked = new Asking(User, "y");
        app.actor.list.User.Channel.Register(asked);
        var got = await Run("identity.export()");
        await Assert.That(got.Success).IsTrue();
        // the step copies the key into a plain text: no [Sensitive] member behind it any more
        var text = ((IdentityRecord)(await got.Value())!).PrivateKey;
        await User.Variable.Set("t", global::app.type.item.@this.Create(text, User));
        return (text, asked);
    }

    [Test]
    public async Task AYes_HandsTheStepTheKey_AfterAskingOnce()
    {
        var (text, asked) = await ExportedAsText();
        await Assert.That(text).IsEqualTo(Key);
        await Assert.That(asked.Count).IsEqualTo(1);
    }

    // the key as text goes into a file a step saves
    [Test]
    public async Task TheKeyAsText_SavedToATxtFile_IsCarried()
    {
        await ExportedAsText();
        var saved = await Run("file.save(Path=\"/out.txt\", Value=%t%)");
        await Assert.That(saved.Success).IsTrue();
        await Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(root, "out.txt")).Contains(Key)).IsTrue();
    }

    // the key as text goes into the body of a request a step sends (a url the actor has been granted)
    [Test]
    public async Task TheKeyAsText_PostedToAUrl_IsCarried()
    {
        using var server = new PLang.Tests.App.Types.PathTests.Http.HttpTestServer();
        await ExportedAsText();
        var url = server.NewResourceUrl();
        var grant = new global::app.type.item.permission.@this("User", User.Url(url).Absolute,
            global::app.type.item.permission.@this.AllVerbs, global::app.type.item.permission.Match.exact,
            app.type.list[typeof(global::app.type.item.permission.@this)]);
        await app.actor.list.User.Permission.Add(
            new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: User), persist: false);

        var held = await (await User.Variable.Get("t")).Value();
        var write = await User.Url(url).WriteText(held!.ToString()!, User);
        await Assert.That(write.Success).IsTrue();
        var read = await User.Url(url).Touch(User);
        await Assert.That((await read.Value())?.ToString()!.Contains(Key)).IsTrue();
    }

    // the answer is per call: a second export is asked again, nothing is kept
    [Test]
    public async Task AYes_IsPerCall_ASecondExportAsksAgain()
    {
        var (_, asked) = await ExportedAsText();
        await Run("identity.export()");
        await Assert.That(asked.Count).IsEqualTo(2);
    }

    // An input that answers whatever it is asked with one answer, counting the asks.
    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        private readonly string _answer;
        public int Count { get; private set; }
        public Asking(global::app.actor.context.@this ctx, string answer) { _ctx = ctx; _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Count++;
            return Task.FromResult(action.Context.Ok(_answer));
        }
    }
}
