using Path = global::app.type.item.path.file.@this;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PermissionRecord = global::app.type.item.permission.@this;
using Verb = global::app.type.item.permission.Verb;
using MatchMode = global::app.type.item.permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// Stage 2b — Batch 6: `Path.Authorize(verb)` consults the actor's permission
/// view, asks the channel on miss, signs + stores on grant, surfaces
/// PermissionDenied on refusal, recurses on bad input.
public class PathAuthorizeTests
{
    private static global::app.@this NewApp() =>
        global::PLang.Tests.TestApp.Plain(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-auth-" + System.Guid.NewGuid().ToString("N")[..8]));

    /// Stub stateful channel — answers Ask with a pre-set canned line.
    private sealed class CannedAnswerChannel : global::app.channel.@this
    {
        public string[] Answers { get; }
        private int _idx;
        public CannedAnswerChannel(string[] answers)
        {
            Name = "input";
            Direction = global::app.channel.ChannelDirection.Bidirectional;
            Answers = answers;
        }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(
            global::app.module.action.output.ask action, CancellationToken ct = default)
        {
            var ans = _idx < Answers.Length ? Answers[_idx++] : "";
            return Task.FromResult(action.Context.Ok(ans));
        }
    }

    private sealed class StatelessChannel : global::app.channel.type.message.@this
    {
        public StatelessChannel() { Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));
    }

    [Test] public async Task Authorize_GrantExists_ReturnsOk_NoChannelAsk()
    {
        var app = NewApp();
        var context = app.User.Context;
        var path = new Path("/p");

        // Pre-seed a grant covering the request.
        var grant = new PermissionRecord(app.User.Name, "/p", global::app.type.item.permission.@this.AllVerbs, MatchMode.Exact);
        var grantData = new global::app.data.@this<PermissionRecord>("", grant, context: context);
        await app.User.Permission.Add(grantData, persist: true);

        var result = await path.Authorize(global::app.type.item.permission.Verb.Read, app.User.Context);
        await result.IsSuccess();
    }

