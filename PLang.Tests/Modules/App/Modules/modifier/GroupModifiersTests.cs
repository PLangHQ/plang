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
            result.Add(new PrAction { Module = m, ActionName = a });
        return result;
    }

    [Test]
    public async Task GroupModifiers_NoModifiers_Unchanged()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        var actions = Flat(("file", "read"), ("variable", "set"));

        var step = new Step { Action = actions }; step.Nest(modules);

        await Assert.That(step.Action.Count).IsEqualTo(2);
        await Assert.That(step.Action[0].Module).IsEqualTo("file");
        await Assert.That(step.Action[1].Module).IsEqualTo("variable");
        await Assert.That(step.Action[0].Modifiers.Count).IsEqualTo(0);
        await Assert.That(step.Action[1].Modifiers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GroupModifiers_ModifierAfterExecutable_Attached()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        var actions = Flat(("file", "read"), ("cache", "wrap"));

        var step = new Step { Action = actions }; step.Nest(modules);

        await Assert.That(step.Action.Count).IsEqualTo(1);
        await Assert.That(step.Action[0].Module).IsEqualTo("file");
        await Assert.That(step.Action[0].Modifiers.Count).IsEqualTo(1);
        await Assert.That(step.Action[0].Modifiers[0].Module).IsEqualTo("cache");
    }

    [Test]
    public async Task GroupModifiers_MultipleModifiersOnOneAction_SortedByOrder()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        // Insertion order: error(3), cache(2), timeout(1) — should sort to timeout, cache, error
        var actions = Flat(
            ("file", "read"),
            ("error", "handle"),
            ("cache", "wrap"),
            ("timeout", "after"));

        var step = new Step { Action = actions }; step.Nest(modules);

        await Assert.That(step.Action.Count).IsEqualTo(1);
        var mods = step.Action[0].Modifiers;
        await Assert.That(mods.Count).IsEqualTo(3);
        await Assert.That(mods[0].Module).IsEqualTo("timeout");
        await Assert.That(mods[1].Module).IsEqualTo("cache");
        await Assert.That(mods[2].Module).IsEqualTo("error");
    }

    [Test]
    public async Task GroupModifiers_ModifierBetweenTwoExecutables_AttachesToPreceding()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        var actions = Flat(("file", "read"), ("cache", "wrap"), ("variable", "set"));

        var step = new Step { Action = actions }; step.Nest(modules);

        await Assert.That(step.Action.Count).IsEqualTo(2);
        await Assert.That(step.Action[0].Module).IsEqualTo("file");
        await Assert.That(step.Action[0].Modifiers.Count).IsEqualTo(1);
        await Assert.That(step.Action[0].Modifiers[0].Module).IsEqualTo("cache");
        await Assert.That(step.Action[1].Module).IsEqualTo("variable");
        await Assert.That(step.Action[1].Modifiers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GroupModifiers_LeadingModifier_NoPreceeding_EdgeCase()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        // Leading modifier has no preceding executable — it is dropped, not an error
        var actions = Flat(("cache", "wrap"), ("file", "read"));

        var step = new Step { Action = actions }; step.Nest(modules);

        await Assert.That(step.Action.Count).IsEqualTo(1);
        await Assert.That(step.Action[0].Module).IsEqualTo("file");
        await Assert.That(step.Action[0].Modifiers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GroupModifiers_Mixed_CorrectGrouping()
    {
        await using var app = TestApp.Create("/gm-" + System.Guid.NewGuid().ToString("N")[..6]); var modules = app.Module;
        // [file.read, cache.wrap, error.handle, variable.set, timeout.after]
        // -> file.read with sorted [cache(2), error(3)]; variable.set with [timeout(1)]
        var actions = Flat(
            ("file", "read"),
            ("cache", "wrap"),
            ("error", "handle"),
            ("variable", "set"),
            ("timeout", "after"));

        var step = new Step { Action = actions }; step.Nest(modules);

        await Assert.That(step.Action.Count).IsEqualTo(2);
        await Assert.That(step.Action[0].Module).IsEqualTo("file");
        await Assert.That(step.Action[0].Modifiers.Count).IsEqualTo(2);
        await Assert.That(step.Action[0].Modifiers[0].Module).IsEqualTo("cache");
        await Assert.That(step.Action[0].Modifiers[1].Module).IsEqualTo("error");
        await Assert.That(step.Action[1].Module).IsEqualTo("variable");
        await Assert.That(step.Action[1].Modifiers.Count).IsEqualTo(1);
        await Assert.That(step.Action[1].Modifiers[0].Module).IsEqualTo("timeout");
    }
}
