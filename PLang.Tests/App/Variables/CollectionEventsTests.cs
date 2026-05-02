namespace PLang.Tests.App.Variables;

// Variables.@this gains three collection-level events: OnSet, OnCreate, OnRemove.
// These complement (do not replace) the existing per-variable events on individual Data instances.
public class CollectionEventsTests
{
    [Test]
    public async Task OnSet_FiresOnRebind_WithBeforeAfter()
    {
        // Set existing variable → OnSet(name, before, after) fires.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task OnCreate_FiresOnInitialSet()
    {
        // First Set of a name → OnCreate(name, value) fires.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task OnRemove_FiresOnDelete()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task OnSet_DoesNotFireOnInitialSet()
    {
        // Initial set → OnCreate only, NOT OnSet (no "before" exists).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task PerVariableEvents_StillFire_BackCompat()
    {
        // The pre-existing per-Data OnChange/OnCreate/OnDelete events still fire.
        // Used by --debug={"variables":[...]}; must not regress.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Events_NotFired_AfterUnsubscribe()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
