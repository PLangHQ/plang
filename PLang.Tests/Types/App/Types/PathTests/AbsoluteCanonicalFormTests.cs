using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.path.file.@this;
using HttpPath = global::app.type.path.http.@this;
using PermissionRecord = global::app.type.permission.@this;
using MatchMode = global::app.type.permission.Match;
using Verb = global::app.type.permission.Verb;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// <C>Path.Absolute</c> canonical form. One test per HttpPath rule.
/// </summary>
public class AbsoluteCanonicalFormTests
{
    private static (global::app.@this app, global::app.actor.context.@this context) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-abs-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = TestApp.Create(dir);
        return (app, app.User.Context);
    }

    [Test] public async Task FilePath_Absolute_Unchanged_OsNormalized()
    {
        var (_, context) = MakeApp();
        var p = FilePath.Resolve("/home/x/../y/z.txt", context);
        // OS-normalized: the .. segment is collapsed.
        await Assert.That(p.Absolute).DoesNotContain("..");
        await Assert.That(p.Absolute).Contains("z.txt");
    }

    [Test] public async Task HttpPath_Absolute_LowercasesSchemeAndHost()
    {
        var (_, context) = MakeApp();
        var p = new HttpPath("HTTP://Example.COM/Foo", context);
        await Assert.That(p.Absolute).IsEqualTo("http://example.com/Foo");
    }

    [Test] public async Task HttpPath_Absolute_StripsDefaultHttpsPort()
    {
        var (_, context) = MakeApp();
        var p = new HttpPath("https://example.com:443/foo", context);
        await Assert.That(p.Absolute).IsEqualTo("https://example.com/foo");
    }

    [Test] public async Task HttpPath_Absolute_StripsDefaultHttpPort()
    {
        var (_, context) = MakeApp();
        var p = new HttpPath("http://example.com:80/foo", context);
        await Assert.That(p.Absolute).IsEqualTo("http://example.com/foo");
    }

    [Test] public async Task HttpPath_Absolute_KeepsNonDefaultPort()
    {
        var (_, context) = MakeApp();
        var p = new HttpPath("https://example.com:8443/foo", context);
        await Assert.That(p.Absolute).IsEqualTo("https://example.com:8443/foo");
    }

    [Test] public async Task HttpPath_Absolute_NormalizesPathSegments()
    {
        var (_, context) = MakeApp();
        var p = new HttpPath("https://example.com/a/../b", context);
        await Assert.That(p.Absolute).IsEqualTo("https://example.com/b");
    }

    [Test] public async Task HttpPath_Absolute_RootWithAndWithoutTrailingSlash_AreEqual()
    {
        var (_, context) = MakeApp();
        var withSlash = new HttpPath("https://example.com/", context);
        var withoutSlash = new HttpPath("https://example.com", context);
        await Assert.That(withSlash.Absolute).IsEqualTo(withoutSlash.Absolute);
        await Assert.That(withSlash.Absolute).IsEqualTo("https://example.com/");
    }

    [Test] public async Task HttpPath_Absolute_SortsQueryParameters_ByKey()
    {
        var (_, context) = MakeApp();
        var p = new HttpPath("https://example.com/?b=2&a=1", context);
        await Assert.That(p.Absolute).IsEqualTo("https://example.com/?a=1&b=2");
    }

    [Test] public async Task HttpPath_Absolute_StripsFragment()
    {
        var (_, context) = MakeApp();
        var p = new HttpPath("https://example.com/foo#bar", context);
        await Assert.That(p.Absolute).IsEqualTo("https://example.com/foo");
    }

    [Test] public async Task Permission_FilePathGrant_DoesNotMatch_HttpPathRequest()
    {
        var (_, context) = MakeApp();
        var filePath = FilePath.Resolve("/home/data.json", context);
        var httpPath = new HttpPath("https://api.example.com/data.json", context);

        var grant = new PermissionRecord("User", filePath.Absolute, global::app.type.permission.@this.AllVerbs, MatchMode.Exact);
        var request = new PermissionRecord("User", httpPath.Absolute, new System.Collections.Generic.HashSet<global::app.type.permission.Verb> { global::app.type.permission.Verb.Read }, MatchMode.Exact);

        await Assert.That(grant.Covers(request)).IsFalse();
    }

    [Test] public async Task Permission_HttpPathGlobGrant_MatchesUrlUnderHost()
    {
        var (_, context) = MakeApp();
        var request = new HttpPath("https://api.example.com/users", context);

        var grant = new PermissionRecord("User", "https://api.example.com/*", global::app.type.permission.@this.AllVerbs, MatchMode.Glob);
        var req = new PermissionRecord("User", request.Absolute, new System.Collections.Generic.HashSet<global::app.type.permission.Verb> { global::app.type.permission.Verb.Read }, MatchMode.Exact);

        await Assert.That(grant.Covers(req)).IsTrue();
    }
}
