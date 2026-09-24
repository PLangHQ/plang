using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Prim = global::app.type.primitive.@this;

namespace PLang.Tests.App.TypeKindStrict.PrimitiveTableTests;

// Primitive table flip: text canonical for string; int/long/decimal/double/float
// canonical to number; BuilderNames trimmed (text in, string/numerics out).
public class PrimitiveTableTests
{
#pragma warning disable CS0618
    private static readonly global::app.type.list.view.@this View = new(null!);
#pragma warning restore CS0618
    private static readonly Prim Table = new();

    [Test] public async Task Canonical_StringMapsToText()
        => await Assert.That(Table.Canonical[typeof(string)]).IsEqualTo("text");

    [Test] public async Task Aliases_StringStillResolves()
        => await Assert.That(Table.Aliases["string"]).IsEqualTo(typeof(string));

    [Test] public async Task Aliases_TextStillResolves()
        => await Assert.That(Table.Aliases["text"]).IsEqualTo(typeof(string));

    [Test] public async Task BuilderNames_IncludesText()
        => await Assert.That(View.BuilderNames).Contains("text");

    [Test] public async Task BuilderNames_ExcludesString()
        => await Assert.That(View.BuilderNames).DoesNotContain("string");

    [Test] public async Task BuilderNames_ExcludesIntLongDecimalDouble()
    {
        await Assert.That(View.BuilderNames).DoesNotContain("int");
        await Assert.That(View.BuilderNames).DoesNotContain("long");
        await Assert.That(View.BuilderNames).DoesNotContain("decimal");
        await Assert.That(View.BuilderNames).DoesNotContain("double");
    }

    [Test] public async Task Canonical_IntLongDecimalDouble_MapToNumber()
    {
        await Assert.That(Table.Canonical[typeof(int)]).IsEqualTo("number");
        await Assert.That(Table.Canonical[typeof(long)]).IsEqualTo("number");
        await Assert.That(Table.Canonical[typeof(decimal)]).IsEqualTo("number");
        await Assert.That(Table.Canonical[typeof(double)]).IsEqualTo("number");
    }

    [Test] public async Task Canonical_FloatMapsToNumber()
        => await Assert.That(Table.Canonical[typeof(float)]).IsEqualTo("number");
}
