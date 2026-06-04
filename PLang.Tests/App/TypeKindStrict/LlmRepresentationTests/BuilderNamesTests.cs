using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Prim = global::app.type.primitive.@this;

namespace PLang.Tests.App.TypeKindStrict.LlmRepresentationTests;

public class BuilderNamesTests
{
    [Test] public async Task BuilderNames_AreTheFundamentalVocabulary()
    {
        // The builder vocabulary is the explicit fundamental set — inline +
        // reference — not every registry alias.
        foreach (var f in Prim.InlineFundamentals.Concat(Prim.ReferenceFundamentals))
            await Assert.That(Prim.BuilderNames.Contains(f)).IsTrue();
    }

    [Test] public async Task BuilderNames_IncludesText()
        => await Assert.That(Prim.BuilderNames).Contains("text");

    [Test] public async Task BuilderNames_MediaAndPathAreFirstClass()
    {
        // Reference fundamentals are always-on names, not buried in the
        // format-family kinds — this is what grounds a developer's `as image`.
        await Assert.That(Prim.BuilderNames).Contains("image");
        await Assert.That(Prim.BuilderNames).Contains("video");
        await Assert.That(Prim.BuilderNames).Contains("audio");
        await Assert.That(Prim.BuilderNames).Contains("path");
    }

    [Test] public async Task BuilderNames_ExcludesNumericPrimitivesAndStringAlias()
    {
        await Assert.That(Prim.BuilderNames).DoesNotContain("string");
        await Assert.That(Prim.BuilderNames).DoesNotContain("int");
        await Assert.That(Prim.BuilderNames).DoesNotContain("long");
        await Assert.That(Prim.BuilderNames).DoesNotContain("decimal");
        await Assert.That(Prim.BuilderNames).DoesNotContain("double");
        await Assert.That(Prim.BuilderNames).DoesNotContain("float");
    }
}
