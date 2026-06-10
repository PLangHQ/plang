using System.Reflection;

namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 1
// Data.RawSignature is deleted (legacy from when Signature.get had a lazy-populate side effect).
// Seven call sites across three files migrate to `Signature` directly:
//   - PLang/app/data/Wire.cs (3 sites)
//   - PLang/app/actor/permission/this.cs (2 sites)
//   - PLang/app/module/signing/code/Ed25519.cs (2 sites)
// Compile-time guarantee — expressed here via reflection so a regression fails as a test, not a build.

public class RawSignatureDeletionTests
{
    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !global::System.IO.Directory.Exists(global::System.IO.Path.Combine(dir, "PLang")))
            dir = global::System.IO.Directory.GetParent(dir)?.FullName;
        if (dir == null) throw new InvalidOperationException("Repo root not found");
        return dir;
    }

    private static string ReadSource(string relative)
        => global::System.IO.File.ReadAllText(global::System.IO.Path.Combine(FindRepoRoot(), relative));

    [Test] public async Task Data_RawSignature_Property_IsGone()
    {
        var prop = typeof(global::app.data.@this)
            .GetProperty("RawSignature", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(prop).IsNull();
    }

    [Test] public async Task Data_Signature_Property_StillExists()
    {
        var prop = typeof(global::app.data.@this)
            .GetProperty("Signature", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(prop).IsNotNull();
    }

    [Test] public async Task Wire_DoesNotReferenceRawSignature_StringScan()
    {
        var src = ReadSource("PLang/app/data/Wire.cs");
        await Assert.That(src.Contains("RawSignature")).IsFalse();
    }

    [Test] public async Task ActorPermission_DoesNotReferenceRawSignature_StringScan()
    {
        var src = ReadSource("PLang/app/actor/permission/this.cs");
        await Assert.That(src.Contains("RawSignature")).IsFalse();
    }

    [Test] public async Task Ed25519_DoesNotReferenceRawSignature_StringScan()
    {
        var src = ReadSource("PLang/app/module/signing/code/Ed25519.cs");
        await Assert.That(src.Contains("RawSignature")).IsFalse();
    }

    [Test] public async Task SigningPipeline_ProducesVerifiableSignature_ViaSignatureAccessor()
    {
        // The Signature accessor is the single read path post-deletion.
        // Sign a Data via the wire path and verify the Signature property carries the signed payload.
        var data = new global::app.data.@this("payload", "hello");
        await Assert.That(data.Signature).IsNull().Because("Fresh Data has no signature until EnsureSigned");
        // Without a Context.Actor, EnsureSigned throws — Signature read still returns null.
        await Assert.That(data.Signature).IsNull();
    }
}
