using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using global::app.types.path.scheme;
using PLangPath = global::app.types.path.@this;
using FilePath = global::app.types.path.file.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// The per-App scheme registry (<c>app.types.path.scheme.@this</c>), reachable as
/// <c>app.Types.Scheme</c>.
/// </summary>
public class SchemeRegistryTests
{
    private static (global::app.@this app, global::app.actor.context.@this ctx) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-scheme-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new global::app.@this(dir);
        return (app, app.User.Context);
    }

    [Test] public async Task Register_ThenFrom_ReturnsRegisteredSubclass()
    {
        var (app, ctx) = MakeApp();
        app.Types.Scheme.Register("test", (raw, c) => new FilePath(raw, c) { Raw = raw });
        var p = app.Types.Scheme.From("test://hello", ctx);
        await Assert.That(p).IsNotNull();
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task Register_SameSchemeTwice_SecondRegistrationReplacesFirst()
    {
        var (app, ctx) = MakeApp();
        var first = new FilePath("/first", ctx);
        var second = new FilePath("/second", ctx);
        app.Types.Scheme.Register("dup", (raw, c) => first);
        app.Types.Scheme.Register("dup", (raw, c) => second);
        var p = app.Types.Scheme.From("dup://x", ctx);
        await Assert.That(object.ReferenceEquals(p, second)).IsTrue();
    }

    [Test] public async Task From_BareAbsolutePath_RoutesToFilePath()
    {
        var (app, ctx) = MakeApp();
        var p = app.Types.Scheme.From("/tmp/anywhere/x.txt", ctx);
        await Assert.That(p is FilePath).IsTrue();
        await Assert.That(p.Scheme).IsEqualTo("file");
    }

    [Test] public async Task From_BareRelativePath_RoutesToFilePath()
    {
        var (app, ctx) = MakeApp();
        var p = app.Types.Scheme.From("relative.txt", ctx);
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task From_WindowsDriveLetterPath_RoutesToFilePath_NotSchemeColon()
    {
        var (app, ctx) = MakeApp();
        // C:\... has a colon but is NOT "scheme://" — must not be treated as a scheme.
        var p = app.Types.Scheme.From("C:\\Users\\x.txt", ctx);
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task From_ExplicitFileScheme_RoutesToFilePath()
    {
        var (app, ctx) = MakeApp();
        var p = app.Types.Scheme.From("file:///home/user/x.txt", ctx);
        await Assert.That(p is FilePath).IsTrue();
        await Assert.That(p.Scheme).IsEqualTo("file");
    }

    [Test] public async Task From_SchemeMatching_IsCaseInsensitive()
    {
        var (app, ctx) = MakeApp();
        // Built-in "file" registered lowercase; FILE:// must resolve to it.
        var p = app.Types.Scheme.From("FILE:///x.txt", ctx);
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task From_UnknownScheme_ThrowsTypedSchemeNotRegistered()
    {
        var (app, ctx) = MakeApp();
        var ex = await Assert.That(() => app.Types.Scheme.From("s3://bucket/key", ctx)).Throws<SchemeNotRegistered>();
        await Assert.That(ex!.Scheme).IsEqualTo("s3");
    }

    [Test] public async Task MultiApp_Registrations_AreIsolated()
    {
        var (a, _) = MakeApp();
        var (b, ctxB) = MakeApp();
        a.Types.Scheme.Register("zzz", (raw, c) => new FilePath(raw, c));
        await Assert.That(a.Types.Scheme.IsRegistered("zzz")).IsTrue();
        await Assert.That(b.Types.Scheme.IsRegistered("zzz")).IsFalse();
        await Assert.That(() => b.Types.Scheme.From("zzz://x", ctxB)).Throws<SchemeNotRegistered>();
    }

    [Test] public async Task SchemeRegistry_ExposedAt_AppTypesScheme()
    {
        var (app, _) = MakeApp();
        var r1 = app.Types.Scheme;
        var r2 = app.Types.Scheme;
        await Assert.That(r1).IsNotNull();
        await Assert.That(object.ReferenceEquals(r1, r2)).IsTrue();
    }
}
