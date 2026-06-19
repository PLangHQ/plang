using System.Linq;

namespace PLang.Tests.App.CompareRedesign;

// The real-read helper (PLang.Tests.Shared.RealGoalLoad.ViaChannel): a hand-built
// goal loaded through the actual channel I/O path. Proves the read assembles the
// action and keeps the param's type — the production path, not the in-C# PrAction
// shape that bypasses the read.
public class ChannelStreamLoadTests
{
    private static Goal SampleGoal() => new()
    {
        Name = "G",
        Path = "/G.goal",
        Steps = new GoalSteps
        {
            new Step
            {
                Index = 0,
                Text = "write out",
                Actions = new StepActions
                {
                    new PrAction
                    {
                        Module = "output",
                        ActionName = "write",
                        Parameters = new List<Data> { new("Content", "Hi %name%") }
                    }
                }
            }
        }
    };

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
    }
}
