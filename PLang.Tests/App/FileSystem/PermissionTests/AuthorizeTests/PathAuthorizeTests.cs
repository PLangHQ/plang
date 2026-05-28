using Path = global::app.types.path.file.@this;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PermissionRecord = global::app.types.path.permission.@this;
using Verb = global::app.types.path.permission.verb.@this;
using Read = global::app.types.path.permission.verb.Read;
using MatchMode = global::app.types.path.permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// Stage 2b — Batch 6: `Path.Authorize(verb)` consults the actor's permission
/// view, asks the channel on miss, signs + stores on grant, surfaces
/// PermissionDenied on refusal, recurses on bad input.
public class PathAuthorizeTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-auth-" + System.Guid.NewGuid().ToString("N")[..8]));

    /// Stub stateful channel — answers Ask with a pre-set canned line.
    private sealed class CannedAnswerChannel : global::app.channels.channel.@this
    {
        public string[] Answers { get; }
        private int _idx;
        public CannedAnswerChannel(string[] answers)
        {
            Name = "input";
            Direction = global::app.channels.channel.ChannelDirection.Bidirectional;
            Answers = answers;
        }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(
            global::app.modules.output.ask action, CancellationToken ct = default)
        {
            var ans = _idx < Answers.Length ? Answers[_idx++] : "";
            return Task.FromResult(global::app.data.@this.Ok(ans));
        }
    }

    private sealed class StatelessChannel : global::app.channels.channel.message.@this
    {
        public StatelessChannel() { Name = "input"; Direction = global::app.channels.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));
    }

    [Test] public async Task Authorize_GrantExists_ReturnsOk_NoChannelAsk()
    {
        var app = NewApp();
        var ctx = app.User.Context;
        var path = new Path("/p", ctx);

        // Pre-seed a grant covering the request.
        var grant = new PermissionRecord(app.User.Name, "/p", Verb.AllowAll(), MatchMode.Exact);
        var grantData = new global::app.data.@this<PermissionRecord>("", grant) { Context = ctx };
        await app.User.Permission.Add(grantData);

        var result = await path.Authorize(new Verb { Read = new Read() });
        await Assert.That(result.Success).IsTrue();
    }

    [Test] public async Task Authorize_StatefulAnswerA_Signs_Adds_ReturnsOk()
    {
        var app = NewApp();
        app.User.Channels.Register(new CannedAnswerChannel(new[] { "a" }));
        var ctx = app.User.Context;
        var path = new Path("/p", ctx);

        var result = await path.Authorize(new Verb { Read = new Read() });
        await Assert.That(result.Success).IsTrue();
        // Subsequent Find should hit since Add ran.
        await Assert.That(await app.User.Permission.Find(path, new Verb { Read = new Read() })).IsNotNull();
    }

    [Test] public async Task Authorize_StatefulAnswerY_SignsWithoutExpiry_Adds_ReturnsOk()
    {
        var app = NewApp();
        app.User.Channels.Register(new CannedAnswerChannel(new[] { "y" }));
        var ctx = app.User.Context;
        var path = new Path("/p", ctx);

        var result = await path.Authorize(new Verb { Read = new Read() });
        await Assert.That(result.Success).IsTrue();
        await Assert.That(await app.User.Permission.Find(path, new Verb { Read = new Read() })).IsNotNull();
    }

    [Test] public async Task Authorize_StatefulAnswerN_ReturnsFail_PermissionDenied()
    {
        var app = NewApp();
        app.User.Channels.Register(new CannedAnswerChannel(new[] { "n" }));
        var ctx = app.User.Context;
        var path = new Path("/p", ctx);

        var result = await path.Authorize(new Verb { Read = new Read() });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsTypeOf<global::app.errors.PermissionDenied>();
    }

    [Test] public async Task Authorize_StatefulAnswerGarbage_RecursesWithInvalidPrefix()
    {
        var app = NewApp();
        // Garbage first, then "a" — pin recursion fires and second call accepts.
        app.User.Channels.Register(new CannedAnswerChannel(new[] { "garbage", "a" }));
        var ctx = app.User.Context;
        var path = new Path("/p", ctx);

        var result = await path.Authorize(new Verb { Read = new Read() });
        await Assert.That(result.Success).IsTrue();
    }

    [Test] public async Task Authorize_StatelessChannel_BubblesDataAskUnchanged()
    {
        var app = NewApp();
        app.User.Channels.Register(new StatelessChannel());
        var ctx = app.User.Context;
        var path = new Path("/p", ctx);

        var result = await path.Authorize(new Verb { Read = new Read() });
        // Stateless: bubble the Exit-typed Data up so the step loop short-circuits.
        await Assert.That(result.Type?.Value).IsEqualTo("ask");
        await Assert.That(result.Snapshot).IsNotNull();
    }

    [Test] public async Task Authorize_ConstructedPermission_HasExpectedActorPathVerbMatch()
    {
        var app = NewApp();
        app.User.Channels.Register(new CannedAnswerChannel(new[] { "a" }));
        var ctx = app.User.Context;
        var path = new Path("/apps/Email/file.txt", ctx);
        var verb = new Verb { Read = new Read() };

        await path.Authorize(verb);
        var grant = await app.User.Permission.Find(path, verb);
        await Assert.That(grant).IsNotNull();
        await Assert.That(grant!.Value!.Actor).IsEqualTo(app.User.Name);
        await Assert.That(grant.Value.Path).IsEqualTo("/apps/Email/file.txt");
        await Assert.That(grant.Value.Match).IsEqualTo(MatchMode.Exact);
    }

    [Test] public async Task PermissionDenied_Error_CarriesConstructedPermission()
    {
        var app = NewApp();
        app.User.Channels.Register(new CannedAnswerChannel(new[] { "n" }));
        var ctx = app.User.Context;
        var path = new Path("/secret", ctx);

        var result = await path.Authorize(new Verb { Read = new Read() });
        var denied = (global::app.errors.PermissionDenied)result.Error!;
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
        app.User.Channels.Register(new CannedAnswerChannel(new[] { "n" }));
        var ctx = app.User.Context;
        var uppered = app.AbsolutePath.ToUpperInvariant() + "/file.txt";
        var path = new Path(uppered, ctx);

        var result = await path.Authorize(new Verb { Read = new Read() });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsTypeOf<global::app.errors.PermissionDenied>();
    }

    /// IsInRoot's second clause: OsDirectory (system-built-in goals like
    /// test, build) auto-grants without a prompt, even though it sits outside
    /// the actor's RootDirectory. Verifies the runtime-owned-files carve-out.
    [Test] public async Task IsInRoot_PathUnderOsDirectory_AutoGrants_NoChannelAsk()
    {
        var app = NewApp();
        // No channel registered — if Authorize tried to prompt, it would
        // throw or return non-Ok. Auto-grant means Ok with no ask.
        var ctx = app.User.Context;
        var osDir = app.OsAbsolutePath;
        var osPath = System.IO.Path.Combine(osDir, "system", "test", "fixture.goal");
        var path = new Path(osPath, ctx);

        var result = await path.Authorize(new Verb { Read = new Read() });
        await Assert.That(result.Success).IsTrue();
    }

    [Test] public async Task PermissionDenied_Error_RoundTripsThroughErrorShape()
    {
        var perm = new PermissionRecord("user", "/p", Verb.AllowAll(), MatchMode.Exact);
        var err = new global::app.errors.PermissionDenied(perm);
        await Assert.That(err.Key).IsEqualTo("PermissionDenied");
        await Assert.That(err.StatusCode).IsEqualTo(403);
        await Assert.That(err.Permission).IsSameReferenceAs(perm);
    }
}
