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
        global::PLang.Tests.Session.App(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-auth-" + System.Guid.NewGuid().ToString("N")[..8])).TestSigning();

    /// Stub stateful channel — answers Ask with a pre-set canned line.
    private sealed class CannedAnswerChannel : global::app.channel.@this
    {
        public string[] Answers { get; }
        private int _idx;
        private readonly global::app.actor.context.@this _ctx;
        public CannedAnswerChannel(string[] answers, global::app.actor.context.@this ctx)
        {
            Name = "input";
            Direction = global::app.channel.ChannelDirection.Bidirectional;
            Answers = answers;
            _ctx = ctx;
        }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(
            global::app.module.output.ask action, CancellationToken ct = default)
        {
            var ans = _idx < Answers.Length ? Answers[_idx++] : "";
            return Task.FromResult(action.Context.Ok(ans));
        }
    }

    private sealed class StatelessChannel : global::app.channel.type.message.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public StatelessChannel(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(_ctx.Ok((object?)null));
    }

    [Test] public async Task Authorize_GrantExists_ReturnsOk_NoChannelAsk()
    {
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var path = app.actor.list.User.Context.File("/p");

        // Pre-seed a grant covering the request.
        var grant = new PermissionRecord(app.actor.list.User.Name, "/p", global::app.type.item.permission.@this.AllVerbs, MatchMode.exact, app.type.list[typeof(PermissionRecord)]);
        var grantData =new global::app.data.@this<PermissionRecord>("", grant, context: context);
        await app.actor.list.User.Permission.Add(grantData, persist: true);

        var result = await path.Authorize(global::app.type.item.permission.Verb.read, app.actor.list.User.Context);
        await result.IsSuccess();
    }

    [Test] public async Task Authorize_StatefulAnswerA_Signs_Adds_ReturnsOk()
    {
        var app = NewApp();
        app.actor.list.User.Channel.Register(new CannedAnswerChannel(new[] { "a" }, app.actor.list.User.Context));
        var context = app.actor.list.User.Context;
        var path = app.actor.list.User.Context.File("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.read, app.actor.list.User.Context);
        await result.IsSuccess();
        // Subsequent Find should hit since Add ran.
        await Assert.That(await app.actor.list.User.Permission.Find(path, global::app.type.item.permission.Verb.read)).IsNotNull();
    }

    [Test] public async Task Authorize_SamePath_ChecksTheCallersActor_NotTheCreators()
    {
        var app = NewApp();
        var path = app.actor.list.User.Context.File("/p");
        var grant = new PermissionRecord(app.actor.list.User.Name, "/p", global::app.type.item.permission.@this.AllVerbs, MatchMode.exact, app.type.list[typeof(PermissionRecord)]);
        await app.actor.list.User.Permission.Add(new global::app.data.@this<PermissionRecord>("", grant, context: app.actor.list.User.Context), persist: false);
        app.actor.list.System.Channel.Register(new CannedAnswerChannel(new[] { "n" }, app.actor.list.System.Context));

        var asUser = await path.Authorize(Verb.read, app.actor.list.User.Context);
        var asSystem = await path.Authorize(Verb.read, app.actor.list.System.Context);

        await asUser.IsSuccess();
        await asSystem.IsFailure();
        await Assert.That(asSystem.Error).IsTypeOf<global::app.error.PermissionDenied>();
    }

    [Test] public async Task Authorize_StatefulAnswerY_SignsWithoutExpiry_Adds_ReturnsOk()
    {
        var app = NewApp();
        app.actor.list.User.Channel.Register(new CannedAnswerChannel(new[] { "y" }, app.actor.list.User.Context));
        var context = app.actor.list.User.Context;
        var path = app.actor.list.User.Context.File("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.read, app.actor.list.User.Context);
        await result.IsSuccess();
        await Assert.That(await app.actor.list.User.Permission.Find(path, global::app.type.item.permission.Verb.read)).IsNotNull();
    }

    [Test] public async Task Authorize_StatefulAnswerN_ReturnsFail_PermissionDenied()
    {
        var app = NewApp();
        app.actor.list.User.Channel.Register(new CannedAnswerChannel(new[] { "n" }, app.actor.list.User.Context));
        var context = app.actor.list.User.Context;
        var path = app.actor.list.User.Context.File("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.read, app.actor.list.User.Context);
        await result.IsFailure();
        await Assert.That(result.Error).IsTypeOf<global::app.error.PermissionDenied>();
    }

    /// Stub channel whose Ask fails with the given error — nobody able to answer (NoAnswer, the stream's end) or
    /// any other failure.
    private sealed class FailingAskChannel(global::app.error.@this failure, global::app.actor.context.@this ctx) : global::app.channel.@this
    {
        public FailingAskChannel(string key, global::app.actor.context.@this ctx) : this(new global::app.error.ServiceError($"Channel 'input' failed ({key})", key, 400), ctx) { }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(
            global::app.module.output.ask action, CancellationToken ct = default)
            => Task.FromResult(action.Context.Error(failure));
    }

    [Test] public async Task Authorize_NobodyCanAnswer_IsPermissionDenied_WithTheChannelFailureAsCause()
    {
        var app = NewApp();
        app.actor.list.User.Channel.Register(new FailingAskChannel(new global::app.error.NoAnswer("Channel 'input' has no interactive answerer"), app.actor.list.User.Context) { Name = "input", Direction = global::app.channel.ChannelDirection.Bidirectional });
        var path = app.actor.list.User.Context.File("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.read, app.actor.list.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error).IsTypeOf<global::app.error.PermissionDenied>()
            .Because($"{result.Error?.Key}: {result.Error?.Message}");
        await Assert.That(result.Error!.list.Single().Key).IsEqualTo("ChannelEof");
    }

    [Test] public async Task Authorize_OtherAskFailure_SurfacesAsItself()
    {
        var app = NewApp();
        app.actor.list.User.Channel.Register(new FailingAskChannel("ChannelBroken", app.actor.list.User.Context) { Name = "input", Direction = global::app.channel.ChannelDirection.Bidirectional });
        var path = app.actor.list.User.Context.File("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.read, app.actor.list.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelBroken");
    }

    [Test] public async Task Authorize_StatefulAnswerGarbage_RecursesWithInvalidPrefix()
    {
        var app = NewApp();
        // Garbage first, then "a" — pin recursion fires and second call accepts.
        app.actor.list.User.Channel.Register(new CannedAnswerChannel(new[] { "garbage", "a" }, app.actor.list.User.Context));
        var context = app.actor.list.User.Context;
        var path = app.actor.list.User.Context.File("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.read, app.actor.list.User.Context);
        await result.IsSuccess();
    }

    [Test] public async Task Authorize_StatelessChannel_BubblesDataAskUnchanged()
    {
        var app = NewApp();
        app.actor.list.User.Channel.Register(new StatelessChannel(app.actor.list.User.Context));
        var context = app.actor.list.User.Context;
        var path = app.actor.list.User.Context.File("/p");

        var result = await path.Authorize(global::app.type.item.permission.Verb.read, app.actor.list.User.Context);
        // Stateless: bubble the Exit-typed Data up so the step loop short-circuits.
        await Assert.That(result.Type?.Name).IsEqualTo("ask");
        await Assert.That(result.Snapshot).IsNotNull();
    }

    [Test] public async Task Authorize_ConstructedPermission_HasExpectedActorPathVerbMatch()
    {
        var app = NewApp();
        app.actor.list.User.Channel.Register(new CannedAnswerChannel(new[] { "a" }, app.actor.list.User.Context));
        var context = app.actor.list.User.Context;
        var path = app.actor.list.User.Context.File("/apps/Email/file.txt");
        var verb = global::app.type.item.permission.Verb.read;

        await path.Authorize(verb, app.actor.list.User.Context);
        var grant = await app.actor.list.User.Permission.Find(path, verb);
        await Assert.That(grant).IsNotNull();
        await Assert.That((await grant!.Value<PermissionRecord>())!.Actor).IsEqualTo(app.actor.list.User.Name);
        await Assert.That((await grant!.Value<PermissionRecord>())!.Path).IsEqualTo("/apps/Email/file.txt");
        await Assert.That((await grant!.Value<PermissionRecord>())!.Match).IsEqualTo(MatchMode.exact);
    }

    [Test] public async Task PermissionDenied_Error_CarriesConstructedPermission()
    {
        var app = NewApp();
        app.actor.list.User.Channel.Register(new CannedAnswerChannel(new[] { "n" }, app.actor.list.User.Context));
        var context = app.actor.list.User.Context;
        var path = app.actor.list.User.Context.File("/secret");

        var result = await path.Authorize(global::app.type.item.permission.Verb.read, app.actor.list.User.Context);
        var denied = (global::app.error.PermissionDenied)result.Error!;
        await Assert.That(denied.Permission.Path).IsEqualTo("/secret");
        await Assert.That(denied.Permission.Actor).IsEqualTo(app.actor.list.User.Name);
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
        app.actor.list.User.Channel.Register(new CannedAnswerChannel(new[] { "n" }, app.actor.list.User.Context));
        var context = app.actor.list.User.Context;
        var uppered = app.AbsolutePath.ToUpperInvariant() + "/file.txt";
        var path = app.actor.list.User.Context.File(uppered);

        var result = await path.Authorize(global::app.type.item.permission.Verb.read, app.actor.list.User.Context);
        await result.IsFailure();
        await Assert.That(result.Error).IsTypeOf<global::app.error.PermissionDenied>();
    }

    /// IsInRoot's second clause: the os root (system-built-in goals like
    /// test, build) auto-grants without a prompt, even though it sits outside
    /// the actor's RootDirectory. Verifies the runtime-owned-files carve-out.
    [Test] public async Task IsInRoot_PathUnderTheOsRoot_AutoGrants_NoChannelAsk()
    {
        var app = NewApp();
        // No channel registered — if Authorize tried to prompt, it would
        // throw or return non-Ok. Auto-grant means Ok with no ask.
        var context = app.actor.list.User.Context;
        var osDir = app.OsAbsolutePath;
        var osPath = System.IO.Path.Combine(osDir, "system", "test", "fixture.goal");
        var path = app.actor.list.User.Context.File(osPath);

        var result = await path.Authorize(global::app.type.item.permission.Verb.read, app.actor.list.User.Context);
        await result.IsSuccess();
    }

    [Test] public async Task PermissionDenied_Error_RoundTripsThroughErrorShape()
    {
        var app = NewApp();
        var perm = new PermissionRecord("user", "/p", global::app.type.item.permission.@this.AllVerbs, MatchMode.exact, app.type.list[typeof(PermissionRecord)]);
        var err = new global::app.error.PermissionDenied(perm);
        await Assert.That(err.Key).IsEqualTo("PermissionDenied");
        await Assert.That(err.Status.Code.ToInt32()).IsEqualTo(403);
        await Assert.That(err.Permission).IsSameReferenceAs(perm);
    }
}
