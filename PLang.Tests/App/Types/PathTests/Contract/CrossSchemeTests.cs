using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Threading;

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// Cross-scheme transfer. Proves the base-class default
/// CopyTo/MoveTo (ReadBytes → WriteBytes; CopyTo + Delete) work across schemes.
/// </summary>
public class CrossSchemeTests
{
    private static void Authorize(global::app.types.path.@this p)
        => p.Context!.Actor!.Channels.Register(new CannedAnswerChannel("a"));

    [Test] public async Task CopyTo_FilePath_To_HttpPath_UsesBaseDefault_RoundTrips()
    {
        using var fileFx = new FilePathFixture();
        using var httpFx = new HttpPathFixture();
        var src = await fileFx.CreateFresh();
        var dst = await httpFx.CreateFresh();
        Authorize(src);
        Authorize(dst);

        await src.WriteText("cross hello");
        var copied = await src.CopyTo(dst, overwrite: true, includeSubfolders: true);
        await Assert.That(copied.Success).IsTrue();
        var read = await dst.ReadText();
        await Assert.That(read.Value).IsEqualTo("cross hello");
    }

    [Test] public async Task CopyTo_HttpPath_To_FilePath_UsesBaseDefault_RoundTrips()
    {
        using var fileFx = new FilePathFixture();
        using var httpFx = new HttpPathFixture();
        var src = await httpFx.CreateFresh();
        var dst = await fileFx.CreateFresh();
        Authorize(src);
        Authorize(dst);

        await src.WriteText("reverse hello");
        var copied = await src.CopyTo(dst, overwrite: true, includeSubfolders: true);
        await Assert.That(copied.Success).IsTrue();
        var read = await dst.ReadText();
        await Assert.That(read.Value).IsEqualTo("reverse hello");
    }

    [Test] public async Task MoveTo_FilePath_To_HttpPath_CopiesThenDeletesSource()
    {
        using var fileFx = new FilePathFixture();
        using var httpFx = new HttpPathFixture();
        var src = await fileFx.CreateFresh();
        var dst = await httpFx.CreateFresh();
        Authorize(src);
        Authorize(dst);

        await src.WriteText("move cross");
        var moved = await src.MoveTo(dst, overwrite: true);
        await Assert.That(moved.Success).IsTrue();
        var read = await dst.ReadText();
        await Assert.That(read.Value).IsEqualTo("move cross");
        var srcGone = await src.ExistsAsync();
        await Assert.That(srcGone.Value).IsEqualTo(false);
    }
}
