using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.path.file.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// <summary>
/// Batch 10. In-root silent fast-path regression guard.
/// </summary>
public class InRootSilentFastPathTests
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
            return Task.FromResult(global::app.data.@this.Ok("y"));
        }
    }

    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-inroot-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    [Test] public async Task InRootRead_DoesNotInvokeOutputAsk()
    {
        var app = NewApp(out var root);
        var ch = new AskCountingChannel();
        app.User.Channel.Register(ch);
        var file = System.IO.Path.Combine(root, "f.txt");
        System.IO.File.WriteAllText(file, "hello");
        var p = new FilePath(file, app.User.Context);
        var r = await p.ReadText();
        await Assert.That(r.Success).IsTrue();
        await Assert.That(ch.AskCount).IsEqualTo(0);
    }

    [Test] public async Task InRootWrite_DoesNotInvokeOutputAsk()
    {
        var app = NewApp(out var root);
        var ch = new AskCountingChannel();
        app.User.Channel.Register(ch);
        var file = System.IO.Path.Combine(root, "w.txt");
        var p = new FilePath(file, app.User.Context);
        var r = await p.WriteText("hello");
        await Assert.That(r.Success).IsTrue();
        await Assert.That(ch.AskCount).IsEqualTo(0);
    }

    [Test] public async Task InRootListThenReadEach_BatchOfTen_ZeroAskInvocations()
    {
        var app = NewApp(out var root);
        var ch = new AskCountingChannel();
        app.User.Channel.Register(ch);
        for (int i = 0; i < 10; i++)
            System.IO.File.WriteAllText(System.IO.Path.Combine(root, $"f{i}.txt"), $"f{i}");
        var dir = new FilePath(root, app.User.Context);
        var listed = await dir.List("*.txt", recursive: false);
        await Assert.That(listed.Success).IsTrue();
        foreach (var f in listed.Value!)
            await f.ReadText();
        await Assert.That(ch.AskCount).IsEqualTo(0);
    }

    [Test] public async Task InRootLoadAssembly_DoesNotInvokeOutputAsk()
    {
        var app = NewApp(out var root);
        var ch = new AskCountingChannel();
        app.User.Channel.Register(ch);
        var srcAssembly = typeof(InRootSilentFastPathTests).Assembly.Location;
        var copyAt = System.IO.Path.Combine(root, "test.dll");
        System.IO.File.Copy(srcAssembly, copyAt, overwrite: true);
        var p = new FilePath(copyAt, app.User.Context);
        var r = await p.LoadAssemblyAsync();
        await Assert.That(r.Success).IsTrue();
        await Assert.That(ch.AskCount).IsEqualTo(0);
    }
}
