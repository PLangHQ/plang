using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Prim = global::app.type.primitive.@this;

namespace PLang.Tests.App.TypeKindStrict.PrimitiveTableTests;

// Primitive table flip: text canonical for string; int/long/decimal/double/float
// canonical to number; BuilderNames trimmed (text in, string/numerics out).
public class PrimitiveTableTests
{
    [Test] public async Task Canonical_StringMapsToText()
        => await Assert.That(Prim.Canonical[typeof(string)]).IsEqualTo("text");

    [Test] public async Task Aliases_StringStillResolves()
        => await Assert.That(Prim.Aliases["string"]).IsEqualTo(typeof(string));

    [Test] public async Task Aliases_TextStillResolves()
        => await Assert.That(Prim.Aliases["text"]).IsEqualTo(typeof(string));

    [Test] public async Task BuilderNames_IncludesText()
        => await Assert.That(Prim.BuilderNames).Contains("text");

    [Test] public async Task BuilderNames_ExcludesString()
        => await Assert.That(Prim.BuilderNames).DoesNotContain("string");

    [Test] public async Task BuilderNames_ExcludesIntLongDecimalDouble()
    {
        await Assert.That(Prim.BuilderNames).DoesNotContain("int");
        await Assert.That(Prim.BuilderNames).DoesNotContain("long");
        await Assert.That(Prim.BuilderNames).DoesNotContain("decimal");
        await Assert.That(Prim.BuilderNames).DoesNotContain("double");
    }

    [Test] public async Task Canonical_IntLongDecimalDouble_MapToNumber()
    {
        await Assert.That(Prim.Canonical[typeof(int)]).IsEqualTo("number");
        await Assert.That(Prim.Canonical[typeof(long)]).IsEqualTo("number");
        await Assert.That(Prim.Canonical[typeof(decimal)]).IsEqualTo("number");
        await Assert.That(Prim.Canonical[typeof(double)]).IsEqualTo("number");
    }

    [Test] public async Task Canonical_FloatMapsToNumber()
        => await Assert.That(Prim.Canonical[typeof(float)]).IsEqualTo("number");
}
