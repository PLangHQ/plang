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

    // each offer as it writes itself into a formal line
    private static List<string> Formal(IReadOnlyList<global::app.type.item.@this> offers) => offers.Select(o =>
    {
        var writer = new global::app.goal.step.action.formal.Writer();
        o.Write(writer);
        return writer.ToString();
    }).ToList();

    // what a type offers for a step: a closed set its options (each a choice); any other the step's own variables, each once
    [Test]
    public async Task AChoiceOffersItsOptions_AnyOtherTypeTheStepsVariables()
    {
        var step = Step("foreach %person% as %value% with key %field%, read 'x.md' into %value%");
        var template = _app.Module("file")["read"]!["Template"]!.Type;
        var item = _app.Module("loop")["foreach"]!["Item"]!.Type;

        var options = await template.Offers(step);
        await Assert.That(options.All(o => o is global::app.type.item.choice.IChoice)).IsTrue();
        await Assert.That(Formal(options)).Contains("\"plang\"");
        await Assert.That(Formal(await item.Offers(step))).IsEquivalentTo(new[] { "%person%", "%value%", "%field%" });
    }

    // a goal is offered by the goals the step can call by name — its own goal and the goals in its file — then the
    // step's variables
    [Test]
    public async Task AGoalOffersTheGoalsTheStepCanCall()
    {
        var goal = Make.Goal(Ctx, "Modules", "/docs/Modules.goal", Make.Step("call goal Page module=%m%", 0));
        goal.Child.Add(Make.Goal(Ctx, "Page", "/docs/Modules.goal"));

        var offers = await _app.Module("goal")["call"]!["Name"]!.Type.Offers(goal.Step[0]);

        await Assert.That(Formal(offers)).IsEquivalentTo(new[] { "\"Modules\"", "\"Page\"", "%m%" });
    }

    // a type is offered by the plang type names, never one plang keeps for itself
    [Test]
    public async Task ATypeOffersThePlangTypeNames()
    {
        var offers = Formal(await _app.Module("variable")["set"]!["Type"]!.Type.Offers(Step("set %p% = \"a.txt\" as path")));

        await Assert.That(offers).Contains("\"path\"");
        await Assert.That(offers).Contains("\"text\"");
        await Assert.That(offers).DoesNotContain("\"wire\"");
    }

    // a goal and a type the decider picked ride the starting line in quotes, the goal's slot filled — and it reads
    [Test]
    public async Task APickedGoalAndType_RideTheStartingLine_AndRead()
    {
        var goal = Make.Goal(Ctx, "Modules", "/docs/Modules.goal", Make.Step("call goal Page module=%m%", 0));
        goal.Child.Add(Make.Goal(Ctx, "Page", "/docs/Modules.goal"));
        var call = goal.Step[0];
        await call.Pick.Take(Answer(
            ("s0_goal.call", Yes(0.99)),
            ("s0_@option.goal.call.Name", Choice("Page"))), [], Ctx);
        var set = Step("set %p% = \"a.txt\" as path");
        await set.Pick.Take(Answer(
            ("s0_variable.set", Yes(0.99)),
            ("s0_@option.variable.set.Type", Choice("path"))), [], Ctx);

        await Assert.That(call.Pick.Formal!).Contains("goal.call(Name=\"Page\")");
        await Assert.That(set.Pick.Formal!).Contains("Type=\"path\"");
        var read = new global::app.goal.step.action.formal.Reader(set, _app.module.list)
            .Read(set.Pick.Formal!.Replace("Name, Value", "Name=%p%, Value=\"a.txt\""), Ctx);
        await read.IsSuccess();
    }

    // a pick no offer shows (a goal the step can't reach) chooses nothing: the slot stays to fill
    [Test]
    public async Task APickNoOfferShows_ChoosesNothing()
    {
        var call = Step("call goal Page module=%m%");
        await call.Pick.Take(Answer(
            ("s0_goal.call", Yes(0.99)),
            ("s0_@option.goal.call.Name", Choice("Page"))), [], Ctx);

        await Assert.That(call.Pick.Formal!).Contains("goal.call(Name)");
    }

    // issue 36: the variable an action binds when the step names none (foreach's %item%) is never offered — choosing it
    // is choosing none — and a pick of it chooses nothing
    [Test]
    public async Task TheVariableAnActionBindsByDefault_IsNeverOffered()
    {
        var step = Step("foreach %x%, call Y y=%item%");
        await step.Pick.Take(Answer(("s0_loop.foreach", Yes(0.99))), [], Ctx);

        var key = step.Pick.Question.Single(q => q.Property?.Name == "Key");
        await Assert.That(key.Values).Contains("%x%");
        await Assert.That(key.Values).DoesNotContain("%item%");

        await step.Pick.Take(Answer(("s0_@option.loop.foreach.Key", Choice("%item%"))), [], Ctx);
        await Assert.That(step.Pick.Formal!).DoesNotContain("Key=");
    }

    // issue 34: a permission — and a list of them — is offered that the step gives one; chosen, it enters the line as
    // the option's bare name, a slot the writer fills
    [Test]
    public async Task APermissionIsOfferedThatTheStepGivesOne_EnteringAsItsBareName()
    {
        var ctx = Ctx;
        var step = Step("start it, it may read X and write /granted");
        var one = await _app.type.list["permission"].Offers(step);
        var many = await _app.type.list[new global::app.type.@this("list", "permission"), ctx].Offers(step);

        await Assert.That(one.Single()).IsSameReferenceAs(global::app.type.item.given.@this.Instance);
        await Assert.That(many.Single()).IsSameReferenceAs(global::app.type.item.given.@this.Instance);
        var line = new global::app.goal.step.action.formal.Writer();
        line.Option("Permission", one.Single());
        await Assert.That(line.ToString()).IsEqualTo("Permission");
        var valued = new global::app.goal.step.action.formal.Writer();
        valued.Option("Template", (await _app.Module("file")["read"]!["Template"]!.Type.Offers(step)).First());
        await Assert.That(valued.ToString()).StartsWith("Template=\"");
    }

    // the step's write-to is where its answer goes, never an option's value
    [Test]
    public async Task AnOptionsOffers_LeaveOutTheStepsWriteTo()
    {
        var step = Step("foreach %person% as %value%, write to %answer%");
        await step.Pick.Take(Answer(("s0_loop.foreach", Yes(0.99))), [], Ctx);

        var item = step.Pick.Question.Single(q => q.Property?.Name == "Item");

        await Assert.That(item.Values).Contains("%value%");
        await Assert.That(item.Values).DoesNotContain("%answer%");
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
        await Assert.That(read.Option).Contains("Template=\"plang\"");
    }
}
