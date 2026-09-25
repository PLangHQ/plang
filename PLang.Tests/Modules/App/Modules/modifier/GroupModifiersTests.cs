namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for the deterministic GroupModifiers pipeline in the builder save path.
/// Takes a flat list of actions (from LLM) and groups modifier actions onto
/// their preceding executable action, sorted by Order.
/// </summary>
public class GroupModifiersTests
{
    private static StepActions Flat(params (string module, string action)[] items)
    {
        var result = new StepActions();
        foreach (var (m, a) in items)
            result.Add(new PrAction { Module = global::PLang.Tests.TestApp.SharedContext.App.Module[m], Name = a });
        return result;
    }

    [Test]
    public async Task GroupModifiers_AGraftedModifier_HasItsOwnProperties()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        var actions = new StepActions
        {
            Make.Action("file", "read", ("Path", "a.txt")),
            Make.Action("timeout", "after", ("ms", 5000)),
        };

        var step = new Step { Code = actions }; step.Nest(modules);

        var action = step.Code[0];
        var modifier = action.Modifier[0];
        await Assert.That(modifier["ms"]).IsNotNull();
        await Assert.That(ReferenceEquals(modifier.Property, action.Property)).IsFalse();
        await Assert.That(ReferenceEquals(modifier.Default, action.Default)).IsFalse();
        modifier.Property.Add(Make.Property(new Data("extra", 1, context: app.User.Context)));
        await Assert.That(action["extra"]).IsNull();
    }

    [Test]
    public async Task GroupModifiers_NoModifiers_Unchanged()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        var actions = Flat(("file", "read"), ("variable", "set"));

        var step = new Step { Code = actions }; step.Nest(modules);

        await Assert.That(step.Code.Count).IsEqualTo(2);
        await Assert.That(step.Code[0].Module.Name).IsEqualTo("file");
        await Assert.That(step.Code[1].Module.Name).IsEqualTo("variable");
        await Assert.That(step.Code[0].Modifier.Count).IsEqualTo(0);
        await Assert.That(step.Code[1].Modifier.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GroupModifiers_ModifierAfterExecutable_Attached()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        var actions = Flat(("file", "read"), ("cache", "wrap"));

        var step = new Step { Code = actions }; step.Nest(modules);

        await Assert.That(step.Code.Count).IsEqualTo(1);
        await Assert.That(step.Code[0].Module.Name).IsEqualTo("file");
        await Assert.That(step.Code[0].Modifier.Count).IsEqualTo(1);
        await Assert.That(step.Code[0].Modifier[0].Module.Name).IsEqualTo("cache");
    }

    [Test]
    public async Task GroupModifiers_MultipleModifiersOnOneAction_SortedByOrder()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        // Insertion order timeout(3), cache(2), error(1) sorts to error, cache, timeout —
        // outermost first: the handler bounds the attempts, the deadline bounds one attempt.
        var actions = Flat(
            ("file", "read"),
            ("timeout", "after"),
            ("cache", "wrap"),
            ("error", "handle"));

        var step = new Step { Code = actions }; step.Nest(modules);

        await Assert.That(step.Code.Count).IsEqualTo(1);
        var mods = step.Code[0].Modifier;
        await Assert.That(mods.Count).IsEqualTo(3);
        await Assert.That(mods[0].Module.Name).IsEqualTo("error");
        await Assert.That(mods[1].Module.Name).IsEqualTo("cache");
        await Assert.That(mods[2].Module.Name).IsEqualTo("timeout");
    }

    [Test]
    public async Task GroupModifiers_ModifierBetweenTwoExecutables_AttachesToPreceding()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        var actions = Flat(("file", "read"), ("cache", "wrap"), ("variable", "set"));

        var step = new Step { Code = actions }; step.Nest(modules);

        await Assert.That(step.Code.Count).IsEqualTo(2);
        await Assert.That(step.Code[0].Module.Name).IsEqualTo("file");
        await Assert.That(step.Code[0].Modifier.Count).IsEqualTo(1);
        await Assert.That(step.Code[0].Modifier[0].Module.Name).IsEqualTo("cache");
        await Assert.That(step.Code[1].Module.Name).IsEqualTo("variable");
        await Assert.That(step.Code[1].Modifier.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GroupModifiers_LeadingModifier_NoPreceeding_EdgeCase()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        // Leading modifier has no preceding executable — it is dropped, not an error
        var actions = Flat(("cache", "wrap"), ("file", "read"));

        var step = new Step { Code = actions }; step.Nest(modules);

        await Assert.That(step.Code.Count).IsEqualTo(1);
        await Assert.That(step.Code[0].Module.Name).IsEqualTo("file");
        await Assert.That(step.Code[0].Modifier.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GroupModifiers_Mixed_CorrectGrouping()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        // [file.read, cache.wrap, error.handle, variable.set, timeout.after]
        // -> file.read with sorted [error(1), cache(2)]; variable.set with [timeout(3)]
        var actions = Flat(
            ("file", "read"),
            ("cache", "wrap"),
            ("error", "handle"),
            ("variable", "set"),
            ("timeout", "after"));

        var step = new Step { Code = actions }; step.Nest(modules);

        await Assert.That(step.Code.Count).IsEqualTo(2);
        await Assert.That(step.Code[0].Module.Name).IsEqualTo("file");
        await Assert.That(step.Code[0].Modifier.Count).IsEqualTo(2);
        await Assert.That(step.Code[0].Modifier[0].Module.Name).IsEqualTo("error");
        await Assert.That(step.Code[0].Modifier[1].Module.Name).IsEqualTo("cache");
        await Assert.That(step.Code[1].Module.Name).IsEqualTo("variable");
        await Assert.That(step.Code[1].Modifier.Count).IsEqualTo(1);
        await Assert.That(step.Code[1].Modifier[0].Module.Name).IsEqualTo("timeout");
    }
}
