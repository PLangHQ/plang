using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.TestModuleTests;

/// <summary>
/// Batch 9. <c>test/discover.cs</c> denial-path tests.
/// </summary>
public class DiscoverDenialPathTests
{
    private sealed class CannedChannel : global::app.channel.@this
    {
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok(_answer));
    }

    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-discover-deny-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    [Test] public async Task Discover_WithTestPathOutsideRoot_DenialNotSilentEmpty()
    {
        var app = NewApp(out _);
        app.User.Channel.Register(new CannedChannel("n"));
        var outOfRoot = "//etc";
        var action = new global::app.module.test.discover
        {
            Context = app.User.Context,
            Path = global::app.data.@this<global::app.type.path.@this>.Ok(
                global::app.type.path.@this.Resolve(outOfRoot, app.User.Context)),
            Pattern = new global::app.data.@this<string>("Pattern", "*.test.goal"),
            Recursive = new global::app.data.@this<bool>("Recursive", false)
        };
        var result = await action.Run();
        // Denial surfaces as Fail, not as an empty list of tests.
        await Assert.That(result.Success).IsFalse();
    }

    [Test] public async Task Discover_WithDotDotTraversal_DeniedByAuthGate()
    {
        var app = NewApp(out _);
        app.User.Channel.Register(new CannedChannel("n"));
        var action = new global::app.module.test.discover
        {
            Context = app.User.Context,
            Path = global::app.data.@this<global::app.type.path.@this>.Ok(
                global::app.type.path.@this.Resolve("//../../../etc", app.User.Context)),
            Pattern = new global::app.data.@this<string>("Pattern", "*.test.goal"),
            Recursive = new global::app.data.@this<bool>("Recursive", false)
        };
        var result = await action.Run();
        // Either denial → Fail, or the resolved path lands under root → empty.
        // BOTH branches assert so a "zero assertions on denial" regression
        // can't slip through.
        if (result.Success)
        {
            var files = result.Value as System.Collections.Generic.List<global::app.tester.test.@this>;
            await Assert.That(files == null || files.Count == 0).IsTrue();
        }
        else
        {
            await Assert.That(result.Error).IsNotNull();
            // The denial must be a permission decision, not a runtime crash.
            await Assert.That(result.Error!.Key).IsNotEqualTo("NullReferenceException");
        }
    }
}
