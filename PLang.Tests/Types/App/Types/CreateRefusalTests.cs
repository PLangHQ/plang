namespace PLang.Tests.App.Types;

// A value a type can't be made from is the type's answer, never an exception out of its birth.
public class CreateRefusalTests
{
    [Test] public async Task Create_FromAValueNothingMakesItFrom_AnswersTypeConversionFailed()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-refuse-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var ctx = app.actor.list.User.Context;

        var refused = await app.type.list["dict"].Create(new global::app.type.item.text.@this("hello"), ctx);

        await refused.IsFailure();
        await Assert.That(refused.Error!.Key).IsEqualTo("TypeConversionFailed");
    }

    [Test] public async Task Create_ALiftThatDeclines_AnswersItsOwnKey()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-refuse-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var ctx = app.actor.list.User.Context;

        var refused = await app.type.list["number"].Create(new global::app.type.item.text.@this("abc"), ctx);

        await refused.IsFailure();
        await Assert.That(refused.Error!.Key).IsEqualTo("NumberConversionFailed");
    }
}
