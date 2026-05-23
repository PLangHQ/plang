using System.Text.Json;
using HttpPath = global::app.types.path.http.@this;
using AppEngine = global::app.@this;
using Ctx = global::app.actor.context.@this;
using PathPermission = global::app.types.path.permission.@this;
using PermVerb = global::app.types.path.permission.verb.@this;
using PermMatch = global::app.types.path.permission.Match;
using PLang.Tests.App.Types.PathTests.Contract;

namespace PLang.Tests.App.Types.PathTests.Http;

/// <summary>
/// Security v1 S1 + partial S2. <c>HttpPath</c> handles 3xx manually now:
/// each hop builds a fresh <see cref="HttpPath"/>, runs it through
/// <see cref="@this.AuthGate"/> (own consent prompt for the new URL), and
/// signs with the destination's URL. These tests pin:
///   – a 302 to an unauthorized host is gated, not auto-followed (S1 core);
///   – a 302 to an authorized host is followed and returns that host's body;
///   – a redirect to an unsupported scheme is rejected;
///   – the hop cap fires on a redirect loop;
///   – the X-Signature on the redirect hop is fresh for the destination URL
///     (not the original signed-for-origin one) — S2 partial.
/// </summary>
public class HttpPathRedirectTests
{
    private static (AppEngine app, Ctx ctx) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-http-redirect-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new AppEngine(dir);
        return (app, app.User.Context);
    }

    private static async Task Grant(Ctx ctx, string url)
    {
        var perm = new PathPermission(
            "User", new HttpPath(url, ctx).Absolute,
            PermVerb.AllowAll(),
            PermMatch.Exact);
        await ctx.Actor!.Permission.Add(new global::app.data.@this<PathPermission>("", perm) { Context = ctx });
    }

    [Test]
    public async Task Redirect_ToUnauthorizedHost_DeniedByGate_NotFollowed()
    {
        using var server = new HttpTestServer();
        var (_, ctx) = MakeApp();

        // Seed the "IMDS secret" directly into the server store — bypasses
        // the PLang gate, so the actor never holds a grant for this URL.
        var secret = server.MapStoredBody(System.Text.Encoding.UTF8.GetBytes("imds-secret"));

        // Origin 302s to the secret. Only the origin is granted; the
        // redirect target must hit the gate and get denied by the canned 'n'.
        var origin = server.MapRedirect(302, secret);
        await Grant(ctx, origin);
        ctx.Actor!.Channels.Register(new CannedAnswerChannel("n"));

        var result = await new HttpPath(origin, ctx).ReadText();

        await Assert.That(result.Success).IsFalse();
        // The deny comes from PermissionDenied on the redirect target —
        // not from the origin (which was granted).
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionDenied");
        // Bonus: the secret bytes never came back.
        await Assert.That(result.Value as string).IsNotEqualTo("imds-secret");
    }

    [Test]
    public async Task Redirect_ToAuthorizedHost_FollowsAndReturnsTargetBody()
    {
        using var server = new HttpTestServer();
        var (_, ctx) = MakeApp();

        var target = server.MapStoredBody(System.Text.Encoding.UTF8.GetBytes("target-body"));

        var origin = server.MapRedirect(302, target);
        await Grant(ctx, origin);
        await Grant(ctx, target);

        var result = await new HttpPath(origin, ctx).ReadText();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("target-body");
    }

    [Test]
    public async Task Redirect_ToUnsupportedScheme_RejectedTypedError()
    {
        using var server = new HttpTestServer();
        var (_, ctx) = MakeApp();

        var origin = server.MapRedirect(302, "ftp://example.invalid/etc/passwd");
        await Grant(ctx, origin);

        var result = await new HttpPath(origin, ctx).ReadText();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedRedirectScheme");
    }

    [Test]
    public async Task Redirect_Loop_ExhaustsHopCap_ReturnsTooManyRedirects()
    {
        using var server = new HttpTestServer();
        var (_, ctx) = MakeApp();

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
            await Grant(ctx, chain[i]);

        var result = await new HttpPath(chain[0], ctx).ReadText();

        await Assert.That(result.Success).IsFalse();
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
        var (_, ctx) = MakeApp();

        // Target — an unwritten URL the server's POST handler will populate.
        var target = server.NewResourceUrl();
        var origin = server.MapRedirect(307, target);

        await Grant(ctx, origin);
        await Grant(ctx, target);

        var writeResult = await new HttpPath(origin, ctx).WriteText("preserved-body");
        await Assert.That(writeResult.Success).IsTrue();

        // Round-trip: the target should now hold the body that 307 carried.
        var readResult = await new HttpPath(target, ctx).ReadText();
        await Assert.That(readResult.Success).IsTrue();
        await Assert.That(readResult.Value).IsEqualTo("preserved-body");
    }

    [Test]
    public async Task Redirect_Signature_IsFreshForDestination_NotOriginalUrl()
    {
        using var server = new HttpTestServer();
        var (_, ctx) = MakeApp();

        var target = server.MapStoredBody(System.Text.Encoding.UTF8.GetBytes("payload"));

        var origin = server.MapRedirect(302, target);
        await Grant(ctx, origin);
        await Grant(ctx, target);

        var result = await new HttpPath(origin, ctx).ReadText();
        await Assert.That(result.Success).IsTrue();

        // The second non-write request is the GET that followed the redirect.
        // Find the captured GET on the target's path and decode its X-Signature
        // envelope — the signed `url` claim must match the destination, not the
        // origin. (security v1 S2 partial: the original signature never reaches
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
