using Path = global::app.type.path.file.@this;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Verb = global::app.type.path.permission.verb.@this;
using Read = global::app.type.path.permission.verb.Read;
using Write = global::app.type.path.permission.verb.Write;
using Delete = global::app.type.path.permission.verb.Delete;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// Batch 8: parametrized coverage across the v2 single-path FS
/// methods. Each method is exercised under three scenarios:
///   1. In-root path → Ok, no Ask issued.
///   2. Out-of-root + stateful channel → blocking prompt → grant stored → succeeds.
///   3. Out-of-root + stateless channel → returns Data<Ask> with Snapshot.
public class FileSystemPermissionFlowTests
{
    public static System.Collections.Generic.IEnumerable<string> SinglePathMethodNames() =>
        new[] { "ReadText", "ReadBytes", "Exists", "List", "Stat",
                "WriteText", "WriteBytes", "Append", "Mkdir", "Delete" };

    private static global::app.@this NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-fs-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new global::app.@this(root);
    }

    private sealed class CannedChannel : global::app.channel.@this
    {
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok(_answer));
    }

    private sealed class StatelessChannel : global::app.channel.type.message.@this
    {
        public StatelessChannel() { Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
    }

    // Typed verbs (Data<bool>, Data<byte[]>, Data<path>, …) widen to base Data
    // through an explicit await/cast so the test can stay shape-agnostic.
    private static async Task<global::app.data.@this> Dispatch(string method, Path path) => method switch
    {
        "ReadText"   => await path.ReadText(),
        "ReadBytes"  => await path.ReadBytes(),
        "Exists"     => await path.ExistsAsync(),
        "List"       => await path.List(),
        "Stat"       => await path.Stat(),
        "WriteText"  => await path.WriteText("hello"),
        "WriteBytes" => await path.WriteBytes(new byte[] { 1, 2, 3 }),
        "Append"     => await path.Append("more"),
        "Mkdir"      => await path.Mkdir(),
        "Delete"     => await path.Delete(),
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
        app.User.Channel.Register(new CannedChannel("UNEXPECTED"));
        PrepareForRead(root, method);
        var targetPath = method switch
        {
            "Mkdir" => System.IO.Path.Combine(root, "newdir"),
            "List"  => System.IO.Path.Combine(root, "fixture"),
            _       => System.IO.Path.Combine(root, "fixture"),
        };
        var path = new Path(targetPath, app.User.Context);

        var result = await Dispatch(method, path);
        await result.IsSuccess();
        await Assert.That(result.Type?.Name).IsNotEqualTo("ask");
    }

    [Test]
    [MethodDataSource(nameof(SinglePathMethodNames))]
    public async Task OutOfRoot_StreamChannel_BlocksAndCompletes_GrantStored(string method)
    {
        var app = NewApp(out _);
        app.User.Channel.Register(new CannedChannel("a"));

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
        await result.IsSuccess();

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
        app.User.Channel.Register(new StatelessChannel());

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
        await Assert.That(result.Type?.Name).IsEqualTo("ask");
        await Assert.That(result.Snapshot).IsNotNull();
    }
}
