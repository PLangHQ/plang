using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 2 — the per-App scheme registry (<c>app.types.path.scheme.@this</c>), reachable as
/// <c>app.Types.Scheme</c>. It maps a scheme string to a <c>Func&lt;string, Path&gt;</c>
/// factory via an internal case-insensitive <c>ConcurrentDictionary</c>, and
/// <c>From(raw)</c> parses the scheme off a raw path and dispatches.
///
/// Construction pattern (mirror existing tests): <c>new global::app.@this(tempDir)</c>;
/// the registry is reached at <c>app.Types.Scheme</c>. Register a lightweight
/// <c>TestScheme : Path</c> subclass for the registration-dispatch tests rather than
/// depending on HttpPath.
///
/// Unknown-scheme policy: <c>From</c> throws a typed exception (e.g.
/// <c>SchemeNotRegistered</c>) — the PLang type-mapper, not the registry, is responsible
/// for shaping that into a <c>data.@this.Fail</c>. The throw-vs-Fail split is tested here
/// (throw) and in <see cref="PathTypeMapperTests"/> (Fail).
/// </summary>
public class SchemeRegistryTests
{
    /// <summary>Intent: <c>Register("test", raw =&gt; new TestScheme(raw))</c> then
    /// <c>From("test://x")</c> returns an instance of the registered subclass.</summary>
    [Test] public async Task Register_ThenFrom_ReturnsRegisteredSubclass()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: registering the same scheme twice replaces the factory — the
    /// second <c>Register</c> wins, <c>From</c> dispatches to the newer factory. Pins the
    /// <c>ConcurrentDictionary</c> indexer-assign semantics.</summary>
    [Test] public async Task Register_SameSchemeTwice_SecondRegistrationReplacesFirst()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: a bare absolute path with no scheme prefix
    /// (<c>/home/user/x.txt</c>) routes to <c>FilePath</c> — the no-scheme default.</summary>
    [Test] public async Task From_BareAbsolutePath_RoutesToFilePath()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: a bare relative path (<c>./relative.txt</c>, <c>sub/file.txt</c>)
    /// routes to <c>FilePath</c> — the scheme parser must not mistake a relative path for
    /// a schemed URI.</summary>
    [Test] public async Task From_BareRelativePath_RoutesToFilePath()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: a Windows-style path (<c>C:\Users\x</c>) routes to
    /// <c>FilePath</c>. The drive-letter colon must NOT be parsed as a scheme separator —
    /// scheme parsing only triggers on the URI <c>scheme://</c> shape.</summary>
    [Test] public async Task From_WindowsDriveLetterPath_RoutesToFilePath_NotSchemeColon()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: an explicit <c>file:///home/user/x.txt</c> routes to
    /// <c>FilePath</c> — the explicitly-schemed file URI resolves the same as a bare
    /// path.</summary>
    [Test] public async Task From_ExplicitFileScheme_RoutesToFilePath()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: scheme matching is case-insensitive — <c>FILE://</c> and
    /// <c>file://</c> resolve to the same handler (registry uses
    /// <c>StringComparer.OrdinalIgnoreCase</c>).</summary>
    [Test] public async Task From_SchemeMatching_IsCaseInsensitive()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: <c>From("s3://bucket/key")</c> with <c>s3</c> unregistered throws
    /// the typed registry exception (e.g. <c>SchemeNotRegistered</c>) — NOT a generic
    /// exception, and NOT a silent null. The type-mapper relies on the type to shape a
    /// clean <c>data.@this.Fail</c>; see <see cref="PathTypeMapperTests"/>.</summary>
    [Test] public async Task From_UnknownScheme_ThrowsTypedSchemeNotRegistered()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: scheme registrations are per-App, not static. Register a scheme on
    /// App A; a separately-constructed App B's registry does not see it
    /// (<c>From</c> on B for that scheme throws unknown-scheme). Pins "no static mutable
    /// state for the registry".</summary>
    [Test] public async Task MultiApp_Registrations_AreIsolated()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: the registry instance is exposed at <c>app.Types.Scheme</c> — a
    /// non-null <c>app.types.path.scheme.@this</c>, and the same instance across repeated
    /// access on one App (a property, not a fresh-each-call factory).</summary>
    [Test] public async Task SchemeRegistry_ExposedAt_AppTypesScheme()
    {
        Assert.Fail("Not implemented");
    }
}
