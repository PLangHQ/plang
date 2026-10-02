namespace PLang.Tests.Generator;

/// <summary>
/// A typed slot's "was it given" is presence (<c>HasValue</c>), never truthiness (decision 463): a written
/// <c>false</c> or <c>0</c> is given, so it beats the setting and the <c>[Default]</c>. Truthiness there would
/// turn every written false into "not given" and quietly take the default.
/// </summary>
public class GivenSlotTests
{
    private static async Task<string?> Split(string step)
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var result = await context.Action(step).Start(context);
        await result.IsSuccess();
        return ((await result.Value()) as global::app.type.item.list.@this)?.Count.ToString();
    }

    // list.split's Empty is [Default(empty.keep)]: written drop drops the empty piece.
    [Test]
    public async Task WrittenDrop_IsGiven_NotTheDefault()
        => await Assert.That(await Split("list.split(Value=\"a,,b\", Empty=drop)")).IsEqualTo("2");

    [Test]
    public async Task LeftOut_TakesTheDefault()
        => await Assert.That(await Split("list.split(Value=\"a,,b\")")).IsEqualTo("3");
}
