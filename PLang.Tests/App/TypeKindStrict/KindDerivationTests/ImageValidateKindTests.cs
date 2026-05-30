using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.KindDerivationTests;

// `image.@this` implements IKindValidatable: byte-sniffs the image bytes
// to confirm the actual format matches the required kind.
// Reuses the ImageSharp `Identify` path used for Width/Height.

public class ImageValidateKindTests
{
    [Test] public async Task ValidateKind_GifBytesMatchingGif_OkTrue()
    {
        // image.ValidateKind(<real GIF bytes>, "gif") → (ok:true, actualKind:"gif").
        // Construct a minimal valid GIF (or load a fixture committed with the test).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task ValidateKind_PngBytesMatchingGif_OkFalse_ActualIsPng()
    {
        // image.ValidateKind(<real PNG bytes>, "gif") → (ok:false, actualKind:"png").
        // The canonical mismatch row. Strict callers use this to report the actual
        // kind in the error ("expected gif, got png").
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task ValidateKind_GarbageBytes_OkFalse()
    {
        // image.ValidateKind(new byte[]{0,1,2,3}, "gif") → (ok:false, actualKind:null).
        // Not a recognisable image format; ok:false, actualKind null (or empty).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task ValidateKind_EmptyBytes_OkFalse()
    {
        // image.ValidateKind(Array.Empty<byte>(), "gif") → (ok:false, actualKind:null).
        // Empty input; no probe possible; ok:false.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
