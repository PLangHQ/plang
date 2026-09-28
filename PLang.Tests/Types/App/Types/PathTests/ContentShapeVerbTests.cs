using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.item.path.file.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// A file's raw content: <c>path.Read</c> lands the reference, and its <see cref="global::app.type.item.IContent"/>
/// hands the bytes — through the same Read gate as every other read.
/// </summary>
public class ContentShapeVerbTests
{
    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-csv-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return TestApp.Create(root);
    }

    private static async Task<global::app.data.@this> Content(FilePath p, global::app.actor.context.@this context)
        => await (await p.Read(context)).Use<global::app.type.item.IContent>(async file => await file.Content(context));

    private sealed class CannedChannel : global::app.channel.@this
    {
        private readonly string _answer;
        private readonly System.Collections.Generic.List<string> _prompts = new();
        public System.Collections.Generic.IReadOnlyList<string> Prompts => _prompts;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.action.output.ask action, CancellationToken ct = default)
        {
            _prompts.Add((action.Question.Peek()?.ToString()) ?? "");
            return Task.FromResult(action.Context.Ok(_answer));
        }
    }

    [Test] public async Task Content_InRoot_IsTheFilesBytes()
    {
        var app = NewApp(out var root);
        var file = System.IO.Path.Combine(root, "data.bin");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        System.IO.File.WriteAllBytes(file, bytes);
        var result = await Content(new FilePath(file), app.User.Context);
        await result.IsSuccess();
        await Assert.That((result.Peek() as global::app.type.item.binary.@this)!.Value).IsEquivalentTo(bytes);
    }

    [Test] public async Task Content_OutOfRoot_DeniedAnswer_DoesNotReadFile()
    {
        var app = NewApp(out _);
        app.User.Channel.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "secret.bin");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllBytes(outOfRoot, new byte[] { 42, 43 });
        var result = await Content(new FilePath(outOfRoot), app.User.Context);
        await result.IsFailure();
        // Differentiate denial from file-not-found / other IO errors.
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionDenied");
    }

    [Test] public async Task Content_GatesUnderReadVerb_NotWriteOrExecute()
    {
        var app = NewApp(out _);
        var canned = new CannedChannel("n");
        app.User.Channel.Register(canned);
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "data.bin");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllBytes(outOfRoot, new byte[] { 1 });
        await Content(new FilePath(outOfRoot), app.User.Context);
        await Assert.That(canned.Prompts.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(canned.Prompts[0]).Contains("read");
    }

    [Test] public async Task Content_OfAFolder_IsNotA_Content()
    {
        var app = NewApp(out var root);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "sub"));
        var result = await Content(new FilePath(System.IO.Path.Combine(root, "sub")), app.User.Context);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotA");
    }
}
