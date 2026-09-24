using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using HttpPath = global::app.type.item.path.http.@this;

namespace PLang.Tests.App.Types.PathTests.Http;

/// <summary>
/// <C>HttpPath : Path</c>, the second scheme.
/// </summary>
public class HttpPathTests
{
    private static (global::app.@this app, global::app.actor.context.@this context) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-http-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = TestApp.Create(dir);
        // Pre-grant http access so the Permission gate doesn't prompt during tests.
        return (app, app.User.Context);
    }

    /// <summary>Pre-authorize an http URL so the verb under test isn't blocked by the gate.</summary>
    private static async Task Grant(global::app.@this app, global::app.actor.context.@this context, string url)
    {
        var perm = new global::app.type.item.permission.@this(
            "User", new HttpPath(url).Absolute,
            global::app.type.item.permission.@this.AllVerbs,
            global::app.type.item.permission.Match.Exact);
        await context.Actor!.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", perm, context: context), persist: true);
    }

    [Test] public async Task Get_200_ReadText_ReturnsBody()
    {
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, context, url);
        await new HttpPath(url).WriteText("the body", context);

        var result = await new HttpPath(url).ReadText(context);
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("the body");
    }

    [Test] public async Task Get_404_ReturnsFail_WithNotFoundStatus()
    {
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        var url = server.NewResourceUrl();   // never written → 404
        await Grant(app, context, url);

        var result = await new HttpPath(url).ReadText(context);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    [Test] public async Task Post_200_WriteText_ReturnsOk_AndBodyIsStored()
    {
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, context, url);

        var write = await new HttpPath(url).WriteText("posted body", context);
        await write.IsSuccess();

        var read = await new HttpPath(url).ReadText(context);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("posted body");
    }

    [Test] public async Task Post_405_ReturnsFail_405_MethodNotAllowed()
    {
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        var url = server.MapGetOnly();
        await Grant(app, context, url);

        var result = await new HttpPath(url).WriteText("nope", context);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("MethodNotAllowed");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(405);
    }

    [Test] public async Task Delete_204_ReturnsOk()
    {
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, context, url);
        await new HttpPath(url).WriteText("to delete", context);

        var del = await new HttpPath(url).Delete(context);
        await del.IsSuccess();

        var read = await new HttpPath(url).ReadText(context);
        await read.IsFailure();
        await Assert.That(read.Error!.StatusCode).IsEqualTo(404);
    }

    [Test] public async Task Stat_Head_PopulatesContentLengthAndLastModified()
    {
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, context, url);
        await new HttpPath(url).WriteText("12345", context);

        var stat = await new HttpPath(url).Stat(context);
        await stat.IsSuccess();
        var info = (global::app.type.item.path.@this.StatInfo)(await stat.Value())!;
        await Assert.That(info.Exists).IsTrue();
        await Assert.That(info.Length).IsEqualTo(5L);
        await Assert.That(info.Modified).IsNotNull();
    }

    [Test] public async Task AsBooleanAsync_TrueWhenPresent_FalseWhenAbsent()
    {
        // http path truthiness is "does the resource exist" — an HTTP HEAD,
        // the async dispatch target for `if %url% exists`.
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        var present = server.NewResourceUrl();
        var absent = server.NewResourceUrl();
        await Grant(app, context, present);
        await Grant(app, context, absent);
        await new HttpPath(present).WriteText("here", context);

        await Assert.That(await new HttpPath(present).AsBooleanAsync(context)).IsTrue();
        await Assert.That(await new HttpPath(absent).AsBooleanAsync(context)).IsFalse();
    }

    [Test] public async Task Exists_2xx_True_4xx_False()
    {
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        var present = server.NewResourceUrl();
        var absent = server.NewResourceUrl();
        await Grant(app, context, present);
        await Grant(app, context, absent);
        await new HttpPath(present).WriteText("here", context);

        var existsPresent = await new HttpPath(present).ExistsAsync(context);
        await existsPresent.IsSuccess();
        await Assert.That((await existsPresent.Value())).IsEqualTo(true);

        var existsAbsent = await new HttpPath(absent).ExistsAsync(context);
        await existsAbsent.IsSuccess();
        await Assert.That((await existsAbsent.Value())).IsEqualTo(false);
    }

    [Test] public async Task Request_CarriesPlangSigningIdentityHeaders()
    {
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, context, url);

        await new HttpPath(url).ReadText(context);

        var captured = server.Requests.FirstOrDefault(r => r.Method == "GET");
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Headers.ContainsKey("X-Signature")).IsTrue();
    }

    [Test] public async Task IdentityRejected_401_CapturedAsFail()
    {
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        // Force a 401 on a fresh URL.
        var u = server.NewResourceUrl();
        server.MapStatus(u, 401);
        await Grant(app, context, u);

        var result = await new HttpPath(u).ReadText(context);
        await result.IsFailure();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(401);
    }

    [Test] public async Task NetworkFailure_ConnectionRefused_ReturnsFail_NetworkError()
    {
        var (app, context) = MakeApp();
        // A loopback port nothing is listening on.
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        int deadPort = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var url = $"http://127.0.0.1:{deadPort}/nothing";
        await Grant(app, context, url);

        var result = await new HttpPath(url).ReadText(context);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NetworkError");
    }

    [Test] public async Task NoPerInstanceState_TwoReads_TwoIndependentRequests()
    {
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, context, url);
        await new HttpPath(url).WriteText("body", context);

        var path = new HttpPath(url);
        await path.ReadText(context);
        await path.ReadText(context);

        var gets = server.Requests.Count(r => r.Method == "GET" && r.Path == new System.Uri(url).AbsolutePath);
        await Assert.That(gets).IsEqualTo(2);
    }

    [Test] public async Task HttpClient_IsProcessShared_NotRecreatedPerInstance()
    {
        using var server = new HttpTestServer();
        var (app, context) = MakeApp();
        var url = server.NewResourceUrl();
        await Grant(app, context, url);
        await new HttpPath(url).WriteText("shared", context);

        // Many instances issuing requests concurrently — a per-instance client
        // would exhaust sockets; a shared static client pools connections.
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => new HttpPath(url).ReadText(context));
        var results = await Task.WhenAll(tasks);
        await Assert.That(results.All(r => r.Success)).IsTrue();
    }
}
