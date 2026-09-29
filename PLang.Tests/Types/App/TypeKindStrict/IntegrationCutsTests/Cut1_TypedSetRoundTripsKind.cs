using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.IntegrationCutsTests;

public class Cut1_TypedSetRoundTripsKind
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() { _app = new global::app.@this("/app").Testing(); }

    // An EXPLICIT kind (`as text/md`) round-trips onto the minted variable. A
    // bare literal's spelling never derives a kind (that's the stage-8 rule) —
    // here the developer declared `md`, so it survives.
    [Test] public async Task SetAsTextMd_DocTypeIsTextWithKindMd()
    {
        var context = _app.actor.list.User.Context;
        var action = global::PLang.Tests.Shared.Make.Action(context, "variable", "set",
            global::PLang.Tests.Shared.Make.Param(context, "Name", "%doc%", "variable"),
            ("value", "readme.md"),
            ("type", new global::app.type.@this("text", "md")));
        var result = await action.Start(context);
        await result.IsSuccess();

        var stored = await context.Variable.Get("doc");
        await Assert.That(stored!.Type!.Name).IsEqualTo("text");
        await Assert.That(stored.Type.kind.Name).IsEqualTo("md");
        await Assert.That(stored.Kind).IsEqualTo("md");
    }

    [Test] public async Task SetAsTextMd_NavigationResolvesKindFromVariableExpression()
    {
        var context = _app.actor.list.User.Context;
        var action = global::PLang.Tests.Shared.Make.Action(context, "variable", "set",
            global::PLang.Tests.Shared.Make.Param(context, "Name", "%doc%", "variable"),
            ("value", "readme.md"),
            ("type", new global::app.type.@this("text", "md")));
        await action.Start(context);

        // A value's type is on the property plane (`%doc!type%`); `.` is its content.
        var type = await (await context.Variable.Get("doc"))!.Get("!type");
        var entity = (global::app.type.@this)(await type.Value())!;
        await Assert.That(entity.Name).IsEqualTo("text");
        await Assert.That(entity.kind.Name).IsEqualTo("md");
    }
}
