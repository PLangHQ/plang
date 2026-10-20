namespace PLang.Tests.App.Types;

/// <summary>A set a choice draws on says so on its own class (<c>[Choice]</c>): the types read the marked classes when
/// an app is born, never every property of every class (a choice over an unmarked class is PLNG005, at build).</summary>
public class ChoiceMarkTests
{
    [Test]
    public async Task AMarkedSet_IsAKindOfChoice_WithItsOptions()
    {
        await using var app = global::PLang.Tests.Session.App("/test").Testing();

        var choice = app.type.list["choice"];

        await Assert.That(choice.kind["httpmethod"]).IsTypeOf<global::app.type.item.choice.set.member.@this>();
        await Assert.That(choice.kind["httpmethod"]!.Values!).Contains("POST");
    }
}
