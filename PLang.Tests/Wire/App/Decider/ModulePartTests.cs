namespace PLang.Tests.App.Decider;

// An action picked through its module is certain only when its module is: the module's share of the work no
// near-certain common action answers. Stage 1's main-work choice is one-of, so a step of two modules splits it; the
// share a settled module takes is not doubt about the other one.
public class ModulePartTests : System.IAsyncDisposable
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

    // set %x% = %x% + 1: variable is settled by variable.set, so math holds all the open work and math.add is certain;
    // the starting line adds first and the set keeps the sum
    [Test]
    public async Task ASetOfASum_IsCertainOfTheAdd_AndAddsFirst()
    {
        var step = Step("set %x% = %x% + 1");
        await step.Pick.Take(Answer(
            ("s0_variable.set", Yes(0.98)),
            ("s0_@module", Choice("math", 0.67, new Dictionary<string, object?> { ["math"] = 0.67, ["variable"] = 0.33 }))), [], Ctx);
        await step.Pick.Take(Answer(
            ("s0_@also.math", Yes(0.98)),
            ("s0_math", Choice("add"))), [], Ctx);

        await Assert.That(step.Pick.Listed.Single(l => l.Name == "math.add").Mark).IsEqualTo(global::app.goal.step.pick.listed.Mark.Certain);
        var formal = step.Pick.Formal!;
        await Assert.That(formal).Contains("math.add(");
        await Assert.That(formal).Contains("Value=%!data%");
        await Assert.That(formal.IndexOf("math.add(", StringComparison.Ordinal)).IsLessThan(formal.IndexOf("variable.set(", StringComparison.Ordinal));
    }

    // remove %x%: no common action settles a module, so file's share of the open work is its split share (0.56 of
    // 0.92) — the decider is unsure which module, and file.delete stays off the starting line whatever @also says
    [Test]
    public async Task ARemoveUnsureOfItsModule_StaysPossible()
    {
        var step = Step("remove %x%");
        await step.Pick.Take(Answer(
            ("s0_@module", Choice("file", 0.56, new Dictionary<string, object?> { ["file"] = 0.56, ["variable"] = 0.36 }))), [], Ctx);
        await step.Pick.Take(Answer(
            ("s0_@also.file", Yes(0.99)),
            ("s0_file", Choice("delete"))), [], Ctx);

        await Assert.That(step.Pick.Listed.Single(l => l.Name == "file.delete").Mark).IsEqualTo(global::app.goal.step.pick.listed.Mark.Possible);
        await Assert.That(step.Pick.Formal ?? "").DoesNotContain("file.delete");
    }

    // stage 1 answering its choice without probabilities names one module at its confidence: the share no module
    // claims (0.44) stays doubt, so file.delete stays Possible
    [Test]
    public async Task ARemoveAnsweredWithoutProbabilities_StaysPossible()
    {
        var step = Step("remove %x%");
        await step.Pick.Take(Answer(("s0_@module", Choice("file", 0.56))), [], Ctx);
        await step.Pick.Take(Answer(
            ("s0_@also.file", Yes(0.99)),
            ("s0_file", Choice("delete"))), [], Ctx);

        await Assert.That(step.Pick.Listed.Single(l => l.Name == "file.delete").Mark).IsEqualTo(global::app.goal.step.pick.listed.Mark.Possible);
        await Assert.That(step.Pick.Formal ?? "").DoesNotContain("file.delete");
    }
}
