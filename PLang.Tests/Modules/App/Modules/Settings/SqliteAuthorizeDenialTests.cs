using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.path.file.@this;
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
            return Task.FromResult(global::app.data.@this.Ok(_answer));
        }
    }

    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-sqlite-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    [Test] public async Task SqliteOpen_DataSourceOutsideRoot_DeniedAnswer_DoesNotOpenDb()
    {
        var app = NewApp(out _);
        app.User.Channel.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "external.sqlite");
        var dbPath = new FilePath(outOfRoot, app.User.Context);
        bool threw = false;
        try { using var _ = new global::app.module.settings.Sqlite(dbPath, app.User.Context); }
        catch (System.InvalidOperationException) { threw = true; }
        await Assert.That(threw).IsTrue();
        await Assert.That(System.IO.File.Exists(outOfRoot)).IsFalse();
    }

    [Test] public async Task SqliteOpen_DataSourceInRoot_OpensSilently()
    {
        var app = NewApp(out var root);
        var ch = new CannedChannel("UNEXPECTED");
        app.User.Channel.Register(ch);
        var dbPath = new FilePath(System.IO.Path.Combine(root, "data.sqlite"), app.User.Context);
        using var _ = new global::app.module.settings.Sqlite(dbPath, app.User.Context);
        await Assert.That(ch.AskCount).IsEqualTo(0);
    }
}
