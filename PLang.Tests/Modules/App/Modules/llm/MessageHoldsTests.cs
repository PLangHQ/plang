using app.module.llm;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// An llm query's messages hold what the step quoted: a list of messages whose content holds the quoted text holds it,
/// by the content's own characters — quotes and all — never only by the escaped form the list writes.
/// </summary>
public class MessageHoldsTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/tmp/message-holds-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private const string Said = "Return a JSON object with key \"color\" set to \"blue\"";

    [Test]
    public async Task AListOfMessages_HoldsTheQuotedTextInAContent()
    {
        var messages = new global::app.type.item.list.@this<LlmMessage>(new global::app.type.item.@this[]
        {
            new LlmMessage { Role = "system", Content = "Reply in JSON format only" },
            new LlmMessage { Role = "user", Content = Said },
        }, _app.type.list[typeof(global::app.type.item.list.@this<LlmMessage>)]);

        await Assert.That(await messages.Holds(Said, Ctx)).IsTrue();
        await Assert.That(await messages.Holds("set to \"red\"", Ctx)).IsFalse();
    }
}
