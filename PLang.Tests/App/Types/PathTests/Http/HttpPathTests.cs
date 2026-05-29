using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using HttpPath = global::app.type.path.http.@this;

namespace PLang.Tests.App.Types.PathTests.Http;

/// <summary>
/// <C>HttpPath : Path</c>, the second scheme.
/// </summary>
public class HttpPathTests
{
    private static (global::app.@this app, global::app.actor.context.@this ctx) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-http-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new global::app.@this(dir);
        // Pre-grant http access so the Permission gate doesn't prompt during tests.
        return (app, app.User.Context);
    }

    /// <summary>Pre-authorize an http URL so the verb under test isn't blocked by the gate.</summary>
    private static async Task Grant(global::app.@this app, global::app.actor.context.@this ctx, string url)
    {
        var perm = new global::app.type.path.permission.@this(
            "User", new HttpPath(url, ctx).Absolute,
            global::app.type.path.permission.verb.@this.AllowAll(),
            global::app.type.path.permission.Match.Exact);
        await ctx.Actor!.Permission.Add(new global::app.data.@this<global::app.type.path.permission.@this>("", perm) { Context = ctx });
    }

    [Test] public async Task Get_200_ReadText_ReturnsBody()
    {
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, ctx, url);
        await new HttpPath(url, ctx).WriteText("the body");

        var result = await new HttpPath(url, ctx).ReadText();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("the body");
    }

    [Test] public async Task Get_404_ReturnsFail_WithNotFoundStatus()
    {
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        var url = server.NewResourceUrl();   // never written → 404
        await Grant(app, ctx, url);

        var result = await new HttpPath(url, ctx).ReadText();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    [Test] public async Task Post_200_WriteText_ReturnsOk_AndBodyIsStored()
    {
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, ctx, url);

        var write = await new HttpPath(url, ctx).WriteText("posted body");
        await Assert.That(write.Success).IsTrue();

        var read = await new HttpPath(url, ctx).ReadText();
        await Assert.That(read.Value).IsEqualTo("posted body");
    }

    [Test] public async Task Post_405_ReturnsFail_405_MethodNotAllowed()
    {
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        var url = server.MapGetOnly();
        await Grant(app, ctx, url);

        var result = await new HttpPath(url, ctx).WriteText("nope");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MethodNotAllowed");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(405);
    }

    [Test] public async Task Delete_204_ReturnsOk()
    {
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, ctx, url);
        await new HttpPath(url, ctx).WriteText("to delete");

        var del = await new HttpPath(url, ctx).Delete();
        await Assert.That(del.Success).IsTrue();

        var read = await new HttpPath(url, ctx).ReadText();
        await Assert.That(read.Success).IsFalse();
        await Assert.That(read.Error!.StatusCode).IsEqualTo(404);
    }

    [Test] public async Task Stat_Head_PopulatesContentLengthAndLastModified()
    {
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, ctx, url);
        await new HttpPath(url, ctx).WriteText("12345");

        var stat = await new HttpPath(url, ctx).Stat();
        await Assert.That(stat.Success).IsTrue();
        var info = (global::app.type.path.@this.StatInfo)stat.Value!;
        await Assert.That(info.Exists).IsTrue();
        await Assert.That(info.Length).IsEqualTo(5L);
        await Assert.That(info.Modified).IsNotNull();
    }

    [Test] public async Task AsBooleanAsync_TrueWhenPresent_FalseWhenAbsent()
    {
        // http path truthiness is "does the resource exist" — an HTTP HEAD,
        // the async dispatch target for `if %url% exists`.
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        var present = server.NewResourceUrl();
        var absent = server.NewResourceUrl();
        await Grant(app, ctx, present);
        await Grant(app, ctx, absent);
        await new HttpPath(present, ctx).WriteText("here");

        await Assert.That(await new HttpPath(present, ctx).AsBooleanAsync()).IsTrue();
        await Assert.That(await new HttpPath(absent, ctx).AsBooleanAsync()).IsFalse();
    }

    [Test] public async Task Exists_2xx_True_4xx_False()
    {
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        var present = server.NewResourceUrl();
        var absent = server.NewResourceUrl();
        await Grant(app, ctx, present);
        await Grant(app, ctx, absent);
        await new HttpPath(present, ctx).WriteText("here");

        var existsPresent = await new HttpPath(present, ctx).ExistsAsync();
        await Assert.That(existsPresent.Success).IsTrue();
        await Assert.That(existsPresent.Value).IsEqualTo(true);

        var existsAbsent = await new HttpPath(absent, ctx).ExistsAsync();
        await Assert.That(existsAbsent.Success).IsTrue();
        await Assert.That(existsAbsent.Value).IsEqualTo(false);
    }

    [Test] public async Task Request_CarriesPlangSigningIdentityHeaders()
    {
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, ctx, url);

        await new HttpPath(url, ctx).ReadText();

        var captured = server.Requests.FirstOrDefault(r => r.Method == "GET");
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Headers.ContainsKey("X-Signature")).IsTrue();
    }

    [Test] public async Task IdentityRejected_401_CapturedAsFail()
    {
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        // Force a 401 on a fresh URL.
        var u = server.NewResourceUrl();
        server.MapStatus(u, 401);
        await Grant(app, ctx, u);

        var result = await new HttpPath(u, ctx).ReadText();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(401);
    }

    [Test] public async Task NetworkFailure_ConnectionRefused_ReturnsFail_NetworkError()
    {
        var (app, ctx) = MakeApp();
        // A loopback port nothing is listening on.
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        int deadPort = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var url = $"http://127.0.0.1:{deadPort}/nothing";
        await Grant(app, ctx, url);

        var result = await new HttpPath(url, ctx).ReadText();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NetworkError");
    }

    [Test] public async Task NoPerInstanceState_TwoReads_TwoIndependentRequests()
    {
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, ctx, url);
        await new HttpPath(url, ctx).WriteText("body");

        var path = new HttpPath(url, ctx);
        await path.ReadText();
        await path.ReadText();

        var gets = server.Requests.Count(r => r.Method == "GET" && r.Path == new System.Uri(url).AbsolutePath);
        await Assert.That(gets).IsEqualTo(2);
    }

    [Test] public async Task HttpClient_IsProcessShared_NotRecreatedPerInstance()
    {
        using var server = new HttpTestServer();
        var (app, ctx) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, ctx, url);
        await new HttpPath(url, ctx).WriteText("shared");

        // Many instances issuing requests concurrently — a per-instance client
        // would exhaust sockets; a shared static client pools connections.
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => new HttpPath(url, ctx).ReadText());
        var results = await Task.WhenAll(tasks);
        await Assert.That(results.All(r => r.Success)).IsTrue();
    }
}
