namespace PLang.Tests.App.Goals;

/// <summary>
/// Stage 1 (item-doors, decision 867): a type named in a step is the REGISTERED one, not
/// a fresh <c>type.@this(name)</c> minted at .pr read. The three places that today ask
/// "is this an action slot?" by comparing <c>slot.Type.Name == "action"</c>
/// (<c>goal/step/action/this.Validate.cs:54–55</c>, <c>formal/reader.cs:221</c>, <c>:349</c>,
/// <c>serializer/Reader.cs:153–154</c>; F8.1) can only do that because a bare, fresh
/// <c>type.@this("action")</c> and the registered action type ARE indistinguishable by
/// name alone — the fresh one carries no facts (no <c>ClrType</c>, no registered kinds).
/// After item-doors #1 the slot's type is the registered one, carrying its facts, and
/// the question is asked of the type itself.
///
/// <para>Red on runtime2: today <c>new type.@this("action")</c> at <c>type/this.cs:146–149</c>
/// has <c>_clrType = null</c> while the registered type at <c>app.type.list["action"]</c>
/// has <c>ClrType = typeof(action.@this)</c>. The Name string compare the three sites rely
/// on can't tell them apart.</para>
/// </summary>
public class ActionSlotIsByTypeTests
{
    private string _dir = null!;
    private global::app.@this _app = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_actionslot_" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_dir);
        _app = new global::app.@this(_dir);
        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try { await _app.DisposeAsync(); } catch { }
        try { System.IO.Directory.Delete(_dir, true); } catch (System.IO.IOException) { }
    }

    [Test] public async Task AFreshTypeByNameCarriesNoFacts_TheRegisteredOneDoes()
    {
        var fresh = new global::app.type.@this("action");
        var registered = _app.type.list["action"];

        // the registered action type carries its CLR class (its real facts)
        await Assert.That(registered.ClrType).IsEqualTo(typeof(global::app.goal.step.action.@this));

        // today a fresh type-by-name has none of those facts — same name, no identity
        await Assert.That(fresh.ClrType).IsEqualTo(registered.ClrType);
    }
}
