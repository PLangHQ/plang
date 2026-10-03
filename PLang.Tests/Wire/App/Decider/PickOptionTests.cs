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

    // a goal one folder down is offered by the slash name a step writes (`call voice/Key`); a hidden folder's are not
    [Test]
    public async Task AGoalOneFolderDown_IsOfferedByItsSlashName()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_offer_" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "voice"));
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, ".build"));
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, "Start.goal"), "Start\n- call voice/Key\n");
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, "voice", "Key.goal"), "Key\n- write out \"k\"\n");
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, ".build", "Hidden.goal"), "Hidden\n- write out \"h\"\n");
        try
        {
            await using var app = new global::app.@this(root).Testing();
            var ctx = app.actor.list.User.Context;
            var goal = Make.Goal(ctx, "Start", "/Start.goal", Make.Step("call voice/Key", 0));

            var offers = Formal(await app.Module("goal")["call"]!["Name"]!.Type.Offers(goal.Step[0]));

            await Assert.That(offers).Contains("\"voice/Key\"");
            await Assert.That(offers.Any(offer => offer.Contains("Hidden"))).IsFalse();
        }
        finally { System.IO.Directory.Delete(root, true); }
    }

    // a conversation is offered only what can continue one: a variable the build's walk knows as a list is left out (no
    // candidates — only none); an earlier query's answer, which the walk can't type, stays
    [Test]
    public async Task AConversationIsOfferedOnlyWhatCanContinueOne()
    {
        var conversation = _app.Module("llm")["query"]!["Conversation"]!.Type;
        var listed = Make.Goal(Ctx, "D6", "/D6.goal",
            Make.Step("set %items% = [\"b\", \"a\"]", Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", "items", "variable"),
                ("Value", new List<object?> { "b", "a" }))),
            Make.Step("ask llm \"Sort these: %items%\", write to %answer%", 0));
        var answered = Make.Goal(Ctx, "Chat", "/Chat.goal",
            Make.Step("ask llm \"hi\", write to %a%", Make.Action(Ctx, "llm", "query", ("Message", "hi")),
                Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", "a", "variable"), ("Value", "%!data%"))),
            Make.Step("ask llm \"more\", continue %a%", 0));

        await listed.Step.Scope(Ctx);
        await answered.Step.Scope(Ctx);

        // the step's own write-to is all that's left, and the pick never offers where the answer goes: only none
        await Assert.That(Formal(await conversation.Offers(listed.Step[1]))).IsEquivalentTo(new[] { "%answer%" });
        await Assert.That(Formal(await conversation.Offers(answered.Step[1]))).IsEquivalentTo(new[] { "%a%" });

        // a dict can be a conversation (its members), so one set from a dict is offered
        var dicted = Make.Goal(Ctx, "Dict", "/Dict.goal",
            Make.Step("set %c% = {continue: %a%}", Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", "c", "variable"),
                ("Value", new Dictionary<string, object?> { ["mood"] = "calm" }))),
            Make.Step("ask llm \"go on\", conversation %c%", 0));
        await dicted.Step.Scope(Ctx);
        await Assert.That(Formal(await conversation.Offers(dicted.Step[1]))).IsEquivalentTo(new[] { "%c%" });
    }

    // a bool is a closed pair: offered true and false, never the step's variable; a formal true still reads
    [Test]
    public async Task ABoolIsOfferedTrueAndFalse_NeverAVariable()
    {
        var step = Step("set default %flag% = true");

        var offers = Formal(await _app.Module("variable")["set"]!["Default"]!.Type.Offers(step));
        var read = new global::app.goal.step.action.formal.Reader(step, _app.module.list)
            .Read("variable.set(Name=%flag%, Value=true, Default=true)", Ctx);

        await Assert.That(offers).IsEquivalentTo(new[] { "true", "false" });
        await read.IsSuccess();
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

    // whether a type's offers are all it can be is the type's own answer: a choice's options and a bool's pair are; a
    // goal's names (a convenience over every goal Find reaches), the step's variables and a separator's named kinds
    // beside variables are not
    [Test]
    public async Task AChoiceAndABoolAreClosed_AGoalAVariableAndASeparatorAreNot()
    {
        await Assert.That(_app.Module("on")["error"]!["Order"]!.Type.IsClosed).IsTrue();
        await Assert.That(_app.Module("variable")["set"]!["Default"]!.Type.IsClosed).IsTrue();
        await Assert.That(_app.Module("goal")["call"]!["Name"]!.Type.IsClosed).IsFalse();
        await Assert.That(_app.Module("loop")["foreach"]!["Item"]!.Type.IsClosed).IsFalse();
        await Assert.That(_app.Module("list")["join"]!["Separator"]!.Type.IsClosed).IsFalse();
    }

    // an option of a closed set the decider answered none for, written by the code, is invented — refused with the hint
    // to leave it out; one the decider chose stays the code's; an option of an open set answered none (a goal no offer
    // names, the step's variables, a separator) is the decider's guess and never refuses the writer
    [Test]
    public async Task AClosedOptionAnsweredNone_WrittenAnyway_IsRefused_AnOpenOneIsNot()
    {
        async Task<List<string>> Refused(string text, string[] actions, (string, object)[] options, string formal)
        {
            var step = Step(text);
            await step.Pick.Take(Answer(actions.Select(a => ($"s0_{a}", (object)Yes(0.99))).ToArray()), [], Ctx);
            await step.Pick.Take(Answer(options), [], Ctx);
            var read = new global::app.goal.step.action.formal.Reader(step, _app.module.list).Read(formal, Ctx);
            await read.IsSuccess();
            return step.Pick.Agree((global::app.goal.step.action.list.@this)read.Peek()!).Refused;
        }
        const string retry = "read 'x.txt', on error retry 2 times, then call Rollback";
        string[] readAndRetry = ["file.read", "on.error"];

        var invented = await Refused(retry, readAndRetry, [("s0_@option.on.error.Order", Choice("none"))],
            """file.read(Path="x.txt"); on.error(RetryCount=2, Order=GoalFirst, Recovery=[goal.call(Name="Rollback")])""");
        var left = await Refused(retry, readAndRetry, [("s0_@option.on.error.Order", Choice("none"))],
            """file.read(Path="x.txt"); on.error(RetryCount=2, Recovery=[goal.call(Name="Rollback")])""");
        var chosen = await Refused("read 'x.txt', on error call Fallback then retry", readAndRetry,
            [("s0_@option.on.error.Order", Choice("GoalFirst"))],
            """file.read(Path="x.txt"); on.error(RetryCount=1, Order=GoalFirst, Recovery=[goal.call(Name="Fallback")])""");
        var unoffered = await Refused("call Finalize", ["goal.call"], [("s0_@option.goal.call.Name", Choice("none"))],
            """goal.call(Name="Finalize")""");
        var guessed = await Refused("foreach %products% as %product%, call Ship", ["loop.foreach", "goal.call"],
            [("s0_@option.loop.foreach.Item", Choice("none"))],
            """loop.foreach(Collection=%products%, Item=%product%); goal.call(Name="Ship")""");
        var separated = await Refused("join %names% with ';'", ["list.join"], [("s0_@option.list.join.Separator", Choice("none"))],
            """list.join(ListName=%names%, Separator=";")""");

        await Assert.That(invented).Contains("step 0's on.error writes Order, which the step doesn't give: leave Order out");
        await Assert.That(left.Any(r => r.Contains("writes Order"))).IsFalse();
        await Assert.That(chosen.Any(r => r.Contains("writes Order"))).IsFalse();
        await Assert.That(unoffered.Any(r => r.Contains("writes Name"))).IsFalse();
        await Assert.That(guessed.Any(r => r.Contains("writes Item"))).IsFalse();
        await Assert.That(separated.Any(r => r.Contains("writes Separator"))).IsFalse();
    }

    // a property written on an action that doesn't have it, when another action the decider offered for the step does,
    // is refused naming that action — the retry writes it there; a property no offered action has keeps the plain refusal
    [Test]
    public async Task APropertyOnTheWrongAction_IsRefusedNamingTheOfferedActionThatTakesIt()
    {
        var step = Step("call Show with the file at 'x.txt'");
        await step.Pick.Take(Answer(("s0_goal.call", Yes(0.93)), ("s0_file.read", Yes(0.81))), [], Ctx);
        var reader = new global::app.goal.step.action.formal.Reader(step, _app.module.list);

        var named = reader.Read("""goal.call(Name="Show", Path="x.txt")""", Ctx);
        var plain = reader.Read("""goal.call(Name="Show", Window="w")""", Ctx);
        var retried = reader.Read("""file.read(Path="x.txt"); goal.call(Name="Show")""", Ctx);

        await named.IsFailure();
        await Assert.That(named.Error!.Message).Contains("`goal.call` has no property `Path`");
        await Assert.That(named.Error!.Message).Contains("`file.read` takes `Path`");
        await plain.IsFailure();
        await Assert.That(plain.Error!.Message).DoesNotContain("takes `Window`");
        await retried.IsSuccess();
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

    // issue 40: a common action named weakly at stage 1 (goal.call 0.46) that stage 2 confirms through its module (also
    // 0.98, its choice "call") is picked on stage 2's answer — one pick, the stronger evidence; unconfirmed, the stage-1
    // score stands (under Possible: not listed)
    [Test]
    [Arguments(0.98, true)]
    [Arguments(0.2, false)]
    public async Task ACommonActionStage2Confirms_IsPickedOnTheStrongerEvidence(double also, bool listed)
    {
        var step = Step("call /system/builder/EmitBuildEvent kind=\"properties\"");
        await step.Pick.Take(Answer(
            ("s0_goal.call", Yes(0.46)),
            ("s0_@module", Choice("goal", 0.87, new Dictionary<string, object?> { ["goal"] = 0.87, ["variable"] = 0.13 }))), [], Ctx);
        await step.Pick.Take(Answer(
            ("s0_@also.goal", Yes(also)),
            ("s0_goal", Choice("call", 1.0))), [], Ctx);

        await Assert.That(step.Pick.Listed.Any(l => l.Name == "goal.call")).IsEqualTo(listed);
    }

    // a common action certain on its own stands, whatever stage 2 says through a module the decider is unsure of
    [Test]
    public async Task ACommonActionCertainOnItsOwn_StaysCertain()
    {
        var step = Step("call /system/builder/EmitBuildEvent kind=\"properties\"");
        await step.Pick.Take(Answer(
            ("s0_goal.call", Yes(0.95)),
            ("s0_@module", Choice("goal", 0.87, new Dictionary<string, object?> { ["goal"] = 0.87, ["variable"] = 0.13 }))), [], Ctx);
        await step.Pick.Take(Answer(
            ("s0_@also.goal", Yes(0.98)),
            ("s0_goal", Choice("call", 1.0))), [], Ctx);

        await Assert.That(step.Pick.Listed.Single(l => l.Name == "goal.call").Mark).IsEqualTo(global::app.goal.step.pick.listed.Mark.Certain);
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
