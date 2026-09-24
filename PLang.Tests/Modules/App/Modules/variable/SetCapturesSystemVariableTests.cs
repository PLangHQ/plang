namespace PLang.Tests.App.Modules.variable;

/// <summary>
/// `set %start% = %Now%` captures the answer at the moment of the set — %start% holds that moment
/// and does not stay live, while %Now% keeps moving.
/// </summary>
public class SetCapturesSystemVariableTests
{
    [Test]
    public async Task SetFromNow_HoldsTheMomentOfTheSet()
    {
        await using var app = TestApp.Create("/tmp/setnow-" + System.Guid.NewGuid().ToString("N")[..8]);
        var context = app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%start%"), ("value", "%Now%"));
        await (await action.Run(context)).IsSuccess();

        var first = (await (await context.Variable.Get("start")).Value())?.ToString();
        await Task.Delay(30);
        var second = (await (await context.Variable.Get("start")).Value())?.ToString();
        var now = (await (await context.Variable.Get("Now")).Value())?.ToString();

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(now).IsNotEqualTo(first);
    }
}
