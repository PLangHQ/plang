using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Path = global::App.FileSystem.Path;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using Read = global::App.FileSystem.Permission.Verb.Read;
using Write = global::App.FileSystem.Permission.Verb.Write;
using Delete = global::App.FileSystem.Permission.Verb.Delete;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// Stage 4 — Batch 8: parametrized coverage across the v2 single-path FS
/// methods. Each method is exercised under three scenarios:
///   1. In-root path → Ok, no Ask issued.
///   2. Out-of-root + stateful channel → blocking prompt → grant stored → succeeds.
///   3. Out-of-root + stateless channel → returns Data<Ask> with Snapshot.
public class FileSystemPermissionFlowTests
{
    public static System.Collections.Generic.IEnumerable<string> SinglePathMethodNames() =>
        new[] { "ReadText", "ReadBytes", "Exists", "List", "Stat",
                "WriteText", "WriteBytes", "Append", "Mkdir", "Delete" };

    private static global::App.@this NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-fs-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new global::App.@this(root);
    }

    private sealed class CannedChannel : global::App.Channels.Channel.@this
    {
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::App.Channels.Channel.ChannelDirection.Bidirectional; }
        public override Task<global::App.Data.@this> WriteCore(global::App.Data.@this data, CancellationToken ct = default) => Task.FromResult(global::App.Data.@this.Ok());
        public override Task<global::App.Data.@this> ReadCore(CancellationToken ct = default) => Task.FromResult(global::App.Data.@this.Ok((object?)null));
        public override Task<global::App.Data.@this> AskCore(global::App.modules.output.ask action, CancellationToken ct = default) => Task.FromResult(global::App.Data.@this.Ok(_answer));
    }

    private sealed class StatelessChannel : global::App.Channels.Channel.Message.@this
    {
        public StatelessChannel() { Name = "input"; Direction = global::App.Channels.Channel.ChannelDirection.Bidirectional; }
        public override Task<global::App.Data.@this> WriteCore(global::App.Data.@this data, CancellationToken ct = default) => Task.FromResult(global::App.Data.@this.Ok());
        public override Task<global::App.Data.@this> ReadCore(CancellationToken ct = default) => Task.FromResult(global::App.Data.@this.Ok((object?)null));
    }

    private static Task<global::App.Data.@this> Dispatch(string method, Path path) => method switch
    {
        "ReadText"   => path.ReadText(),
        "ReadBytes"  => path.ReadBytes(),
        "Exists"     => path.ExistsAsync(),
        "List"       => path.List(),
        "Stat"       => path.Stat(),
        "WriteText"  => path.WriteText("hello"),
        "WriteBytes" => path.WriteBytes(new byte[] { 1, 2, 3 }),
        "Append"     => path.Append("more"),
        "Mkdir"      => path.Mkdir(),
        "Delete"     => path.Delete(),
        _            => throw new System.ArgumentException($"unknown method {method}"),
    };

    private static void PrepareForRead(string root, string method)
    {
        var p = System.IO.Path.Combine(root, "fixture");
        switch (method)
        {
            case "ReadText":
            case "ReadBytes":
            case "Append":
            case "Stat":
            case "Exists":
                System.IO.File.WriteAllText(p, "hello");
                break;
            case "List":
                if (!System.IO.Directory.Exists(p)) System.IO.Directory.CreateDirectory(p);
                break;
            case "Delete":
                System.IO.File.WriteAllText(p, "to-delete");
                break;
        }
    }

    [Test]
    [MethodDataSource(nameof(SinglePathMethodNames))]
    public async Task InRootPath_ReturnsOk_NoAskIssued(string method)
    {
        var app = NewApp(out var root);
        app.User.Channels.Register(new CannedChannel("UNEXPECTED"));
        PrepareForRead(root, method);
        var targetPath = method switch
        {
            "Mkdir" => System.IO.Path.Combine(root, "newdir"),
            "List"  => System.IO.Path.Combine(root, "fixture"),
            _       => System.IO.Path.Combine(root, "fixture"),
        };
        var path = new Path(targetPath, app.User.Context);

        var result = await Dispatch(method, path);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Type?.Value).IsNotEqualTo("ask");
    }

    [Test]
    [MethodDataSource(nameof(SinglePathMethodNames))]
    public async Task OutOfRoot_StreamChannel_BlocksAndCompletes_GrantStored(string method)
    {
        var app = NewApp(out _);
        app.User.Channels.Register(new CannedChannel("a"));

        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(outOfRoot);
        PrepareForRead(outOfRoot, method);
        var targetPath = method switch
        {
            "Mkdir" => System.IO.Path.Combine(outOfRoot, "newdir"),
            "List"  => outOfRoot,
            _       => System.IO.Path.Combine(outOfRoot, "fixture"),
        };
        var path = new Path(targetPath, app.User.Context);

        var result = await Dispatch(method, path);
        await Assert.That(result.Success).IsTrue();

        var verb = method switch
        {
            "WriteText" or "WriteBytes" or "Append" or "Mkdir" => new Verb { Write = new Write() },
            "Delete" => new Verb { Delete = new Delete() },
            _ => new Verb { Read = new Read() },
        };
        await Assert.That(await app.User.Permission.Find(path, verb)).IsNotNull();
    }

    [Test]
    [MethodDataSource(nameof(SinglePathMethodNames))]
    public async Task OutOfRoot_MessageChannel_ReturnsDataAsk_WithSnapshot(string method)
    {
        var app = NewApp(out _);
        app.User.Channels.Register(new StatelessChannel());

        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(outOfRoot);
        PrepareForRead(outOfRoot, method);
        var targetPath = method switch
        {
            "Mkdir" => System.IO.Path.Combine(outOfRoot, "newdir"),
            "List"  => outOfRoot,
            _       => System.IO.Path.Combine(outOfRoot, "fixture"),
        };
        var path = new Path(targetPath, app.User.Context);

        var result = await Dispatch(method, path);
        await Assert.That(result.Type?.Value).IsEqualTo("ask");
        await Assert.That(result.Snapshot).IsNotNull();
    }
}
