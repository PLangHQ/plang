using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.types.path.file.@this;
using HttpPath = global::app.types.path.http.@this;
using PermissionRecord = global::app.types.path.permission.@this;
using MatchMode = global::app.types.path.permission.Match;
using Verb = global::app.types.path.permission.verb.@this;
using ReadVerb = global::app.types.path.permission.verb.Read;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 6 — <c>Path.Absolute</c> canonical form. One test per HttpPath rule.
/// </summary>
public class AbsoluteCanonicalFormTests
{
    private static (global::app.@this app, global::app.actor.context.@this ctx) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-abs-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new global::app.@this(dir);
        return (app, app.User.Context);
    }

    [Test] public async Task FilePath_Absolute_Unchanged_OsNormalized()
    {
        var (_, ctx) = MakeApp();
        var p = FilePath.Resolve("/home/x/../y/z.txt", ctx);
        // OS-normalized: the .. segment is collapsed.
        await Assert.That(p.Absolute).DoesNotContain("..");
        await Assert.That(p.Absolute).Contains("z.txt");
    }

    [Test] public async Task HttpPath_Absolute_LowercasesSchemeAndHost()
    {
        var (_, ctx) = MakeApp();
        var p = new HttpPath("HTTP://Example.COM/Foo", ctx);
        await Assert.That(p.Absolute).IsEqualTo("http://example.com/Foo");
    }

    [Test] public async Task HttpPath_Absolute_StripsDefaultHttpsPort()
    {
        var (_, ctx) = MakeApp();
        var p = new HttpPath("https://example.com:443/foo", ctx);
        await Assert.That(p.Absolute).IsEqualTo("https://example.com/foo");
    }

    [Test] public async Task HttpPath_Absolute_StripsDefaultHttpPort()
    {
        var (_, ctx) = MakeApp();
        var p = new HttpPath("http://example.com:80/foo", ctx);
        await Assert.That(p.Absolute).IsEqualTo("http://example.com/foo");
    }

    [Test] public async Task HttpPath_Absolute_KeepsNonDefaultPort()
    {
        var (_, ctx) = MakeApp();
        var p = new HttpPath("https://example.com:8443/foo", ctx);
        await Assert.That(p.Absolute).IsEqualTo("https://example.com:8443/foo");
    }

    [Test] public async Task HttpPath_Absolute_NormalizesPathSegments()
    {
        var (_, ctx) = MakeApp();
        var p = new HttpPath("https://example.com/a/../b", ctx);
        await Assert.That(p.Absolute).IsEqualTo("https://example.com/b");
    }

    [Test] public async Task HttpPath_Absolute_RootWithAndWithoutTrailingSlash_AreEqual()
    {
        var (_, ctx) = MakeApp();
        var withSlash = new HttpPath("https://example.com/", ctx);
        var withoutSlash = new HttpPath("https://example.com", ctx);
        await Assert.That(withSlash.Absolute).IsEqualTo(withoutSlash.Absolute);
        await Assert.That(withSlash.Absolute).IsEqualTo("https://example.com/");
    }

    [Test] public async Task HttpPath_Absolute_SortsQueryParameters_ByKey()
    {
        var (_, ctx) = MakeApp();
        var p = new HttpPath("https://example.com/?b=2&a=1", ctx);
        await Assert.That(p.Absolute).IsEqualTo("https://example.com/?a=1&b=2");
    }

    [Test] public async Task HttpPath_Absolute_StripsFragment()
    {
        var (_, ctx) = MakeApp();
        var p = new HttpPath("https://example.com/foo#bar", ctx);
        await Assert.That(p.Absolute).IsEqualTo("https://example.com/foo");
    }

    [Test] public async Task Permission_FilePathGrant_DoesNotMatch_HttpPathRequest()
    {
        var (_, ctx) = MakeApp();
        var filePath = FilePath.Resolve("/home/data.json", ctx);
        var httpPath = new HttpPath("https://api.example.com/data.json", ctx);

        var grant = new PermissionRecord("User", filePath.Absolute, Verb.AllowAll(), MatchMode.Exact);
        var request = new PermissionRecord("User", httpPath.Absolute, new Verb { Read = new ReadVerb() }, MatchMode.Exact);

        await Assert.That(grant.Covers(request)).IsFalse();
    }

    [Test] public async Task Permission_HttpPathGlobGrant_MatchesUrlUnderHost()
    {
        var (_, ctx) = MakeApp();
        var request = new HttpPath("https://api.example.com/users", ctx);

        var grant = new PermissionRecord("User", "https://api.example.com/*", Verb.AllowAll(), MatchMode.Glob);
        var req = new PermissionRecord("User", request.Absolute, new Verb { Read = new ReadVerb() }, MatchMode.Exact);

        await Assert.That(grant.Covers(req)).IsTrue();
    }
}
