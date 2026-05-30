using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.PrimitiveTableTests;

// `app.type.primitive.@this` table changes:
//   1. Canonical[typeof(string)] flips from "string" to "text".
//   2. Aliases keep both "string" and "text" → typeof(string) (back-compat input).
//   3. BuilderNames picks "text" (not "string") and excludes int/long/decimal/double
//      (they now surface only as `number` kinds).
//   4. Canonical[typeof(int)/long/decimal/double/float] all map to "number".

public class PrimitiveTableTests
{
    [Test] public async Task Canonical_StringMapsToText()
    {
        // primitive.@this.Canonical[typeof(string)] == "text". This is the global
        // pivot — every string value renders as text on the wire / in navigation.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Aliases_StringStillResolves()
    {
        // primitive.@this.Aliases["string"] == typeof(string). Back-compat for any
        // input still naming "string" (e.g. legacy .pr files, hand-written goals).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Aliases_TextStillResolves()
    {
        // primitive.@this.Aliases["text"] == typeof(string). "text" is the canonical
        // input now too; "string" remains accepted (the alias table holds both).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task BuilderNames_IncludesText()
    {
        // primitive.@this.BuilderNames contains "text". The LLM-facing list of
        // primitive type names now offers text as a top-level choice.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task BuilderNames_ExcludesString()
    {
        // primitive.@this.BuilderNames does NOT contain "string". The LLM stops
        // seeing "string" as a top-level name (alias resolution still accepts it
        // on input; only the rendered list drops it).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task BuilderNames_ExcludesIntLongDecimalDouble()
    {
        // primitive.@this.BuilderNames does NOT contain "int", "long", "decimal",
        // "double". They surface only as `number` kinds (kinds: int | long | …)
        // so they stop competing with `number` in the LLM's name list.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Canonical_IntLongDecimalDouble_MapToNumber()
    {
        // primitive.@this.Canonical[typeof(int)] == "number" (and long/decimal/double
        // same). Numeric inference produces {number, <kind>} everywhere, agreeing
        // with what number.Build returns from a literal.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Canonical_FloatMapsToNumber()
    {
        // primitive.@this.Canonical[typeof(float)] == "number". float still aliases
        // to its own kind ("float"? "double"? — coder picks; pin Name=="number").
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
