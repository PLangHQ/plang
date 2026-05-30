using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.SetAndStrictTests;

// Regression guard for the dropped-kind bug: the minted variable's Type must
// carry the kind end-to-end (built, stamped, minted, visible via navigation).
// `variable.set.Run` writes `minted.Type = Type.Value` — the WHOLE type, not
// just the name — so the kind survives by construction.

public class SetMintCarriesKindTests
{
    [Test] public async Task Run_BareSetWithLiteralReadmeMd_MintTypeIsTextMd()
    {
        // `- set %doc% = "readme.md"` (no `as`). The build-time inference + Build
        // hook produces {text, md}; mint preserves both Name and Kind.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Run_SetAsTextWithReadmeMd_MintTypeIsTextMd()
    {
        // `- set %doc% = "readme.md" as text`. Explicit `as text`; kind derived
        // from extension by text.Build. Mint type is {text, md}.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Run_SetAsImageGifWithGifBytes_MintTypeIsImageGif()
    {
        // `- set %img% = "real.gif" as image/gif`. Mint type is {image, gif}.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
