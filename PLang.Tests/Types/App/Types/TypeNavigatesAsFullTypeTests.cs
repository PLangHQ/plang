namespace PLang.Tests.App.Types;

/// <summary>
/// `%x!type%` is a bare type object — name and kind, no context. Navigating into it answers as
/// its full type from `app.type`, found with the asker's context: the facts (description, a
/// choice's options) come from the registry, the identity (kind) stays x's own.
/// </summary>
public class TypeNavigatesAsFullTypeTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/tmp/typenav-" + System.Guid.NewGuid().ToString("N")[..8]);

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private static readonly byte[] GifBytes =
    {
        0x47,0x49,0x46,0x38,0x37,0x61,0x01,0x00,0x01,0x00,0x80,0x00,0x00,0x00,0x00,0x00,
        0xFF,0xFF,0xFF,0x2C,0x00,0x00,0x00,0x00,0x01,0x00,0x01,0x00,0x00,0x02,0x02,0x44,
        0x01,0x00,0x3B
    };

    private static async Task<string?> Read(Data x, string path)
        => (await (await x.Get(path)).Value())?.ToString();

    [Test]
    public async Task Text_TypeDescription_IsFilled()
    {
        var x = new Data("x", "hello", context: Ctx);
        await Assert.That(await Read(x, "!type.Description")).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task Image_TypeDescription_IsFilled_AndKindStaysItsOwn()
    {
        var x = new Data("x", GifBytes, new global::app.type.@this("image", "gif"), context: Ctx);
        await Assert.That(await Read(x, "!type.Description")).IsNotNull().And.IsNotEmpty();
        await Assert.That(await Read(x, "!type.Kind")).IsEqualTo("gif");
    }

    [Test]
    public async Task Goal_TypeDescription_IsFilled()
    {
        var x = new Data("x", global::PLang.Tests.Shared.Make.Goal("Start"), context: Ctx);
        await Assert.That(await Read(x, "!type.Description")).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task Choice_TypeValues_AnswersItsSetsOptions()
    {
        var x = new Data("x", new global::app.type.item.choice.@this<global::app.goal.step.ErrorOrder>(
            global::app.goal.step.ErrorOrder.RetryFirst), context: Ctx);
        var values = await (await x.Get("!type.Values")).Value() as global::app.type.item.list.@this;
        await Assert.That(values).IsNotNull();
        var options = values!.Items(Ctx).Select(o => o.Peek()?.ToString()).ToList();
        await Assert.That(options.Count).IsGreaterThan(1);
    }
}
