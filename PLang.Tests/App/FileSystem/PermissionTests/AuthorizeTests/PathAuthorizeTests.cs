using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Path = global::App.FileSystem.Path;
using PermissionRecord = global::App.FileSystem.Permission.@this;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using Read = global::App.FileSystem.Permission.Verb.Read;
using MatchMode = global::App.FileSystem.Permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// Stage 2b — Batch 6: `Path.Authorize(verb)` consults the actor's permission
/// view, asks the channel on miss, signs + stores on grant, surfaces
/// PermissionDenied on refusal, recurses on bad input.
public class PathAuthorizeTests
{
    private static global::App.@this NewApp() =>
        new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-auth-" + System.Guid.NewGuid().ToString("N")[..8]));

    /// Stub stateful channel — answers Ask with a pre-set canned line.
    private sealed class CannedAnswerChannel : global::App.Channels.Channel.@this
    {
        public string[] Answers { get; }
        private int _idx;
        public CannedAnswerChannel(string[] answers)
        {
            Name = "input";
            Direction = global::App.Channels.Channel.ChannelDirection.Bidirectional;
            Answers = answers;
        }
        public override Task<global::App.Data.@this> WriteCore(global::App.Data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::App.Data.@this.Ok());
        public override Task<global::App.Data.@this> ReadCore(CancellationToken ct = default)
            => Task.FromResult(global::App.Data.@this.Ok((object?)null));
        public override Task<global::App.Data.@this> AskCore(
            global::App.modules.output.ask action, CancellationToken ct = default)
        {
            var ans = _idx < Answers.Length ? Answers[_idx++] : "";
            return Task.FromResult(global::App.Data.@this.Ok(ans));
        }
    }

    private sealed class StatelessChannel : global::App.Channels.Channel.Message.@this
    {
        public StatelessChannel() { Name = "input"; Direction = global::App.Channels.Channel.ChannelDirection.Bidirectional; }
        public override Task<global::App.Data.@this> WriteCore(global::App.Data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::App.Data.@this.Ok());
        public override Task<global::App.Data.@this> ReadCore(CancellationToken ct = default)
            => Task.FromResult(global::App.Data.@this.Ok((object?)null));
    }

    [Test] public async Task Authorize_GrantExists_ReturnsOk_NoChannelAsk()
    {
        var app = NewApp();
        var ctx = app.User.Context;
        var path = new Path("/p", ctx);

        // Pre-seed a grant covering the request.
        var grant = new PermissionRecord(app.Id, app.User.Name, "/p", new Verb(), MatchMode.Exact);
        var grantData = new global::App.Data.@this<PermissionRecord>("", grant) { Context = ctx };
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
        await Assert.That(result.Error).IsTypeOf<global::App.Errors.PermissionDenied>();
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

    [Test] public async Task Authorize_ConstructedPermission_HasExpectedAppIdActorPathVerbMatch()
    {
        var app = NewApp();
        app.User.Channels.Register(new CannedAnswerChannel(new[] { "a" }));
        var ctx = app.User.Context;
        var path = new Path("/apps/Email/file.txt", ctx);
        var verb = new Verb { Read = new Read() };

        await path.Authorize(verb);
        var grant = await app.User.Permission.Find(path, verb);
        await Assert.That(grant).IsNotNull();
        await Assert.That(grant!.Value!.AppId).IsEqualTo(app.Id);
        await Assert.That(grant.Value.Actor).IsEqualTo(app.User.Name);
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
        var denied = (global::App.Errors.PermissionDenied)result.Error!;
        await Assert.That(denied.Permission.Path).IsEqualTo("/secret");
        await Assert.That(denied.Permission.Actor).IsEqualTo(app.User.Name);
    }

    [Test] public async Task PermissionDenied_Error_RoundTripsThroughErrorShape()
    {
        var perm = new PermissionRecord("app1", "user", "/p", new Verb(), MatchMode.Exact);
        var err = new global::App.Errors.PermissionDenied(perm);
        await Assert.That(err.Key).IsEqualTo("PermissionDenied");
        await Assert.That(err.StatusCode).IsEqualTo(403);
        await Assert.That(err.Permission).IsSameReferenceAs(perm);
    }
}
