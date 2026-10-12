namespace PLang.Tests.App.Types;

/// <summary>
/// A value with rows holds what any of them holds — the build's check that an answer kept what the step said: a dict
/// <c>{token: 50}</c> holds the step's 50, and one that holds no "zebra" says so.
/// </summary>
public class ContainerHoldsTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/tmp/container-holds-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private async Task<global::app.type.item.@this> Dict(Dictionary<string, object?> entries)
        => (await new global::app.data.@this("", entries, context: Ctx).Value())!;

    [Test]
    public async Task ADict_HoldsANumberInAnEntry()
    {
        var limit = await Dict(new() { ["token"] = 50L });

        await Assert.That(await limit.Holds((global::app.type.item.number.@this)50d, Ctx)).IsTrue();
    }

    [Test]
    public async Task ADict_HoldsATextInAnEntry_ByItsCharacters()
    {
        var said = await Dict(new() { ["say"] = "key \"color\" set to \"blue\"" });

        await Assert.That(await said.Holds("key \"color\" set to \"blue\"", Ctx)).IsTrue();
    }

    [Test]
    public async Task ADict_ThatHoldsNoZebra_SaysSo()
    {
        var limit = await Dict(new() { ["token"] = 50L, ["name"] = "box" });

        await Assert.That(await limit.Holds("zebra", Ctx)).IsFalse();
        await Assert.That(await limit.Holds((global::app.type.item.number.@this)5d, Ctx)).IsFalse();
    }
}