    [Test] public async Task Authorize_StatefulAnswerA_Signs_Adds_ReturnsOk()
    {
        var app = NewApp();
        app.User.Channel.Register(new CannedAnswerChannel(new[] { "a" }));
        var context = app.User.Context;
        var path = new Path("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.Read, app.User.Context);
        await result.IsSuccess();
        // Subsequent Find should hit since Add ran.
        await Assert.That(await app.User.Permission.Find(path, global::app.type.item.permission.Verb.Read)).IsNotNull();
    }

    [Test] public async Task Authorize_SamePath_ChecksTheCallersActor_NotTheCreators()
    {
        var app = NewApp();
        var path = new Path("/p");
        var grant = new PermissionRecord(app.User.Name, "/p", global::app.type.item.permission.@this.AllVerbs, MatchMode.Exact);
        await app.User.Permission.Add(new global::app.data.@this<PermissionRecord>("", grant, context: app.User.Context), persist: false);
        app.System.Channel.Register(new CannedAnswerChannel(new[] { "n" }));

        var asUser = await path.Authorize(Verb.Read, app.User.Context);
        var asSystem = await path.Authorize(Verb.Read, app.System.Context);

        await asUser.IsSuccess();
        await asSystem.IsFailure();
        await Assert.That(asSystem.Error).IsTypeOf<global::app.error.PermissionDenied>();
    }

    [Test] public async Task Authorize_StatefulAnswerY_SignsWithoutExpiry_Adds_ReturnsOk()
    {
        var app = NewApp();
        app.User.Channel.Register(new CannedAnswerChannel(new[] { "y" }));
        var context = app.User.Context;
        var path = new Path("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.Read, app.User.Context);
        await result.IsSuccess();
        await Assert.That(await app.User.Permission.Find(path, global::app.type.item.permission.Verb.Read)).IsNotNull();
    }

    [Test] public async Task Authorize_StatefulAnswerN_ReturnsFail_PermissionDenied()
    {
        var app = NewApp();
        app.User.Channel.Register(new CannedAnswerChannel(new[] { "n" }));
        var context = app.User.Context;
        var path = new Path("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.Read, app.User.Context);
        await result.IsFailure();
        await Assert.That(result.Error).IsTypeOf<global::app.error.PermissionDenied>();
    }

    /// Stub channel whose Ask fails with the given key — the stream's EOF answer ("ChannelEof") or any other.
    private sealed class FailingAskChannel(string key) : global::app.channel.@this
    {
        public FailingAskChannel() : this("ChannelEof") { }
        private readonly string _key = key;
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(
            global::app.module.action.output.ask action, CancellationToken ct = default)
            => Task.FromResult(action.Context.Error(new global::app.error.ServiceError(
                $"Channel 'input' failed ({_key})", _key, 400)));
    }

    [Test] public async Task Authorize_NobodyCanAnswer_IsPermissionDenied_WithTheChannelFailureAsCause()
    {
        var app = NewApp();
        app.User.Channel.Register(new FailingAskChannel("ChannelEof") { Name = "input", Direction = global::app.channel.ChannelDirection.Bidirectional });
        var path = new Path("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.Read, app.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error).IsTypeOf<global::app.error.PermissionDenied>()
            .Because($"{result.Error?.Key}: {result.Error?.Message}");
        await Assert.That(result.Error!.list.Single().Key).IsEqualTo("ChannelEof");
    }

    [Test] public async Task Authorize_OtherAskFailure_SurfacesAsItself()
    {
        var app = NewApp();
        app.User.Channel.Register(new FailingAskChannel("ChannelBroken") { Name = "input", Direction = global::app.channel.ChannelDirection.Bidirectional });
        var path = new Path("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.Read, app.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelBroken");
    }

    [Test] public async Task Authorize_StatefulAnswerGarbage_RecursesWithInvalidPrefix()
    {
        var app = NewApp();
        // Garbage first, then "a" — pin recursion fires and second call accepts.
        app.User.Channel.Register(new CannedAnswerChannel(new[] { "garbage", "a" }));
        var context = app.User.Context;
        var path = new Path("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.Read, app.User.Context);
        await result.IsSuccess();
    }

    [Test] public async Task Authorize_StatelessChannel_BubblesDataAskUnchanged()
    {
        var app = NewApp();
        app.User.Channel.Register(new StatelessChannel());
        var context = app.User.Context;
        var path = new Path("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.Read, app.User.Context);
        // Stateless: bubble the Exit-typed Data up so the step loop short-circuits.
        await Assert.That(result.Type?.Name).IsEqualTo("ask");
        await Assert.That(result.Snapshot).IsNotNull();
    }

    [Test] public async Task Authorize_ConstructedPermission_HasExpectedActorPathVerbMatch()
    {
        var app = NewApp();
        app.User.Channel.Register(new CannedAnswerChannel(new[] { "a" }));
        var context = app.User.Context;
        var path = new Path("/apps/Email/file.txt");
        var verb = global::app.type.item.permission.Verb.Read;

        await path.Authorize(verb, app.User.Context);
        var grant = await app.User.Permission.Find(path, verb);
        await Assert.That(grant).IsNotNull();
        await Assert.That((await grant!.Value<PermissionRecord>())!.Actor).IsEqualTo(app.User.Name);
        await Assert.That((await grant!.Value<PermissionRecord>())!.Path).IsEqualTo("/apps/Email/file.txt");
        await Assert.That((await grant!.Value<PermissionRecord>())!.Match).IsEqualTo(MatchMode.Exact);
    }

    [Test] public async Task PermissionDenied_Error_CarriesConstructedPermission()
    {
        var app = NewApp();
        app.User.Channel.Register(new CannedAnswerChannel(new[] { "n" }));
        var context = app.User.Context;
        var path = new Path("/secret");

        var result = await path.Authorize(global::app.type.item.permission.Verb.Read, app.User.Context);
        var denied = (global::app.error.PermissionDenied)result.Error!;
        await Assert.That(denied.Permission.Path).IsEqualTo("/secret");
        await Assert.That(denied.Permission.Actor).IsEqualTo(app.User.Name);
    }

    /// Regression: codeanalyzer v2 #2. On Linux the root-prefix comparison must
    /// be case-sensitive — `/SRV/myapp` must not be treated as in-root when the
    /// real root is `/srv/myapp`, or the auto-grant in IsInRoot becomes a
    /// permission-gate bypass. Linux/macOS only; Windows is intentionally
    /// case-insensitive at the FS layer.
    [Test] public async Task IsInRoot_UpperCasedRoot_TreatedAsOutOfRoot_OnUnix()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
        var app = NewApp();
        // "n" answers the prompt that IsInRoot=false forces — so we observe
        // PermissionDenied (out-of-root path → prompt → refused). Under the
        // OrdinalIgnoreCase bug, IsInRoot=true and Ok() comes back instead.
        app.User.Channel.Register(new CannedAnswerChannel(new[] { "n" }));
        var context = app.User.Context;
        var uppered = app.AbsolutePath.ToUpperInvariant() + "/file.txt";
        var path = new Path(uppered);

        var result = await path.Authorize(global::app.type.item.permission.Verb.Read, app.User.Context);
        await result.IsFailure();
        await Assert.That(result.Error).IsTypeOf<global::app.error.PermissionDenied>();
    }

    /// IsInRoot's second clause: OsDirectory (system-built-in goals like
    /// test, build) auto-grants without a prompt, even though it sits outside
    /// the actor's RootDirectory. Verifies the runtime-owned-files carve-out.
    [Test] public async Task IsInRoot_PathUnderOsDirectory_AutoGrants_NoChannelAsk()
    {
        var app = NewApp();
        // No channel registered — if Authorize tried to prompt, it would
        // throw or return non-Ok. Auto-grant means Ok with no ask.
        var context = app.User.Context;
        var osDir = app.OsAbsolutePath;
        var osPath = System.IO.Path.Combine(osDir, "system", "test", "fixture.goal");
        var path = new Path(osPath);

        var result = await path.Authorize(global::app.type.item.permission.Verb.Read, app.User.Context);
        await result.IsSuccess();
    }

    [Test] public async Task PermissionDenied_Error_RoundTripsThroughErrorShape()
    {
        var perm = new PermissionRecord("user", "/p", global::app.type.item.permission.@this.AllVerbs, MatchMode.Exact);
        var err = new global::app.error.PermissionDenied(perm);
        await Assert.That(err.Key).IsEqualTo("PermissionDenied");
        await Assert.That(err.StatusCode).IsEqualTo(403);
        await Assert.That(err.Permission).IsSameReferenceAs(perm);
    }
}
