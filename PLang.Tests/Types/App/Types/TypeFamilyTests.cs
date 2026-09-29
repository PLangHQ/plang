namespace PLang.Tests.App.Types;

// A kinded type ({text, md}, {choice, operator}) is born holding its family's entry and reads the family's facts
// through it — one set of facts per class; a set of options answers its own values.
public class TypeFamilyTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    [Test] public async Task AKindedType_ReadsItsFamilysFacts()
    {
        var text = Ctx.App.type.list["text"];
        var markdown = Ctx.App.type.list[new global::app.type.@this("text", "md"), Ctx];

        await Assert.That(markdown.kind.Name).IsEqualTo("md");
        await Assert.That(markdown.Description).IsEqualTo(text.Description);
        await Assert.That(markdown.Alias).IsSameReferenceAs(text.Alias);
        await Assert.That(markdown.Namespace).IsEqualTo(text.Namespace);
    }

    [Test] public async Task AChoiceKind_AnswersItsSetsValues()
    {
        var operators = Ctx.App.type.list[new global::app.type.@this("choice", "operator"), Ctx];

        await Assert.That(operators.Values).IsNotNull();
        await Assert.That(operators.Values!).IsEquivalentTo(operators.kind.Values!);
        await Assert.That(operators.Values!.Count).IsGreaterThan(0);
    }
}
