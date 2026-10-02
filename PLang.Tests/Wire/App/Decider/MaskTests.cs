namespace PLang.Tests.App.Decider;

// The step as the decider and the writer read it (step.Mask): its variables placeholders, so a variable's own words are
// never read as the step's; an answer written in placeholders is restored at the formal reader before it is read. The
// pick's options ride the same placeholders, and an action's chosen options are carried whether it is certain or not.
public class MaskTests : System.IAsyncDisposable
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

    [Test]
    public async Task TheMask_NumbersEachVariableOnce_InTheOrderWritten_AndLeavesTheOtherWords()
    {
        var mask = Step("call goal Page module=%!app.module.file% with %x%, then %!app.module.file% again").Mask;

        await Assert.That(mask.Text).IsEqualTo("call goal Page module=%v1% with %v2%, then %v1% again");
        await Assert.That(mask.Placeholder).IsEquivalentTo(new[] { "%v1%", "%v2%" });
    }

    // a developer's own %v1% is never a placeholder: the stem moves on, and restore leaves it as written
    [Test]
    public async Task AStepsOwnV1_IsNeverAPlaceholder_AndRestoreLeavesIt()
    {
        var mask = Step("set %v1% = %x%").Mask;

        await Assert.That(mask.Text).IsEqualTo("set %v_1% = %v_2%");
        await Assert.That(mask.Restore("variable.set(Name=%v_1%, Value=%v_2%)")).IsEqualTo("variable.set(Name=%v1%, Value=%x%)");
        await Assert.That(mask.Restore("keep %v1% as written")).IsEqualTo("keep %v1% as written");
    }

    [Test]
    public async Task HideThenRestore_IsTheLineItself()
    {
        var mask = Step("foreach %person% as %value% with key %field%, call ShowEntry field=%field%").Mask;
        var line = "loop.foreach(Collection=%person%, Item=%value%, Key=%field%)";

        await Assert.That(mask.Hide(line)).IsEqualTo("loop.foreach(Collection=%v1%, Item=%v2%, Key=%v3%)");
        await Assert.That(mask.Restore(mask.Hide(line))).IsEqualTo(line);
    }

    // issue 32(b): the writer answers in placeholders; the reader restores before it reads — the goal stays Page, the
    // argument the variable
    [Test]
    public async Task AnAnswerInPlaceholders_ReadsAsTheStepsVariables_TheGoalNameStaysPage()
    {
        var step = Step("call goal Page module=%!app.module.file%");

        var read = new global::app.goal.step.action.formal.Reader(step, _app.module.list)
            .Read("goal.call(Name=\"Page\", Parameter={module: %v1%})", Ctx);

        await read.IsSuccess();
        var call = ((global::app.goal.step.action.list.@this)read.Peek()!).Items().Single();
        await Assert.That(call["Name"]?.Value?.RawText).IsEqualTo("Page");
        var written = new global::app.goal.step.action.formal.Writer();
        await ((global::app.goal.step.action.list.@this)read.Peek()!).Output(written, global::app.View.Store, Ctx);
        await Assert.That(written.ToString()).Contains("%!app.module.file%");
        await Assert.That(written.ToString()).DoesNotContain("%v1%");
    }

    // what a type offers for a step: a closed set its options; any other the step's own variables, as the decider reads them
    [Test]
    public async Task AChoiceOffersItsOptions_AnyOtherTypeTheStepsVariables()
    {
        var step = Step("foreach %person% as %value% with key %field%, read 'x.md'");
        var template = _app.Module("file")["read"]!["Template"]!.Type;
        var item = _app.Module("loop")["foreach"]!["Item"]!.Type;

        await Assert.That(template.Offers(step)).Contains("plang");
        await Assert.That(item.Offers(step)).IsEquivalentTo(new[] { "%v1%", "%v2%", "%v3%" });
    }

    // issue 2 at the writer: the option picks ride the starting line in placeholders, and the line reads back to the
    // step's variables
    [Test]
    public async Task ItemAndKey_RideTheStartingLine_AndReadBackToTheStepsVariables()
    {
        var step = Step("foreach %person% as %value% with key %field%, call ShowEntry field=%field% value=%value%");
        await step.Pick.Take(Answer(
            ("s0_loop.foreach", Yes(0.99)),
            ("s0_@option.loop.foreach.Item", Choice("%v2%")),
            ("s0_@option.loop.foreach.Key", Choice("%v3%"))), [], Ctx);

        var formal = step.Pick.Formal!;
        await Assert.That(formal).Contains("Item=%v2%");
        await Assert.That(formal).Contains("Key=%v3%");
        await Assert.That(formal).DoesNotContain("%value%");
        await Assert.That(step.Mask.Restore(formal)).Contains("Item=%value%, Key=%field%");
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
