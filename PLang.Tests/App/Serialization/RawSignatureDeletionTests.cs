using System.Reflection;

namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 1
// Data.RawSignature is deleted (legacy from when Signature.get had a lazy-populate side effect).
// Seven call sites across three files migrate to `Signature` directly:
//   - PLang/app/data/WireJsonConverter.cs (3 sites)
//   - PLang/app/actor/permission/this.cs (2 sites)
//   - PLang/app/modules/signing/code/Ed25519.cs (2 sites)
// Compile-time guarantee — expressed here via reflection so a regression fails as a test, not a build.

public class RawSignatureDeletionTests
{
    [Test] public async Task Data_RawSignature_Property_IsGone()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Data_Signature_Property_StillExists()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task WireJsonConverter_DoesNotReferenceRawSignature_StringScan()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task ActorPermission_DoesNotReferenceRawSignature_StringScan()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Ed25519_DoesNotReferenceRawSignature_StringScan()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task SigningPipeline_ProducesVerifiableSignature_ViaSignatureAccessor()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
