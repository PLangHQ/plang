namespace PLang.Tests.App.VariablesTests;

public class CollectionEventsTests
{
    [Test]
    public async Task OnSet_FiresOnRebind_WithBeforeAfter()
    {
        var vars = new Variables();
        vars.Set("name", "old");

        string? capturedName = null;
        object? capturedBefore = null, capturedAfter = null;
        vars.OnSet += (n, before, after) => { capturedName = n; capturedBefore = before; capturedAfter = after; };

        vars.Set("name", "new");

        await Assert.That(capturedName).IsEqualTo("name");
        await Assert.That(capturedBefore?.ToString()).IsEqualTo("old");
        await Assert.That(capturedAfter?.ToString()).IsEqualTo("new");
    }

    [Test]
    public async Task OnCreate_FiresOnInitialSet()
    {
        var vars = new Variables();
        string? capturedName = null;
        object? capturedValue = null;
        vars.OnCreate += (n, v) => { capturedName = n; capturedValue = v; };

        vars.Set("name", "ingi");

        await Assert.That(capturedName).IsEqualTo("name");
        await Assert.That(capturedValue).IsEqualTo("ingi");
    }

    [Test]
    public async Task OnRemove_FiresOnDelete()
    {
        var vars = new Variables();
        vars.Set("name", "ingi");
        string? capturedName = null;
        vars.OnRemove += n => capturedName = n;

        vars.Remove("name");
        await Assert.That(capturedName).IsEqualTo("name");
    }

    [Test]
    public async Task OnSet_DoesNotFireOnInitialSet()
    {
        var vars = new Variables();
        var setFired = false;
        vars.OnSet += (_, _, _) => setFired = true;

        vars.Set("name", "first");
        await Assert.That(setFired).IsFalse();
    }

    [Test]
    public async Task PerVariableEvents_StillFire_BackCompat()
    {
        // Per-variable Data.OnChange must still fire — used by --debug={"variable":[...]}.
        var vars = new Variables();
        vars.Set("name", "first");

        var data = await vars.Get("name");
        bool perVarFired = false;
        data.OnChange.Add((oldData, newData) => perVarFired = true);

        vars.Set("name", "second");

        await Assert.That(perVarFired).IsTrue();
    }

    [Test]
    public async Task Events_NotFired_AfterUnsubscribe()
    {
        var vars = new Variables();
        vars.Set("name", "first");
        var fired = false;
        Action<string, object?, object?> handler = (_, _, _) => fired = true;
        vars.OnSet += handler;
        vars.OnSet -= handler;

        vars.Set("name", "second");
        await Assert.That(fired).IsFalse();
    }
}
