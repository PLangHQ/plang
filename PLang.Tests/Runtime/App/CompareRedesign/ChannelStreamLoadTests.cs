
namespace PLang.Tests.App.CompareRedesign;

// The real-read helper (PLang.Tests.Shared.RealGoalLoad.ViaChannel): a hand-built
// goal loaded through the actual channel I/O path. Proves the read assembles the
// action and keeps the param's type — the production path, not the in-C# PrAction
// shape that bypasses the read.
public class ChannelStreamLoadTests
{
    private static Goal SampleGoal() => PLang.Tests.Shared.Goals.Build("G",
        PLang.Tests.Shared.Goals.Step("write out",
            PLang.Tests.Shared.Goals.Action("output", "write", ("Content", "Hi %name%"))));

    [Test]
    public async Task ViaChannel_AssemblesActions_AndKeepsParamType()
    {
        await using var app = new global::app.@this("/test");

        var loaded = await PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, SampleGoal());

        var action = loaded.Steps.First().Actions.First();
        await Assert.That(action.Module).IsEqualTo("output");
        await Assert.That(action.ActionName).IsEqualTo("write");

        var param = action.Parameters.First(p => p.Name == "Content");
        // The type survives the read — self-describing, no Judge needed.
        await Assert.That(param.Type.Name).IsEqualTo("text");
        // And the %ref% borns a live template — a goal is authored code, so the goal
        // read stamps (mode rides the goal type, not the read path).
        await Assert.That(param.HasVariableReference).IsTrue();
    }
}
