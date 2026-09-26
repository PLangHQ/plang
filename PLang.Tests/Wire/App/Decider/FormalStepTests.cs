namespace PLang.Tests.App.Decider;

// A step written in formal is its code: it is read as written — with every check an answer's line gets —
// and asked of no one, not the decider, not the LLM. A formal step that doesn't read is refused with why.
public class FormalStepTests
{
    private static global::app.goal.@this Parse(string text, global::app.actor.context.@this context)
        => global::app.goal.@this.Parse(text, global::app.type.item.path.@this.Resolve("/Start.goal", context), context)!;

    private static Task<global::app.error.Error?> Read(global::app.goal.@this goal, string answer, global::app.actor.context.@this context)
        => goal.Step.Read(answer, context);

    [Test]
    public async Task AFormalStep_IsTakenAsWritten_AndAskedNothing()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(BootstrapTests.RepoRoot(), "os"));
        var context = os.User.Context;
        var goal = Parse("Start\n- output.write(Data=\"a\")\n- write out \"b\"\n", context);

        await Assert.That(goal.Step[0].IsFormal).IsTrue();
        await Assert.That(goal.Step[1].IsFormal).IsFalse();

        // the decider is asked nothing about it
        var answer = Make.Dict(new Dictionary<string, object?>
        {
            ["s0_output.write"] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = 0.99 },
            ["s1_output.write"] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = 0.99 },
        }, context);
        foreach (var s in goal.Step.Items()) await s.Pick.Take(answer, [], context);
        await Assert.That(goal.Step[0].Pick.Listed.Count).IsEqualTo(0);

        // the answer's line for it is set aside: its own text is its line
        var refused = await Read(goal, "[0] output.write(Data=\"x\")\n[1] output.write(Data=\"b\")", context);

        await Assert.That(refused?.Message).IsNull();
        var code = goal.Step[0].Code[0];
        await Assert.That($"{code.Module.Name}.{code.Name}").IsEqualTo("output.write");
        await Assert.That(code["Data"]?.Value?.ToString()).Contains("a");
    }

    [Test]
    public async Task AFormalStep_NamingAnUnknownAction_IsRefusedWithWhy()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(BootstrapTests.RepoRoot(), "os"));
        var context = os.User.Context;
        var goal = Parse("Start\n- output.shout(Data=\"a\")\n", context);

        var refused = await Read(goal, "", context);

        await Assert.That(refused?.Message).Contains("step 0 is written in formal and does not read");
        await Assert.That(refused?.Message).Contains("shout");
        await Assert.That(goal.Step[0].Code.Count).IsEqualTo(0);
    }
}
