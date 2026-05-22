using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// Stage 7 — cross-scheme transfer. Lives outside the generic contract base because it
/// needs two fixtures (two schemes) at once. Proves the base-class default
/// <c>CopyTo</c>/<c>MoveTo</c> (ReadBytes → WriteBytes; CopyTo + Delete) work when source
/// and destination are different schemes — the case that makes "copy a file to an HTTP
/// endpoint" just work with no scheme-pair-specific code.
///
/// Each test mints one Path from a <see cref="FilePathFixture"/> and one from a
/// <see cref="HttpPathFixture"/>, and tears both down in a <c>finally</c>.
/// </summary>
public class CrossSchemeTests
{
    /// <summary>Intent: <c>FilePath.CopyTo(HttpPath)</c> uses the base default
    /// (ReadBytes + WriteBytes). After the copy, <c>httpDst.ReadText()</c> equals the
    /// FilePath source content, and the source FilePath still exists.</summary>
    [Test] public async Task CopyTo_FilePath_To_HttpPath_UsesBaseDefault_RoundTrips()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: the reverse direction — <c>HttpPath.CopyTo(FilePath)</c> — also
    /// uses the base default and round-trips. Confirms the default is direction-agnostic.</summary>
    [Test] public async Task CopyTo_HttpPath_To_FilePath_UsesBaseDefault_RoundTrips()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: <c>FilePath.MoveTo(HttpPath)</c> is CopyTo + Delete across schemes
    /// — after the move the HttpPath holds the content and the source FilePath no longer
    /// <c>Exists</c>.</summary>
    [Test] public async Task MoveTo_FilePath_To_HttpPath_CopiesThenDeletesSource()
    {
        Assert.Fail("Not implemented");
    }
}
