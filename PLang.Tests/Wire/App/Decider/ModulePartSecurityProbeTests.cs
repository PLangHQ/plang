namespace PLang.Tests.App.Decider;

// Security probes (set-add-order). Each W test asserts TODAY'S edge (F1, Low accepted risk, ruled B at 74bf815d1): it flips to Possible when
// the backlog item A lands (the settled share removed only when the settled action is >= 0.9 on its own probability).
//
// Security probes (set-add-order): `Part` divides a module's stage-1 share by 1 less the shares of the modules a near-certain
// common action settles. Which destructive or sensitive action becomes Certain (so on the starting line, and a build that leaves
// it out is refused by Agree) where the plain share, the 09-30 rule, said Possible?
public class ModulePartSecurityProbeTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this(System.IO.Path.Combine(Fixture.Root(), "os"), global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private global::app.goal.step.@this Step(string text)
        => Make.Goal(Ctx, "G", "/G.goal", Make.Step(text, 0)).Step[0];

    private global::app.type.item.dict.@this Answer(params (string Id, object Value)[] answers)
        => Make.Dict(answers.ToDictionary(a => a.Id, a => a.Value), Ctx);

    private static Dictionary<string, object?> Choice(string choice, double confidence = 0.95, Dictionary<string, object?>? probabilities = null)
        => new() { ["type"] = "choice", ["choice"] = choice, ["confidence"] = confidence, ["probabilities"] = probabilities };

    private static Dictionary<string, object?> Yes(double score) => new() { ["type"] = "noul", ["noul"] = score };

    // the mark the module's action gets for stage 1 { module: share, variable: other }, the common variable.set at `set`
    // (null: no common action), the yes/no at 0.99
    private async Task<(global::app.goal.step.pick.listed.Mark Mark, string Formal)> Marked(string text, string module, string action,
        double share, double other, double? set)
    {
        var step = Step(text);
        var first = new List<(string Id, object Value)>
        {
            ("s0_@module", Choice(module, share, new Dictionary<string, object?> { [module] = share, ["variable"] = other })),
        };
        if (set is { } s) first.Add(("s0_variable.set", Yes(s)));
        await step.Pick.Take(Answer(first.ToArray()), [], Ctx);
        await step.Pick.Take(Answer(($"s0_@also.{module}", Yes(0.99)), ($"s0_{module}", Choice(action))), [], Ctx);
        return (step.Pick.Listed.Single(l => l.Name == $"{module}.{action}").Mark, step.Pick.Formal ?? "");
    }

    private const global::app.goal.step.pick.listed.Mark Certain = global::app.goal.step.pick.listed.Mark.Certain;
    private const global::app.goal.step.pick.listed.Mark Possible = global::app.goal.step.pick.listed.Mark.Possible;

    // the control (the 09-30 rule): no settled module, the plain share 0.60 is under Near, so Possible, and not on the line
    [Test]
    [Arguments("file", "delete")]
    [Arguments("code", "remove")]
    [Arguments("identity", "archive")]
    public async Task C1_NoSettledModule_ASixtyPercentShare_IsPossible(string module, string action)
    {
        var (mark, formal) = await Marked("remove %x%", module, action, 0.60, 0.36, null);
        await TestContext.Current!.OutputWriter.WriteLineAsync($"SA control {module}.{action}: {mark}");
        await Assert.That(mark).IsEqualTo(Possible);
        await Assert.That(formal).DoesNotContain($"{module}.{action}");
    }

    // the window: variable is settled by a common variable.set at 0.95 (the decider's yes/no about writing a variable), and the
    // variable share 0.36 is divided out: 0.60 / 0.64 = 0.94 — the destructive action is Certain on a 0.60 module share where the
    // plain share said Possible. Pinned as the observed edge (the step's words are `remove %x%`, which names no module)
    [Test]
    [Arguments("file", "delete")]
    [Arguments("code", "remove")]
    [Arguments("identity", "archive")]
    public async Task W1_AVariableSetSettles_ASixtyPercentShare_BecomesCertain(string module, string action)
    {
        var (mark, formal) = await Marked("remove %x%", module, action, 0.60, 0.36, 0.95);
        await TestContext.Current!.OutputWriter.WriteLineAsync($"SA window {module}.{action}: {mark} onLine={formal.Contains($"{module}.{action}")}");
        await Assert.That(mark).IsEqualTo(Certain);
    }

    // where the window opens for file.delete: the lowest file share that is Certain at variable 0.36 (0.9 x 0.64 = 0.576), and
    // with variable.set below Near it never opens
    [Test]
    public async Task W2_TheWindowsEdge_AndAUnsettledSetNeverOpensIt()
    {
        var below = await Marked("remove %x%", "file", "delete", 0.57, 0.36, 0.95);
        var above = await Marked("remove %x%", "file", "delete", 0.58, 0.36, 0.95);
        var unsettled = await Marked("remove %x%", "file", "delete", 0.80, 0.18, 0.89);
        await TestContext.Current!.OutputWriter.WriteLineAsync($"SA edge 0.57={below.Mark} 0.58={above.Mark}; set 0.89 at file 0.80={unsettled.Mark}");
        await Assert.That(below.Mark).IsEqualTo(Possible);
        await Assert.That(above.Mark).IsEqualTo(Certain);
        await Assert.That(unsettled.Mark).IsEqualTo(Possible);
    }

    // the worse end of the window: a near coin-flip (file 0.50, variable 0.46) with variable settled is Certain (0.50 / 0.54 = 0.93), and
    // so is the runner-up of a step the decider thinks is mainly variable's (variable 0.53, file 0.42: 0.42 / 0.47 = 0.89 is under,
    // 0.43 / 0.47 = 0.91 is over). Pinned as observed
    [Test]
    public async Task W3_ACloseRace_AndARunnerUp_BecomeCertain_WhenVariableIsSettled()
    {
        var race = await Marked("remove %x%", "file", "delete", 0.50, 0.46, 0.95);
        var runnerUp = await Marked("remove %x%", "file", "delete", 0.43, 0.53, 0.95);
        await TestContext.Current!.OutputWriter.WriteLineAsync($"SA close race file 0.50 / variable 0.46 settled: {race.Mark}; runner-up file 0.43 / variable 0.53 settled: {runnerUp.Mark}");
        await Assert.That(race.Mark).IsEqualTo(Certain);
        await Assert.That(runnerUp.Mark).IsEqualTo(Certain);
    }

    // a settled module that holds nearly all the mass leaves almost nothing: the division must not throw or exceed sanity
    [Test]
    public async Task W4_SettledSharesOfOneOrMore_AnswerZero_NotADivisionByZero()
    {
        var step = Step("remove %x%");
        await step.Pick.Take(Answer(
            ("s0_variable.set", Yes(0.95)),
            ("s0_@module", Choice("file", 0.5, new Dictionary<string, object?> { ["file"] = 0.5, ["variable"] = 1.0 }))), [], Ctx);
        await step.Pick.Take(Answer(("s0_@also.file", Yes(0.99)), ("s0_file", Choice("delete"))), [], Ctx);
        await Assert.That(step.Pick.Listed.Where(l => l.Name == "file.delete").All(l => l.Mark != Certain)).IsTrue();
    }
}
