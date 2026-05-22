using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 6 — <c>Path.Absolute</c> becomes a scheme-defined canonical form. Two Path
/// instances pointing at the same logical resource must produce the same <c>Absolute</c>
/// string; Permission grants and requests match on it.
///
/// <c>FilePath.Absolute</c> is unchanged (OS-normalized). <c>HttpPath.Absolute</c> applies
/// six canonical-form rules. Permission code itself does not branch on scheme — it reads
/// <c>path.Absolute</c> as a string. Each rule below is pinned as one test so a future
/// permission-bypass from a forgotten rule fails loudly.
/// </summary>
public class AbsoluteCanonicalFormTests
{
    /// <summary>Intent: <c>FilePath.Absolute</c> is unchanged from today — an OS-normalized
    /// absolute path. <c>/home/x/../y/z.txt</c> canonicalizes to <c>/home/y/z.txt</c>.
    /// Existing FilePath permission grants stay valid (formula unchanged).</summary>
    [Test] public async Task FilePath_Absolute_Unchanged_OsNormalized()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: rule 1 — scheme and host are lowercased.
    /// <c>HTTP://Example.COM/foo</c> → <c>http://example.com/foo</c>. The path is
    /// case-preserved (only scheme + host lowercase).</summary>
    [Test] public async Task HttpPath_Absolute_LowercasesSchemeAndHost()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: rule 2a — the default HTTPS port is stripped.
    /// <c>https://example.com:443/foo</c> → <c>https://example.com/foo</c>.</summary>
    [Test] public async Task HttpPath_Absolute_StripsDefaultHttpsPort()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: rule 2b — the default HTTP port is stripped.
    /// <c>http://example.com:80/foo</c> → <c>http://example.com/foo</c>.</summary>
    [Test] public async Task HttpPath_Absolute_StripsDefaultHttpPort()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: rule 2c — a non-default port is preserved.
    /// <c>https://example.com:8443/foo</c> stays <c>https://example.com:8443/foo</c>.
    /// Pins that port-stripping is default-only, not blanket.</summary>
    [Test] public async Task HttpPath_Absolute_KeepsNonDefaultPort()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: rule 3 — path segments are normalized.
    /// <c>https://example.com/a/../b</c> → <c>https://example.com/b</c>.</summary>
    [Test] public async Task HttpPath_Absolute_NormalizesPathSegments()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: rule 4 — trailing-slash policy on the root. <c>https://example.com</c>
    /// and <c>https://example.com/</c> produce the SAME canonical form (the form WITH the
    /// trailing slash on the root). Two spellings of the root must not yield two distinct
    /// permission keys.</summary>
    [Test] public async Task HttpPath_Absolute_RootWithAndWithoutTrailingSlash_AreEqual()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: rule 5 — query parameters are sorted by key lexicographically.
    /// <c>https://example.com/?b=2&amp;a=1</c> → <c>https://example.com/?a=1&amp;b=2</c>.
    /// Duplicate keys keep their original relative order within the same key.</summary>
    [Test] public async Task HttpPath_Absolute_SortsQueryParameters_ByKey()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: rule 6 — the fragment is stripped. <c>https://example.com/foo#bar</c>
    /// → <c>https://example.com/foo</c>. Fragments are client-side and do not address a
    /// distinct server resource.</summary>
    [Test] public async Task HttpPath_Absolute_StripsFragment()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: a FilePath permission grant never matches an HttpPath request. The
    /// two canonical forms share no prefix (<c>/home/...</c> vs <c>https://...</c>), so a
    /// grant for one scheme cannot satisfy a request on another. Cross-scheme isolation by
    /// construction — no scheme-aware code in Permission required.</summary>
    [Test] public async Task Permission_FilePathGrant_DoesNotMatch_HttpPathRequest()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: a Glob HttpPath grant <c>https://api.example.com/*</c> matches a
    /// request for <c>https://api.example.com/users</c> — the glob matcher walks the
    /// canonical-form string with no scheme awareness. Confirms URL grants compose with
    /// the existing Match modes.</summary>
    [Test] public async Task Permission_HttpPathGlobGrant_MatchesUrlUnderHost()
    {
        Assert.Fail("Not implemented");
    }
}
