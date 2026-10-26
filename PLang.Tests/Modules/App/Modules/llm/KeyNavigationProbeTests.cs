namespace PLang.Tests.App.Modules.llm;

/// <summary>Security probe (doc2 finding H): a [Sensitive] member is masked when its owner is written, but a goal that
/// navigates to it (%!llm.setting.key%) gets the text whole. Asserts the SAFE behaviour: red until a navigated key is
/// a sensitive value. The key here is a fake.</summary>
public class KeyNavigationProbeTests
{
    private const string Fake = "fake-key-navprobe-5521";

    private static global::app.type.item.variable.@this Path(global::app.@this app, string path)
        => new global::app.type.item.variable.parser.@this(path, app.type.list).Variable.Single();

    [Test]
    [Arguments("llm.setting.key")]
    [Arguments("llm.decider.setting.key")]
    public async Task ANavigatedKey_IsNotItsPlainText(string address)
    {
        await using var app = new global::app.@this("/tmp/knp-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        context.Setting.Set(address, new global::app.type.item.text.@this(Fake, app.type.list[typeof(global::app.type.item.text.@this)]));

        var read = await Path(app, $"%!{address}%").Start(context);

        var shown = read.Success ? (await read.Value())?.ToString() ?? "" : "";
        await Assert.That(shown).DoesNotContain(Fake);
    }
}
