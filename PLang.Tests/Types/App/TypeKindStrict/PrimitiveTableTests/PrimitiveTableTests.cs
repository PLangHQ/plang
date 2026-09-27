using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.type.listKindStrict.PrimitiveTableTests;

// text owns "string" as its alias; BuilderNames trimmed (text in, string/numerics out).
public class PrimitiveTableTests
{
#pragma warning disable CS0618
    private static readonly global::app.type.list.view.@this View = new(null!);
#pragma warning restore CS0618
    private static global::app.type.list.@this Types => global::PLang.Tests.TestApp.SharedContext.App.type.list;

    [Test] public async Task String_NamesText()
        => await Assert.That(Types["string"].Name).IsEqualTo("text");

    [Test] public async Task Text_OwnsStringAlias()
        => await Assert.That(Types["text"].Alias).Contains("string");

    [Test] public async Task Text_Resolves()
        => await Assert.That(Types.Clr("text")).IsEqualTo(typeof(global::app.type.item.text.@this));

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
}
