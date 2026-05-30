using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.KindDerivationTests;

// The kind canonicaliser. Derived from the formats registry (NOT
// hand-written): subtype ↔ extension map gets inverted, primary picked on
// shared subtypes (.jpg/.jpeg → "jpg"), unknown free strings pass through.
// Runs on the build/LLM-facing path (via the type factory); runtime reads
// the already-canonical kind.

public class KindCanonicalisationTests
{
    [Test] public async Task Canonicalise_Markdown_ToMd()
    {
        // Canonicalise("markdown") → "md". The MIME subtype name normalises to
        // the canonical extension.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Canonicalise_Jpeg_ToJpg()
    {
        // Canonicalise("jpeg") → "jpg". Shared-subtype case: .jpg/.jpeg both map
        // to image/jpeg; primary extension is "jpg".
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Canonicalise_UnknownFrobnicate_PassesThrough()
    {
        // Canonicalise("frobnicate") → "frobnicate". No registry entry; pass through
        // unchanged (free string).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Canonicalise_SharedSubtypePicksPrimary()
    {
        // Both ".jpg" and ".jpeg" map to image/jpeg in the registry. The canonicaliser
        // picks the primary (shorter / alphabetically-first / first-registered —
        // coder picks; pin "jpg" as the primary).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Canonicalise_NullInput_ReturnsNull()
    {
        // Canonicalise(null) → null. Null-safety; the factory passes Kind through
        // canonicalisation regardless of whether the caller supplied one.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Canonicalise_AliasTableDerived_NotHandWritten()
    {
        // The alias table is derived from the formats registry, not a literal map
        // in source. Probe: pick a registry entry not in the examples above
        // (e.g. add a new MIME at test setup), and confirm its subtype gets a fresh
        // canonicalisation entry without the canonicaliser being edited. (If the
        // table is hand-written, this test fails.)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
