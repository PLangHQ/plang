using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Prim = global::app.type.primitive.@this;

namespace PLang.Tests.App.TypeKindStrict.LlmRepresentationTests;

public class BuilderNamesTests
{
#pragma warning disable CS0618
    private static readonly global::app.type.list.view.@this View = new(null!);
#pragma warning restore CS0618
    private static readonly Prim Table = new();

    [Test] public async Task BuilderNames_AreTheFundamentalVocabulary()
    {
        // The builder vocabulary is the explicit fundamental set — inline +
        // reference — not every registry alias.
        foreach (var f in View.InlineFundamentals.Concat(View.ReferenceFundamentals))
            await Assert.That(View.BuilderNames.Contains(f)).IsTrue();
    }

    [Test] public async Task BuilderNames_IncludesText()
        => await Assert.That(View.BuilderNames).Contains("text");

    [Test] public async Task BuilderNames_MediaAndPathAreFirstClass()
    {
        // Reference fundamentals are always-on names, not buried in the
        // format-family kinds — this is what grounds a developer's `as image`.
        await Assert.That(View.BuilderNames).Contains("image");
        await Assert.That(View.BuilderNames).Contains("video");
        await Assert.That(View.BuilderNames).Contains("audio");
        await Assert.That(View.BuilderNames).Contains("path");
    }

    [Test] public async Task BuilderNames_ExcludesNumericPrimitivesAndStringAlias()
    {
        await Assert.That(View.BuilderNames).DoesNotContain("string");
        await Assert.That(View.BuilderNames).DoesNotContain("int");
        await Assert.That(View.BuilderNames).DoesNotContain("long");
        await Assert.That(View.BuilderNames).DoesNotContain("decimal");
        await Assert.That(View.BuilderNames).DoesNotContain("double");
        await Assert.That(View.BuilderNames).DoesNotContain("float");
    }
}
