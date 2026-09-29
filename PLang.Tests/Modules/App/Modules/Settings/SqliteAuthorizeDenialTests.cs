using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.item.path.file.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.Settings;

/// <summary>
/// Batch 9. <c>settings/Sqlite.cs</c> — D9b take-over API.
/// </summary>
public class SqliteAuthorizeDenialTests
{
    private sealed class CannedChannel : global::app.channel.@this
    {
        public int AskCount;
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            System.Threading.Interlocked.Increment(ref AskCount);
            return Task.FromResult(action.Context.Ok(_answer));
        }
    }

    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-sqlite-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new global::app.@this(root).Testing();
    }

    [Test] public async Task SqliteOpen_DataSourceOutsideRoot_DeniedAnswer_DoesNotOpenDb()
    {
        var app = NewApp(out _);
        app.actor.list.User.Channel.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "external.sqlite");
        var dbPath = new FilePath(outOfRoot);
        using var store = new global::app.store.sqlite.@this(dbPath, () => null, app.actor.list.User.Context);
        // the first verb opens the store: an out-of-root path the actor denies fails it, and sqlite never sees it
        var read = await store.Get<global::app.type.item.@this>("t", "k");
        await Assert.That(read.Success).IsFalse();
        await Assert.That(read.Error!.Key).IsEqualTo("PermissionDenied");
        await Assert.That(System.IO.File.Exists(outOfRoot)).IsFalse();
    }

    [Test] public async Task SqliteOpen_DataSourceInRoot_OpensSilently()
    {
        var app = NewApp(out var root);
        var ch = new CannedChannel("UNEXPECTED");
        app.actor.list.User.Channel.Register(ch);
        var dbPath = new FilePath(System.IO.Path.Combine(root, "data.sqlite"));
        using var store = new global::app.store.sqlite.@this(dbPath, () => null, app.actor.list.User.Context);
        await (await store.Get<global::app.type.item.@this>("t", "k")).IsSuccess();
        await Assert.That(ch.AskCount).IsEqualTo(0);
    }
}
