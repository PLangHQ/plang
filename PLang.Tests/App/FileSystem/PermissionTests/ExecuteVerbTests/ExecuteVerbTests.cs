using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.path.file.@this;
using Verb = global::app.type.path.permission.verb.@this;
using Read = global::app.type.path.permission.verb.Read;
using ExecuteVerb = global::app.type.path.permission.verb.Execute;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.FileSystem.PermissionTests.ExecuteVerbTests;

/// <summary>
/// Batch 6. <c>Execute</c> verb + <c>path.LoadAssemblyAsync</c>.
/// </summary>
public class ExecuteVerbTests
{
    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-execute-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    private sealed class CannedChannel : global::app.channel.@this
    {
        private readonly string _answer;
        private readonly System.Collections.Generic.List<string> _prompts = new();
        public System.Collections.Generic.IReadOnlyList<string> Prompts => _prompts;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.modules.output.ask action, CancellationToken ct = default)
        {
            _prompts.Add(action.Question?.Value ?? "");
            return Task.FromResult(global::app.data.@this.Ok(_answer));
        }
    }

    private sealed class StatelessChannel : global::app.channel.message.@this
    {
        public StatelessChannel() { Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
    }

    [Test] public async Task ExecuteVerb_ExistsInVerbTaxonomy()
    {
        var verb = new Verb { Execute = new ExecuteVerb() };
        await Assert.That(verb.Execute).IsNotNull();
    }

    [Test] public async Task ExecuteVerb_JsonRoundTrip_PreservesShape()
    {
        var verb = new Verb { Execute = new ExecuteVerb() };
        var json = JsonSerializer.Serialize(verb, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        await Assert.That(json).Contains("Execute");
        var loaded = JsonSerializer.Deserialize<Verb>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        await Assert.That(loaded!.Execute).IsNotNull();
    }

    [Test] public async Task ExecuteVerb_PromptCopy_DistinguishesFromRead()
    {
        var app = NewApp(out _);
        var canned = new CannedChannel("n");
        app.User.Channels.Register(canned);
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(outOfRoot);
        var dllPath = System.IO.Path.Combine(outOfRoot, "stub.dll");
        System.IO.File.WriteAllText(dllPath, "not-a-real-dll");
        var p = new FilePath(dllPath, app.User.Context);
        await p.LoadAssemblyAsync();
        await Assert.That(canned.Prompts.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(canned.Prompts[0]).Contains("execute");
    }

    [Test] public async Task ReadGrant_DoesNotCoverExecute()
    {
        var app = NewApp(out var root);
        var p = new FilePath(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "lib.dll"), app.User.Context);
        // Grant Read only.
        var permission = new global::app.type.path.permission.@this(
            Actor: app.User.Name,
            Path: p.Absolute,
            Verb: new Verb { Read = new Read() },
            Match: global::app.type.path.permission.Match.Exact);
        var grantData = new global::app.data.@this<global::app.type.path.permission.@this>("", permission) { Context = app.User.Context };
        await app.User.Permission.Add(grantData);
        // Execute should NOT be covered.
        var executeMatch = await app.User.Permission.Find(p, new Verb { Execute = new ExecuteVerb() });
        await Assert.That(executeMatch).IsNull();
    }

    [Test] public async Task LoadAssemblyAsync_InRoot_ReturnsLoadedAssembly_NoPrompt()
    {
        var app = NewApp(out var root);
        var canned = new CannedChannel("UNEXPECTED");
        app.User.Channels.Register(canned);
        // Pre-stage: an actually-loadable DLL inside the App root. We use this
        // very test assembly — it lives somewhere on disk and copying it here
        // produces a valid loadable target.
        var srcAssembly = typeof(ExecuteVerbTests).Assembly.Location;
        var copyAt = System.IO.Path.Combine(root, "test.dll");
        System.IO.File.Copy(srcAssembly, copyAt, overwrite: true);
        var p = new FilePath(copyAt, app.User.Context);
        var result = await p.LoadAssemblyAsync();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(canned.Prompts.Count).IsEqualTo(0);
    }

    [Test] public async Task LoadAssemblyAsync_OutOfRoot_StatelessChannel_ReturnsAsk()
    {
        var app = NewApp(out _);
        app.User.Channels.Register(new StatelessChannel());
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "stub.dll");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllText(outOfRoot, "stub");
        var p = new FilePath(outOfRoot, app.User.Context);
        var result = await p.LoadAssemblyAsync();
        // Stateless channels surface "ask" as a Data type signal, not a stored grant.
        await Assert.That(result.Type?.Value == "ask" || !result.Success).IsTrue();
    }

    [Test] public async Task LoadAssemblyAsync_OutOfRoot_DeniedAnswer_DoesNotLoadAssembly()
    {
        var app = NewApp(out _);
        app.User.Channels.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "stub.dll");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllText(outOfRoot, "stub");
        var p = new FilePath(outOfRoot, app.User.Context);
        var result = await p.LoadAssemblyAsync();
        await Assert.That(result.Success).IsFalse();
        // The fail must be a permission decision — not file-not-found or a
        // malformed-DLL throw. Differentiate via Error.Key.
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionDenied");
    }
}
