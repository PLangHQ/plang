using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.LlmRepresentationTests;

// `BuilderNames` is the list of primitive type names the LLM is told it can
// emit. It must be catalog-generated (so renaming the canonical map is enough
// to update what the LLM sees) and reflect the text/number consolidation.

public class BuilderNamesTests
{
    [Test] public async Task BuilderNames_IsCatalogGenerated_NotHandWritten()
    {
        // The list comes from primitive.@this.Canonical (or its equivalent),
        // not a literal `["string","int","long",...]` in source. Probe by
        // confirming the list is derived from Canonical.Values.Distinct() — or
        // a comparable shape — rather than a const array.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task BuilderNames_IncludesText()
    {
        // Mirrors PrimitiveTableTests.BuilderNames_IncludesText but framed at
        // the LLM surface — what the LLM sees when picking a type name.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task BuilderNames_ExcludesNumericPrimitives()
    {
        // int/long/decimal/double do not appear in the LLM's name list; they
        // surface only as kinds of `number`.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
