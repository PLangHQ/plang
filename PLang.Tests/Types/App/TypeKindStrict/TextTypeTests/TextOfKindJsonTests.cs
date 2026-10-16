using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TextType = global::app.type.item.text.@this;

namespace PLang.Tests.App.TypeKindStrict.TextTypeTests;

// text-of-kind-json is retired (decision 773: json is item's kind, not text's) — a json value is {item, json},
// covered by JsonParsesOnceTests / JsonArrayForeachTests / JsonMalformedMaterializeFailedTests. What stays here
// is the plain-text case: a plain text (no kind) is not navigable.
public class TextOfKindJsonTests
{
    [Test] public async Task APlainText_StillCantBeNavigated()
    {
        await using var app = new global::app.@this("/app").Testing();
        var data = new global::app.data.@this("x", new TextType("{\"a\": 1}"), context: app.actor.list.User.Context);

        var a = await data.Get("a");

        await a.IsFailure();
        await Assert.That(a.Error!.Key).IsEqualTo("CantNavigateText");
    }
}
