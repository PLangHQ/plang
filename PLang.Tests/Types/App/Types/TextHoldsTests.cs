using text = global::app.type.item.text.@this;

namespace PLang.Tests.App.Types;

/// <summary>
/// A text holds what a step quoted by its own characters, whatever kind they are in — the build's check that an answer
/// kept what the step said. A json text holding <c>{"size": 10, "name": "box"}</c> holds that quoted literal, not only
/// the escaped form a writer gives it.
/// </summary>
public class TextHoldsTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = global::PLang.Tests.Session.App("/tmp/text-holds-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    [Test]
    public async Task AJsonText_HoldsTheQuotedLiteral_ByItsCharacters()
    {
        const string literal = """{"size": 10, "name": "box"}""";
        var json = Ctx.Text(literal);

        await Assert.That(await json.Holds(literal, Ctx)).IsTrue();
    }

    [Test]
    public async Task APlainText_HoldsItsCharacters_AndNothingElse()
    {
        var plain = Ctx.Text("Hello world");

        await Assert.That(await plain.Holds("world", Ctx)).IsTrue();
        await Assert.That(await plain.Holds("planet", Ctx)).IsFalse();
    }
}
