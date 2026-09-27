using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.PrimitiveTableTests;

// text owns "string" as its alias.
public class PrimitiveTableTests
{
    private static global::app.type.list.@this Types => global::PLang.Tests.TestApp.SharedContext.App.type.list;

    [Test] public async Task String_NamesText()
        => await Assert.That(Types["string"].Name).IsEqualTo("text");

    [Test] public async Task Text_OwnsStringAlias()
        => await Assert.That(Types["text"].Alias).Contains("string");

    [Test] public async Task Text_Resolves()
        => await Assert.That(Types.Clr("text")).IsEqualTo(typeof(global::app.type.item.text.@this));
}
