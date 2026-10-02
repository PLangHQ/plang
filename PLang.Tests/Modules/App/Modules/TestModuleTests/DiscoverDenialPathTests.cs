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
        return new global::app.@this(root).Testing();
    }

    [Test] public async Task Discover_WithTestPathOutsideRoot_DenialNotSilentEmpty()
    {
        var app = NewApp(out _);
        app.actor.list.User.Channel.Register(new CannedChannel("n"));
        var outOfRoot = "//etc";
        var action = new global::app.module.test.discover(app.actor.list.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(
                global::app.type.item.path.@this.Resolve(outOfRoot, app.actor.list.User.Context)),
            Pattern = new global::app.data.@this<global::app.type.item.text.@this>("Pattern", "*.test.goal"),
            Subfolder = new global::app.data.@this<global::app.type.item.choice.@this<global::app.module.file.type.subfolder>>("Subfolder",
                new global::app.type.item.choice.@this<global::app.module.file.type.subfolder>(global::app.module.file.type.subfolder.skip))
        };
        var result = await action.Start();
        // Denial surfaces as Fail, not as an empty list of tests.
        await result.IsFailure();
    }

    [Test] public async Task Discover_WithDotDotTraversal_DeniedByAuthGate()
    {
        var app = NewApp(out _);
        app.actor.list.User.Channel.Register(new CannedChannel("n"));
        var action = new global::app.module.test.discover(app.actor.list.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(
                global::app.type.item.path.@this.Resolve("//../../../etc", app.actor.list.User.Context)),
            Pattern = new global::app.data.@this<global::app.type.item.text.@this>("Pattern", "*.test.goal"),
            Subfolder = new global::app.data.@this<global::app.type.item.choice.@this<global::app.module.file.type.subfolder>>("Subfolder",
                new global::app.type.item.choice.@this<global::app.module.file.type.subfolder>(global::app.module.file.type.subfolder.skip))
        };
        var result = await action.Start();
        // Either denial → Fail, or the resolved path lands under root → empty.
        // BOTH branches assert so a "zero assertions on denial" regression
        // can't slip through.
        if (result.Success)
        {
            var files = result.GetValue<List<global::app.test.@this>>();
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
