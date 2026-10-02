namespace PLang.Tests.App.Decider;

// The pick's options: what a property's type offers for a step (a closed set its options, any other the step's own
// variables, as written), the chosen options riding the starting line, and an action's chosen options carried whether
// the decider is certain of it or not.
public class PickOptionTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this(System.IO.Path.Combine(Fixture.Root(), "os")).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private global::app.goal.step.@this Step(string text)
        => Make.Goal(Ctx, "G", "/G.goal", Make.Step(text, 0)).Step[0];

    private global::app.type.item.dict.@this Answer(params (string Id, object Value)[] answers)
        => Make.Dict(answers.ToDictionary(a => a.Id, a => a.Value), Ctx);

    private static Dictionary<string, object?> Choice(string choice, double confidence = 0.95, Dictionary<string, object?>? probabilities = null)
        => new() { ["type"] = "choice", ["choice"] = choice, ["confidence"] = confidence, ["probabilities"] = probabilities };

    private static Dictionary<string, object?> Yes(double score) => new() { ["type"] = "noul", ["noul"] = score };

    // what a type offers for a step: a closed set its options; any other the step's own variables, each once
    [Test]
    public async Task AChoiceOffersItsOptions_AnyOtherTypeTheStepsVariables()
    {
        var step = Step("foreach %person% as %value% with key %field%, read 'x.md' into %value%");
        var template = _app.Module("file")["read"]!["Template"]!.Type;
        var item = _app.Module("loop")["foreach"]!["Item"]!.Type;

        await Assert.That(template.Offers(step)).Contains("plang");
        await Assert.That(item.Offers(step)).IsEquivalentTo(new[] { "%person%", "%value%", "%field%" });
    }

    // issue 2 at the writer: the option picks ride the starting line as the step's variables
    [Test]
    public async Task ItemAndKey_RideTheStartingLine()
    {
        var step = Step("foreach %person% as %value% with key %field%, call ShowEntry field=%field% value=%value%");
        await step.Pick.Take(Answer(
            ("s0_loop.foreach", Yes(0.99)),
            ("s0_@option.loop.foreach.Item", Choice("%value%")),
            ("s0_@option.loop.foreach.Key", Choice("%field%"))), [], Ctx);

        await Assert.That(step.Pick.Formal!).Contains("Item=%value%, Key=%field%");
    }

    // issue 25: a listed action the decider is unsure of still carries the options chosen for it
    [Test]
    public async Task AListedButUncertainFileRead_CarriesItsTemplate()
    {
        var step = Step("read 'notes.md' and fill in its variables, write to %text%");
        await step.Pick.Take(Answer(("s0_@module", Choice("file", 0.72,
            new Dictionary<string, object?> { ["file"] = 0.72, ["variable"] = 0.28 }))), [], Ctx);
        await step.Pick.Take(Answer(
            ("s0_file", Choice("read")),
            ("s0_@also.file", Yes(0.8)),
            ("s0_@option.file.read.Template", Choice("plang"))), [], Ctx);

        var read = step.Pick.Listed.Single(l => l.Name == "file.read");
        await Assert.That(read.Mark).IsEqualTo(global::app.goal.step.pick.listed.Mark.Possible);
        await Assert.That(read.Option).Contains("Template=plang");
    }
}
