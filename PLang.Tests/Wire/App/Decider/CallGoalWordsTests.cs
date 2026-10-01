namespace PLang.Tests.App.Decider;

// `call goal X` calls X: the word goal is plang's, never the goal's name. The answer that calls X is taken; one
// that calls another goal is refused with the goal the step names.
public class CallGoalWordsTests
{
    [Test]
    [Arguments("Page", null)]
    [Arguments("Other", "step 0 calls Page, but no action calls it")]
    public async Task CallGoalX_IsACallOfX(string called, string? refusal)
    {
        await using var os = new global::app.@this(System.IO.Path.Combine(BootstrapTests.RepoRoot(), "os")).Testing().Building();
        var context = os.actor.list.User.Context;
        var goal = global::app.goal.@this.Parse("Start\n- call goal Page module=%!app.module.file%\n\nPage\n- write out \"x\"\n\nOther\n- write out \"y\"\n",
            global::app.type.item.path.@this.Resolve("/Start.goal", context), context)!;
        var picks = Make.Dict(new Dictionary<string, object?>
        {
            ["s0_goal.call"] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = 0.97 },
        }, context);
        await goal.Step[0].Pick.Take(picks, [], context);

        var refused = await goal.Step.Read($"[0] goal.call(Name=\"{called}\", Parameter={{module: %!app.module.file%}})", context);

        if (refusal == null) await Assert.That(refused?.Message).IsNull();
        else await Assert.That(refused?.Message).Contains(refusal);
    }
}
