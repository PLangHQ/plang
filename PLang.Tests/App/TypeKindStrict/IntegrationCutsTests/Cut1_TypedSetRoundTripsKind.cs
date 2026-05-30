using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.IntegrationCutsTests;

public class Cut1_TypedSetRoundTripsKind
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() { _app = new global::app.@this("/app"); }

    [Test] public async Task SetReadmeMdAsText_DocTypeIsTextWithKindMd()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%doc%"),
            ("value", "readme.md"),
            ("type", new global::app.type.@this("text")));
        var result = await action.RunAsync(context);
        await result.IsSuccess();

        var stored = context.Variable.Get("doc");
        await Assert.That(stored!.Type!.Name).IsEqualTo("text");
        await Assert.That(stored.Type.Kind).IsEqualTo("md");
        await Assert.That(stored.Kind).IsEqualTo("md");
    }

    [Test] public async Task SetReadmeMdAsText_NavigationResolvesKindFromVariableExpression()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%doc%"),
            ("value", "readme.md"),
            ("type", new global::app.type.@this("text")));
        await action.RunAsync(context);

        // Navigation via the same engine path used by `%doc.Type.Name%` in goal text.
        var name = context.Variable.Get("doc")!.GetChild("Type.Name");
        var kind = context.Variable.Get("doc")!.GetChild("Type.Kind");
        await Assert.That(name.Value).IsEqualTo("text");
        await Assert.That(kind.Value).IsEqualTo("md");
    }
}
