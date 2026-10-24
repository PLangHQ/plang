namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>
/// Stage 5's kind follow-up (pick/list): once a Type-coercion option is picked as a type whose kind is empty,
/// a KindOf question offers that type's kinds, and the answer refines the option to the kinded type
/// {name, kind}. Deterministic over pick.list with canned answers — it goes red if Refine is a no-op (the
/// option stays the bare type `number`, never `number<decimal>`), which the non-deterministic build measure
/// cannot catch.
/// </summary>
public class KindFollowUpTests : System.IAsyncDisposable
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kindfollowup-" + System.Guid.NewGuid().ToString("N")[..8]);
    private readonly global::app.@this _app;

    public KindFollowUpTests()
    {
        // variable.set's own teaching: its Type line asks which type the step coerces to (the `as <type>` slot).
        var notes = System.IO.Path.Combine(_root, "system", "modules", "variable");
        System.IO.Directory.CreateDirectory(notes);
        System.IO.File.WriteAllText(System.IO.Path.Combine(notes, "set.notes.md"),
            "Name — the variable · ask: which variable does the step keep its result in?\n" +
            "Value — the value the step writes\n" +
            "Type — the type the step coerces to · ask: which type does the step coerce the value to (none unless the step writes `as <type>`)?\n");
        _app = new global::app.@this(_root, global::PLang.Tests.Parent.Actor).Testing().Building();
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        System.IO.Directory.Delete(_root, recursive: true);
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

    [Test]
    public async Task AKindFollowUp_RefinesTheTypeOptionToTheKindedType()
    {
        var goal = global::PLang.Tests.Shared.Make.Goal(_app.actor.list.User.Context, "G",
            global::PLang.Tests.Shared.Make.Step("set %n% = 1.5"));
        var step = goal.Step[0];

        // stage 1: variable.set is the certain action
        await step.Pick.Take(global::PLang.Tests.Shared.Make.Dict(new Dictionary<string, object?>
        {
            ["s0_variable.set"] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = 0.99 },
        }, Ctx), [], Ctx);

        // the Type option is offered (its notes ask); pick the type `number`
        await Assert.That(step.Pick.Question.Select(q => q.Id)).Contains("s0_@option.variable.set.Type");
        await step.Pick.Take(global::PLang.Tests.Shared.Make.Dict(new Dictionary<string, object?>
        {
            ["s0_@option.variable.set.Type"] = new Dictionary<string, object?> { ["choice"] = "number", ["confidence"] = 0.9 },
        }, Ctx), [], Ctx);

        // number has program-named kinds, so a kind follow-up is asked; answer `decimal`
        await Assert.That(step.Pick.Question.Select(q => q.Id)).Contains("s0_@kindof.variable.set.Type");
        await step.Pick.Take(global::PLang.Tests.Shared.Make.Dict(new Dictionary<string, object?>
        {
            ["s0_@kindof.variable.set.Type"] = new Dictionary<string, object?> { ["choice"] = "decimal", ["confidence"] = 0.9 },
        }, Ctx), [], Ctx);

        // Refine folded the kind in: the option is the kinded type, not the bare `number`. A no-op Refine
        // would leave Type={"name": "number"} with no kind.
        await Assert.That(step.Pick.Formal).Contains("Type={\"name\": \"number\", \"kind\": \"decimal\"}");
    }

    // decision 814: a kind follow-up offers only the program-named kinds — item's json — never the C# host
    // kinds (dict, list, *), which are Internal. The `!k.Internal` filter in KindsOffered (pick/list) drops them.
    [Test]
    public async Task AKindFollowUp_ForItem_OffersJson_NotTheInternalHostKinds()
    {
        var goal = global::PLang.Tests.Shared.Make.Goal(_app.actor.list.User.Context, "G",
            global::PLang.Tests.Shared.Make.Step("set %x% = '{}'"));
        var step = goal.Step[0];

        await step.Pick.Take(global::PLang.Tests.Shared.Make.Dict(new Dictionary<string, object?>
        {
            ["s0_variable.set"] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = 0.99 },
        }, Ctx), [], Ctx);
        await step.Pick.Take(global::PLang.Tests.Shared.Make.Dict(new Dictionary<string, object?>
        {
            ["s0_@option.variable.set.Type"] = new Dictionary<string, object?> { ["choice"] = "item", ["confidence"] = 0.9 },
        }, Ctx), [], Ctx);

        var kindOf = step.Pick.Question.Single(q => q.Id == "s0_@kindof.variable.set.Type");
        await Assert.That(kindOf.Values).Contains("json");
        await Assert.That(kindOf.Values).DoesNotContain("dict");
        await Assert.That(kindOf.Values).DoesNotContain("list");
        await Assert.That(kindOf.Values).DoesNotContain("*");
    }
}
