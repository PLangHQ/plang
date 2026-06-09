using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.IntegrationCutsTests;

public class Cut1_TypedSetRoundTripsKind
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() { _app = new global::app.@this("/app"); }

    // An EXPLICIT kind (`as text/md`) round-trips onto the minted variable. A
    // bare literal's spelling never derives a kind (that's the stage-8 rule) —
    // here the developer declared `md`, so it survives.
    [Test] public async Task SetAsTextMd_DocTypeIsTextWithKindMd()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%doc%"),
            ("value", "readme.md"),
            ("type", new global::app.type.@this("text", "md")));
        var result = await action.RunAsync(context);
        await result.IsSuccess();

        var stored = await context.Variable.Get("doc");
        await Assert.That(stored!.Type!.Name).IsEqualTo("text");
        await Assert.That(stored.Type.Kind).IsEqualTo("md");
        await Assert.That(stored.Kind).IsEqualTo("md");
    }

    [Test] public async Task SetAsTextMd_NavigationResolvesKindFromVariableExpression()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%doc%"),
            ("value", "readme.md"),
            ("type", new global::app.type.@this("text", "md")));
        await action.RunAsync(context);

        // Navigation via the same engine path used by `%doc.Type.Name%` in goal text.
        var name = await (await context.Variable.Get("doc"))!.GetChild("Type.Name");
        var kind = await (await context.Variable.Get("doc"))!.GetChild("Type.Kind");
        await Assert.That((await name.Value())).IsEqualTo("text");
        await Assert.That((await kind.Value())).IsEqualTo("md");
    }
}
