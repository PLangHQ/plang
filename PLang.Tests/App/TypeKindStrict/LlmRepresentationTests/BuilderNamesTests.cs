using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Prim = global::app.type.primitive.@this;

namespace PLang.Tests.App.TypeKindStrict.LlmRepresentationTests;

public class BuilderNamesTests
{
    [Test] public async Task BuilderNames_IsCatalogGenerated_NotHandWritten()
    {
        // Every canonical name is in BuilderNames — the list is derived from
        // Canonical (and Aliases), not a literal source array.
        foreach (var canonical in Prim.Canonical.Values.Distinct())
            await Assert.That(Prim.BuilderNames.Contains(canonical)).IsTrue();
    }

    [Test] public async Task BuilderNames_IncludesText()
        => await Assert.That(Prim.BuilderNames).Contains("text");

    [Test] public async Task BuilderNames_ExcludesNumericPrimitives()
    {
        await Assert.That(Prim.BuilderNames).DoesNotContain("int");
        await Assert.That(Prim.BuilderNames).DoesNotContain("long");
        await Assert.That(Prim.BuilderNames).DoesNotContain("decimal");
        await Assert.That(Prim.BuilderNames).DoesNotContain("double");
        await Assert.That(Prim.BuilderNames).DoesNotContain("float");
    }
}
