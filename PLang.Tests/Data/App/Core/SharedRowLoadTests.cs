namespace PLang.Tests.App.Core;

/// <summary>
/// A program row read off a real <c>.pr</c> is shared by every run and holds its value unloaded.
/// It has no context of its own — a run loads its own copy (<c>action[name]</c>). Asking the row
/// itself to load fails with a named error that says so, never a NullReferenceException.
/// </summary>
public class SharedRowLoadTests
{
    [Test]
    public async Task SharedRow_ReadDirectly_FailsWithNamedError()
    {
        await using var app = TestApp.Create("/tmp/sharedrow-" + System.Guid.NewGuid().ToString("N")[..8]);
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal("Start",
            Make.Step("set %x% = hello",
                Make.Action("variable", "set", ("Name", "%x%"), ("Value", "hello")))));

        var row = goal.Step[0].Action[0].Parameter.First(p => p.Name == "Value");
        await row.Value();

        await Assert.That(row.Success).IsFalse();
        await Assert.That(row.Error!.Key).IsEqualTo("NoContextToLoad");
    }
}
