using System.Text.Json;
using HttpPath = global::app.type.path.http.@this;
using AppEngine = global::app.@this;
using Ctx = global::app.actor.context.@this;
using PathPermission = global::app.type.path.permission.@this;
using PermVerb = global::app.type.path.permission.verb.@this;
using PermMatch = global::app.type.path.permission.Match;
using PLang.Tests.App.Types.PathTests.Contract;

namespace PLang.Tests.App.Types.PathTests.Http;

/// <summary>
/// <c>HttpPath</c> handles 3xx manually: each hop builds a fresh
/// <see cref="HttpPath"/>, runs it through <see cref="@this.AuthGate"/>
/// (own consent prompt for the new URL), and signs with the destination's
/// URL. These tests pin:
///   – a 302 to an unauthorized host is gated, not auto-followed;
///   – a 302 to an authorized host is followed and returns that host's body;
///   – a redirect to an unsupported scheme is rejected;
///   – the hop cap fires on a redirect loop;
///   – the X-Signature on the redirect hop is fresh for the destination URL
///     (not the original signed-for-origin one).
/// </summary>
public class HttpPathRedirectTests
{
    private static (AppEngine app, Ctx context) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-http-redirect-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new AppEngine(dir);
        return (app, app.User.Context);
    }

    private static async Task Grant(Ctx context, string url)
    {
        var perm = new PathPermission(
            "User", new HttpPath(url, context).Absolute,
            PermVerb.AllowAll(),
            PermMatch.Exact);
        await context.Actor!.Permission.Add(new global::app.data.@this<PathPermission>("", perm) { Context = context });
    }

    [Test]
    public async Task Redirect_ToUnauthorizedHost_DeniedByGate_NotFollowed()
    {
        using var server = new HttpTestServer();
        var (_, context) = MakeApp();

        // Seed the "IMDS secret" directly into the server store — bypasses
        // the PLang gate, so the actor never holds a grant for this URL.
        var secret = server.MapStoredBody(System.Text.Encoding.UTF8.GetBytes("imds-secret"));

        // Origin 302s to the secret. Only the origin is granted; the
        // redirect target must hit the gate and get denied by the canned 'n'.
        var origin = server.MapRedirect(302, secret);
        await Grant(context, origin);
        context.Actor!.Channel.Register(new CannedAnswerChannel("n"));

        var result = await new HttpPath(origin, context).ReadText();

        await result.IsFailure();
        // The deny comes from PermissionDenied on the redirect target —
        // not from the origin (which was granted).
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionDenied");
        // Bonus: the secret bytes never came back.
        await Assert.That((await result.Value()) as string).IsNotEqualTo("imds-secret");
    }

    [Test]
    public async Task Redirect_ToAuthorizedHost_FollowsAndReturnsTargetBody()
    {
        using var server = new HttpTestServer();
        var (_, context) = MakeApp();

        var target = server.MapStoredBody(System.Text.Encoding.UTF8.GetBytes("target-body"));

        var origin = server.MapRedirect(302, target);
        await Grant(context, origin);
        await Grant(context, target);

        var result = await new HttpPath(origin, context).ReadText();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("target-body");
    }

    [Test]
    public async Task Redirect_ToUnsupportedScheme_RejectedTypedError()
    {
        using var server = new HttpTestServer();
        var (_, context) = MakeApp();

        var origin = server.MapRedirect(302, "ftp://example.invalid/etc/passwd");
        await Grant(context, origin);

        var result = await new HttpPath(origin, context).ReadText();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedRedirectScheme");
    }

    [Test]
    public async Task Redirect_Loop_ExhaustsHopCap_ReturnsTooManyRedirects()
    {
        using var server = new HttpTestServer();
        var (_, context) = MakeApp();

        // A chain of 7 redirects, each pre-granted. Cap is 5 hops; the 6th
        // FollowRedirect call has hopsLeft=0 and returns TooManyRedirects.
        var dead = "http://127.0.0.1:1/never-arrives";
        var chain = new string[8];
        chain[7] = dead;
        for (int i = 6; i >= 0; i--)
        {
            chain[i] = server.MapRedirect(302, chain[i + 1]);
        }

        for (int i = 0; i < 7; i++)
            await Grant(context, chain[i]);

        var result = await new HttpPath(chain[0], context).ReadText();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("TooManyRedirects");
    }

    [Test]
    public async Task Redirect_307_PreservesPostBody_AcrossHops()
    {
        // HttpContent is single-send: .NET disposes the underlying stream after
        // the first SendAsync, so the original implementation passed a
        // disposed-content instance to the next hop. v10 re-buffers the body
        // into a fresh ByteArrayContent per hop. This test fails red with the
        // old shape because the second POST silently sends no body — target
        // store ends up with an empty entry instead of "preserved-body".
        using var server = new HttpTestServer();
        var (_, context) = MakeApp();

        // Target — an unwritten URL the server's POST handler will populate.
        var target = server.NewResourceUrl();
        var origin = server.MapRedirect(307, target);

        await Grant(context, origin);
        await Grant(context, target);

        var writeResult = await new HttpPath(origin, context).WriteText("preserved-body");
        await writeResult.IsSuccess();

        // Round-trip: the target should now hold the body that 307 carried.
        var readResult = await new HttpPath(target, context).ReadText();
        await readResult.IsSuccess();
        await Assert.That((await readResult.Value())?.ToString()).IsEqualTo("preserved-body");
    }

    [Test]
    public async Task Redirect_Signature_IsFreshForDestination_NotOriginalUrl()
    {
        using var server = new HttpTestServer();
        var (_, context) = MakeApp();

        var target = server.MapStoredBody(System.Text.Encoding.UTF8.GetBytes("payload"));

        var origin = server.MapRedirect(302, target);
        await Grant(context, origin);
        await Grant(context, target);

        var result = await new HttpPath(origin, context).ReadText();
        await result.IsSuccess();

        // The second non-write request is the GET that followed the redirect.
        // Find the captured GET on the target's path and decode its X-Signature
        // envelope — the signed `url` claim must match the destination, not the
        // origin. (the original signature never reaches
        // a host the user didn't directly consent to.)
        var targetPath = new System.Uri(target).AbsolutePath;
        var targetGet = server.Requests
            .Where(r => r.Method == "GET" && r.Path == targetPath)
            .ToList();
        await Assert.That(targetGet.Count).IsGreaterThanOrEqualTo(1);

        var headers = targetGet[^1].Headers;
        await Assert.That(headers.ContainsKey("X-Signature")).IsTrue();
        var envelope = JsonDocument.Parse(headers["X-Signature"]).RootElement;
        var signedUrl = envelope.GetProperty("Headers").GetProperty("url").GetString();
        await Assert.That(signedUrl).IsEqualTo(new System.Uri(target).ToString());

        // And it is definitely not the origin URL.
        await Assert.That(signedUrl).IsNotEqualTo(new System.Uri(origin).ToString());
    }
}
