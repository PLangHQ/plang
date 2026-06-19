namespace PLang.Tests.App.CompareRedesign;

// The real-read helpers (Make.Goal to construct, RealGoalLoad.ViaChannel to load
// through the actual channel I/O path). Proves the read assembles the action and
// keeps each param's type — the production path, not the in-C# PrAction shape that
// bypasses the read. (Make / RealGoalLoad come from PLang.Tests.Shared via a global using.)
public class ChannelStreamLoadTests
{
    private static Goal SampleGoal() => Make.Goal("G",
        Make.Step("write out",
            Make.Action("output", "write", ("Content", "Hi %name%"))));

    [Test]
    public async Task ViaChannel_AssemblesActions_AndKeepsParamType()
    {
        await using var app = TestApp.Create("/test");

        var loaded = await RealGoalLoad.ViaChannel(app, SampleGoal());

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

    [Test]
    public async Task ViaChannel_ParamTypes_FromValue_AndExplicit()
    {
        await using var app = TestApp.Create("/test");

        // number from the value (5 → number); variable declared explicitly.
        var goal = Make.Goal("G",
            Make.Step("set it",
                Make.Action("variable", "set",
                    Make.Param("Name", "target", "variable"),
                    ("Count", 5))));

        var loaded = await RealGoalLoad.ViaChannel(app, goal);
        var ps = loaded.Steps.First().Actions.First().Parameters;

        await Assert.That(ps.First(p => p.Name == "Name").Type.Name).IsEqualTo("variable");
        await Assert.That(ps.First(p => p.Name == "Count").Type.Name).IsEqualTo("number");
    }
}
